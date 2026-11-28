using System.Security.Cryptography;
using NexaHR.Application.Interfaces.CvDocuments;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NexaHR.Application.Interfaces.CvMatching;
using NexaHR.Application.Interfaces.Audit;
using NexaHR.Domain.Constants;
using NexaHR.Domain.Entities;
using NexaHR.Infrastructure.Identity;
using NexaHR.Infrastructure.Persistence;

namespace NexaHR.Infrastructure.Services.CvMatching;

public sealed partial class CvMatchingService
{
    private readonly NexaHRDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IAuditWriter _audit;
    private readonly ICvFileStorage _storage;

    public CvMatchingService(
        NexaHRDbContext db,
        UserManager<ApplicationUser> users,
        IAuditWriter audit,
        ICvFileStorage storage)
    {
        _db = db;
        _users = users;
        _audit = audit;
        _storage = storage;
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

    private async Task<bool> VerifyDocumentIntegrityAsync(
        CvDocument document,
        CancellationToken cancellationToken)
    {
        const long maxDocumentBytes = 5L * 1024 * 1024;

        if (document.SizeBytes <= 0 ||
            document.SizeBytes > maxDocumentBytes)
        {
            return false;
        }

        try
        {
            await using var source =
                _storage.OpenCleanRead(document.StorageKey);

            if (!source.CanRead ||
                !source.CanSeek ||
                source.Length != document.SizeBytes ||
                source.Length > maxDocumentBytes)
            {
                return false;
            }

            using var hash = IncrementalHash.CreateHash(
                HashAlgorithmName.SHA256);

            var buffer = new byte[81920];
            long totalBytes = 0;

            while (true)
            {
                var count = await source.ReadAsync(
                    buffer.AsMemory(),
                    cancellationToken);

                if (count == 0)
                    break;

                totalBytes += count;

                if (totalBytes > maxDocumentBytes ||
                    totalBytes > document.SizeBytes)
                {
                    return false;
                }

                hash.AppendData(buffer, 0, count);
            }

            if (totalBytes != document.SizeBytes)
                return false;

            var actualHash = Convert.ToHexString(
                hash.GetHashAndReset());

            return string.Equals(
                actualHash,
                document.Sha256Hash,
                StringComparison.OrdinalIgnoreCase);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    private sealed record MatchingSource(
        CandidateApplication Application,
        CvDocument Document,
        CvReviewVersion Review,
        VacancyRevision Revision,
        SkillCatalogueVersion Catalogue,
        IReadOnlyList<VacancyCriterion> Criteria);

    private async Task<(MatchingSource? Source, string Code)>
        LoadSourceAsync(
            Guid applicationId,
            CancellationToken cancellationToken)
    {
        var application = await _db
            .Set<CandidateApplication>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == applicationId,
                cancellationToken);

        if (application is null)
            return (null, "application_not_found");

        var document = await _db
            .Set<CvDocument>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == application.DocumentId,
                cancellationToken);

        if (document is null ||
            document.OwnerUserId != application.CandidateUserId ||
            document.ScanStatus != "Clean" ||
            document.ScannedAtUtc is null ||
            document.Sha256Hash.Length != 64 ||
            !document.Sha256Hash.All(Uri.IsHexDigit))
        {
            return (null, "document_not_available");
        }

        if (!await VerifyDocumentIntegrityAsync(
                document,
                cancellationToken))
        {
            return (null, "document_not_available");
        }

        var review = await _db
            .Set<CvReviewVersion>()
            .AsNoTracking()
            .Where(x => x.ApplicationId == applicationId)
            .OrderByDescending(x => x.Version)
            .FirstOrDefaultAsync(cancellationToken);

        if (review is null ||
            review.Status != CvReviewStatuses.Confirmed ||
            review.ConfirmedAtUtc is null ||
            review.DocumentId != document.Id)
        {
            return (null, "confirmed_review_required");
        }

        // Match against the exact confirmed review revision.
        // API-083 may revalidate against another approved
        // revision of the same vacancy without changing
        // the original application revision.
        var revision = await _db
            .Set<VacancyRevision>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == review.VacancyRevisionId,
                cancellationToken);

        if (revision is null ||
            revision.VacancyId != application.VacancyId ||
            revision.SubmittedAtUtc is null ||
            revision.SubmittedByUserId is null)
        {
            return (null, "revision_not_approved");
        }

        var decisions = await _db
            .Set<VacancyDecision>()
            .AsNoTracking()
            .Where(x => x.VacancyRevisionId == revision.Id)
            .ToListAsync(cancellationToken);

        if (decisions.Count != 1)
            return (null, "revision_not_approved");

        var decision = decisions[0];

        if (decision.Decision != VacancyDecisionTypes.Approved ||
            decision.ApproverRole != AppRoles.CEO ||
            decision.AtUtc < revision.SubmittedAtUtc.Value)
        {
            return (null, "revision_not_approved");
        }

        var catalogue = await _db
            .SkillCatalogueVersions
            .AsNoTracking()
            .OrderByDescending(x => x.Version)
            .FirstOrDefaultAsync(cancellationToken);

        if (catalogue is null)
            return (null, "skill_catalogue_required");

        var criteria = await _db
            .Set<VacancyCriterion>()
            .AsNoTracking()
            .Where(x => x.VacancyRevisionId == revision.Id)
            .ToListAsync(cancellationToken);

        return (
            new MatchingSource(
                application,
                document,
                review,
                revision,
                catalogue,
                criteria),
            "success");
    }
}
