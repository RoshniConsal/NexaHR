using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NexaHR.Application.DTOs.Audit;
using NexaHR.Application.DTOs.CvMatching;
using NexaHR.Application.Interfaces.Audit;
using NexaHR.Application.Interfaces.CvMatching;
using NexaHR.Application.Services.CvMatching;
using NexaHR.Domain.Constants;
using NexaHR.Domain.Entities;
using NexaHR.Infrastructure.Identity;
using NexaHR.Infrastructure.Persistence;

namespace NexaHR.Infrastructure.Services.CvMatching;

public sealed class SkillCatalogueService
    : ISkillCatalogueService
{
    private readonly NexaHRDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IConfiguration _configuration;
    private readonly IAuditWriter _audit;

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public SkillCatalogueService(
        NexaHRDbContext db,
        UserManager<ApplicationUser> users,
        IConfiguration configuration,
        IAuditWriter audit)
    {
        _db = db;
        _users = users;
        _configuration = configuration;
        _audit = audit;
    }

    private async Task<bool> IsAuthorizedHrAsync(
        Guid actorUserId)
    {
        if (actorUserId == Guid.Empty)
            return false;

        var user = await _users.FindByIdAsync(
            actorUserId.ToString());

        if (user is null ||
            !user.IsActive ||
            !user.EmailConfirmed ||
            await _users.IsLockedOutAsync(user))
        {
            return false;
        }

        var roles = await _users.GetRolesAsync(user);

        return roles.Count == 1 &&
            roles.Contains(AppRoles.HRManager);
    }

    private bool CanPublish(Guid actorUserId)
    {
        return _configuration
            .GetSection(
                "Recruitment:SkillCatalogue:PublisherUserIds")
            .GetChildren()
            .Any(item =>
                Guid.TryParse(item.Value, out var id) &&
                id != Guid.Empty &&
                id == actorUserId);
    }

    private static string Hash(string value)
    {
        return Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(value)));
    }

    private static CvMatchingOperationResponse<
        SkillCatalogueResponse> Fail(
            string code,
            string message)
    {
        return new(false, code, message, null);
    }

    public async Task<
        CvMatchingOperationResponse<SkillCatalogueResponse>>
        GetCurrentAsync(
            Guid actorUserId,
            CancellationToken cancellationToken)
    {
        if (!await IsAuthorizedHrAsync(actorUserId))
            return Fail("forbidden", "Access denied.");

        try
        {
            await using var transaction =
                await _db.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);

            var current = await _db.SkillCatalogueVersions
                .AsNoTracking()
                .OrderByDescending(x => x.Version)
                .FirstOrDefaultAsync(cancellationToken);

            if (current is null)
            {
                await transaction.CommitAsync(
                    cancellationToken);

                return new(
                    true,
                    "success",
                    "No catalogue has been published.",
                    new SkillCatalogueResponse
                    {
                        Version = 0,
                        Entries = [],
                        CanPublish = CanPublish(actorUserId)
                    });
            }

            var versionIndex =
                await LoadVersionIndexAsync(
                    cancellationToken);

            var response = Map(
                current,
                CanPublish(actorUserId),
                versionIndex);

            await transaction.CommitAsync(
                cancellationToken);

            return new(
                true,
                "success",
                "Skill catalogue retrieved.",
                response);
        }
        catch (JsonException)
        {
            return Fail(
                "invalid_catalogue",
                "Saved catalogue is invalid.");
        }
        catch (ArgumentException)
        {
            return Fail(
                "invalid_catalogue",
                "Saved catalogue validation failed.");
        }
        catch (DbException)
        {
            return Fail(
                "catalogue_database_error",
                "Catalogue could not be retrieved.");
        }
    }

    public async Task<
        CvMatchingOperationResponse<SkillCatalogueResponse>>
        PublishAsync(
            Guid actorUserId,
            PublishSkillCatalogueRequest request,
            CancellationToken cancellationToken)
    {
        if (!await IsAuthorizedHrAsync(actorUserId) ||
            !CanPublish(actorUserId))
        {
            return Fail(
                "forbidden",
                "Catalogue publication permission required.");
        }

        if (request is null ||
            request.ExpectedVersion < 0 ||
            string.IsNullOrWhiteSpace(request.ReviewReason) ||
            request.ReviewReason.Trim().Length < 3 ||
            request.ReviewReason.Trim().Length > 1000 ||
            request.ReviewReason.Any(char.IsControl) ||
            request.Entries is null ||
            request.Entries.Count == 0 ||
            request.Entries.Count > 500)
        {
            return Fail(
                "invalid_catalogue",
                "Invalid catalogue publication request.");
        }

        try
        {
            var entries = NormalizeEntries(request.Entries);

            RuleBasedMatchingEngine.BuildIndex(entries);

            var json = JsonSerializer.Serialize(
                entries,
                JsonOptions);

            if (Encoding.UTF8.GetByteCount(json) > 262144)
            {
                return Fail(
                    "invalid_catalogue",
                    "Catalogue exceeds the allowed size.");
            }

            var hash = Hash(json);

            await using var transaction =
                await _db.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);

            var current = await _db.SkillCatalogueVersions
                .AsNoTracking()
                .OrderByDescending(x => x.Version)
                .FirstOrDefaultAsync(cancellationToken);

            if ((current?.Version ?? 0) !=
                request.ExpectedVersion)
            {
                return Fail(
                    "catalogue_version_conflict",
                    "Catalogue changed. Refresh and retry.");
            }

            if (current is not null &&
                !string.Equals(
                    Hash(current.CatalogueJson),
                    current.ContentSha256Hash,
                    StringComparison.OrdinalIgnoreCase))
            {
                return Fail(
                    "catalogue_integrity_error",
                    "Current catalogue integrity check failed.");
            }

            if (current is not null &&
                string.Equals(
                    current.ContentSha256Hash,
                    hash,
                    StringComparison.OrdinalIgnoreCase))
            {
                return Fail(
                    "catalogue_no_change",
                    "Catalogue content is unchanged.");
            }

            var version = new SkillCatalogueVersion
            {
                Version = checked(
                    (current?.Version ?? 0) + 1),
                PreviousVersionId = current?.Id,
                CatalogueJson = json,
                ContentSha256Hash = hash,
                ReviewReason = request.ReviewReason.Trim(),
                PublishedByUserId = actorUserId,
                PublishedAtUtc = DateTime.UtcNow
            };

            _db.SkillCatalogueVersions.Add(version);

            _audit.StageEvent(new AuditWriteRequest
            {
                ActorUserId = actorUserId,
                ActorRole = AppRoles.HRManager,
                Module = "Recruitment",
                Action = "SkillCataloguePublished",
                EntityType = "SkillCatalogueVersion",
                EntityId = version.Id,
                Outcome = "success"
            });

            await _db.SaveChangesAsync(cancellationToken);

            var versionIndex =
                await LoadVersionIndexAsync(
                    cancellationToken);

            var response = Map(
                version,
                true,
                versionIndex);

            await transaction.CommitAsync(cancellationToken);

            return new(
                true,
                "success",
                "Skill catalogue version published.",
                response);
        }
        catch (ArgumentException)
        {
            _db.ChangeTracker.Clear();

            return Fail(
                "invalid_catalogue",
                "Catalogue contains invalid or ambiguous entries.");
        }
        catch (JsonException)
        {
            _db.ChangeTracker.Clear();

            return Fail(
                "invalid_catalogue",
                "Catalogue data is invalid.");
        }
        catch (DbUpdateException)
        {
            _db.ChangeTracker.Clear();

            return Fail(
                "catalogue_version_conflict",
                "Catalogue could not be saved. Retry.");
        }
        catch (DbException)
        {
            _db.ChangeTracker.Clear();

            return Fail(
                "catalogue_database_error",
                "Catalogue database operation failed.");
        }
    }

    private static string NormalizeLabel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Any(char.IsControl))
        {
            throw new ArgumentException("Invalid skill label.");
        }

        var normalized = value
            .Normalize(NormalizationForm.FormKC)
            .Trim();

        if (normalized.Length == 0 ||
            normalized.Length > 100)
        {
            throw new ArgumentException("Invalid skill length.");
        }

        return normalized;
    }

    private static IReadOnlyList<SkillCatalogueEntry>
        NormalizeEntries(
            IReadOnlyList<SkillCatalogueEntry> entries)
    {
        var result = new List<SkillCatalogueEntry>();

        foreach (var entry in entries)
        {
            if (entry is null ||
                entry.Synonyms is null ||
                entry.Synonyms.Count > 50)
            {
                throw new ArgumentException(
                    "Invalid catalogue entry.");
            }

            var canonical =
                NormalizeLabel(entry.CanonicalName);

            var synonyms = entry.Synonyms
                .Select(NormalizeLabel)
                .OrderBy(x => x, StringComparer.Ordinal)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(x => !string.Equals(
                    x,
                    canonical,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();

            result.Add(
                new SkillCatalogueEntry(
                    canonical,
                    synonyms));
        }

        return result
            .OrderBy(
                x => x.CanonicalName,
                StringComparer.OrdinalIgnoreCase)
            .ThenBy(
                x => x.CanonicalName,
                StringComparer.Ordinal)
            .ToList();
    }

    private async Task<
        IReadOnlyList<SkillCatalogueVersionSummary>>
        LoadVersionIndexAsync(
            CancellationToken cancellationToken)
    {
        var versions = await _db.SkillCatalogueVersions
            .AsNoTracking()
            .OrderByDescending(x => x.Version)
            .Select(x => new
            {
                x.Id,
                x.Version,
                x.PreviousVersionId,
                x.ContentSha256Hash,
                x.PublishedByUserId,
                x.PublishedAtUtc
            })
            .ToListAsync(cancellationToken);

        return versions.Select(x =>
            new SkillCatalogueVersionSummary
            {
                Id = x.Id,
                Version = x.Version,
                PreviousVersionId = x.PreviousVersionId,
                ContentSha256Hash = x.ContentSha256Hash,
                PublishedByUserId = x.PublishedByUserId,
                PublishedAtUtc = DateTime.SpecifyKind(
                    x.PublishedAtUtc,
                    DateTimeKind.Utc)
            }).ToList();
    }

    private static SkillCatalogueResponse Map(
        SkillCatalogueVersion version,
        bool canPublish,
        IReadOnlyList<SkillCatalogueVersionSummary>
            versionIndex)
    {
        if (!string.Equals(
            Hash(version.CatalogueJson),
            version.ContentSha256Hash,
            StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Catalogue integrity check failed.");
        }

        var entries = JsonSerializer.Deserialize<
            List<SkillCatalogueEntry>>(
                version.CatalogueJson,
                JsonOptions)
            ?? throw new JsonException(
                "Catalogue entries missing.");

        RuleBasedMatchingEngine.BuildIndex(entries);

        return new SkillCatalogueResponse
        {
            Id = version.Id,
            Version = version.Version,
            PreviousVersionId = version.PreviousVersionId,
            ContentSha256Hash = version.ContentSha256Hash,
            ReviewReason = version.ReviewReason,
            PublishedByUserId = version.PublishedByUserId,
            PublishedAtUtc = DateTime.SpecifyKind(
                version.PublishedAtUtc,
                DateTimeKind.Utc),
            Entries = entries,
            VersionIndex = versionIndex,
            CanPublish = canPublish
        };
    }
}
