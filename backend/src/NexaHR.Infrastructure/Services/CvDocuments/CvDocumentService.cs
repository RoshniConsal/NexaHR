using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NexaHR.Application.DTOs.Audit;
using NexaHR.Application.DTOs.CvDocuments;
using NexaHR.Application.Interfaces.Audit;
using NexaHR.Application.Interfaces.CvDocuments;
using NexaHR.Domain.Constants;
using NexaHR.Domain.Entities;
using NexaHR.Infrastructure.Identity;
using NexaHR.Infrastructure.Persistence;

namespace NexaHR.Infrastructure.Services.CvDocuments;

public sealed class CvDocumentService
    : ICvDocumentService
{
    private readonly NexaHRDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly ICvFileStorage _storage;
    private readonly ICvFileValidator _validator;
    private readonly ICvMalwareScanner _scanner;
    private readonly IAuditWriter _audit;

    private static readonly SemaphoreSlim ScanGate =
        new(1, 1);

    public CvDocumentService(
        NexaHRDbContext db,
        UserManager<ApplicationUser> users,
        ICvFileStorage storage,
        ICvFileValidator validator,
        ICvMalwareScanner scanner,
        IAuditWriter audit)
    {
        _db = db;
        _users = users;
        _storage = storage;
        _validator = validator;
        _scanner = scanner;
        _audit = audit;
    }

    // API-057: Candidate CV upload.
    public async Task<CvDocumentOperationResponse> UploadAsync(
        Guid actorUserId,
        string originalFileName,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        if (!await IsCandidateAsync(actorUserId))
        {
            return Failure(
                "forbidden",
                "Candidate access is required.");
        }

        if (content is null || !content.CanRead)
        {
            return Failure(
                "invalid_file",
                "A readable CV file is required.");
        }

        if (!await ScanGate.WaitAsync(
                TimeSpan.Zero,
                cancellationToken))
        {
            return Failure(
                "scanner_busy",
                "CV scanning is busy. Please try again.");
        }

        var createdAtUtc = DateTime.UtcNow;

        CvStoredFile? storedFile = null;

        var commitAttempted = false;

        try
        {
            storedFile =
                await _storage.SaveToQuarantineAsync(
                    content,
                    cancellationToken);

            var path = _storage.GetQuarantinePath(
                storedFile.StorageKey);

            var validation = _validator.Validate(
                path, originalFileName);

            if (!validation.IsValid)
            {
                return Failure(
                    validation.Code,
                    "CV file validation failed.");
            }

            var scan = await _scanner.ScanAsync(
                path,
                cancellationToken);

            var scannedAtUtc = DateTime.UtcNow;

            var scanStatus = scan.Status switch
            {
                CvScanStatuses.Clean =>
                    CvScanStatuses.Clean,

                CvScanStatuses.Infected =>
                    CvScanStatuses.Infected,

                _ => CvScanStatuses.Failed
            };

            var scanResultCode = scanStatus switch
            {
                CvScanStatuses.Clean => "scan_clean",
                CvScanStatuses.Infected => "malware_detected",
                _ => "scan_failed"
            };

            if (scanStatus == CvScanStatuses.Clean)
            {
                _storage.PromoteToClean(
                    storedFile.StorageKey);
            }

            await using var transaction =
                await _db.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);

            if (!await IsCandidateAsync(actorUserId))
            {
                return Failure(
                    "forbidden",
                    "Candidate access is required.");
            }

            var latestVersion = await _db.CvDocuments
                .Where(x => x.OwnerUserId == actorUserId)
                .Select(x => (int?)x.Version)
                .MaxAsync(cancellationToken) ?? 0;

            var document = new CvDocument
            {
                OwnerUserId = actorUserId,

                Version = checked(latestVersion + 1),

                OriginalFileName =
                    validation.SafeFileName,

                StorageKey =
                    storedFile.StorageKey,

                ContentType =
                    validation.ContentType,

                SizeBytes =
                    storedFile.SizeBytes,

                Sha256Hash =
                    storedFile.Sha256Hash,

                ScanStatus = scanStatus,

                ScanResultCode = scanResultCode,

                CreatedAtUtc = createdAtUtc,

                ScannedAtUtc = scannedAtUtc
            };

            _db.CvDocuments.Add(document);

            _audit.StageEvent(new AuditWriteRequest
            {
                ActorUserId = actorUserId,

                ActorRole = AppRoles.Candidate,

                Module = "Recruitment",

                Action = "CvDocumentUploaded",

                EntityType = "CvDocument",

                EntityId = document.Id,

                Outcome = "success"
            });

            await _db.SaveChangesAsync(
                cancellationToken);

            commitAttempted = true;

            await transaction.CommitAsync(
                CancellationToken.None);

            return new CvDocumentOperationResponse
            {
                Success = true,

                Code = scanResultCode,

                Message = scanStatus switch
                {
                    CvScanStatuses.Clean =>
                        "CV uploaded successfully.",

                    CvScanStatuses.Infected =>
                        "CV recorded but blocked by malware scanning.",

                    _ =>
                        "CV recorded but access is blocked because scanning failed."
                },

                Document = ToResponse(document)
            };
        }
        catch (InvalidDataException)
        {
            return Failure(
                "invalid_file_size_or_content",
                "CV must be non-empty and no larger than 5 MB.");
        }
        catch (DbUpdateException)
        {
            return Failure(
                "cv_save_failed",
                "CV metadata could not be saved.");
        }
        catch (Microsoft.Data.SqlClient.SqlException)
        {
            return Failure(
                "cv_database_error",
                "CV database operation failed.");
        }
        catch (IOException)
        {
            return Failure(
                "cv_storage_error",
                "CV storage operation failed.");
        }
        catch (UnauthorizedAccessException)
        {
            return Failure(
                "cv_storage_error",
                "CV storage operation failed.");
        }
        finally
        {
            try
            {
                if (!commitAttempted &&
                    storedFile is not null)
                {
                    try
                    {
                        _storage.Delete(
                            storedFile.StorageKey);
                    }
                    catch (IOException)
                    {
                        // Requires orphan reconciliation.
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Requires orphan reconciliation.
                    }
                }
            }
            finally
            {
                ScanGate.Release();
            }
        }
    }

    // API-058: List own CV document versions.
    public async Task<CvDocumentListResponse?> GetListAsync(
        Guid actorUserId,
        CvDocumentQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!await IsCandidateAsync(actorUserId))
            return null;

        if (query.OwnerUserId.HasValue &&
            query.OwnerUserId.Value != actorUserId)
        {
            return null;
        }

        var page = Math.Clamp(query.Page, 1, 100000);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var documents = _db.CvDocuments
            .AsNoTracking()
            .Where(x => x.OwnerUserId == actorUserId);

        var totalCount = await documents
            .CountAsync(cancellationToken);

        var items = await documents
            .OrderByDescending(x => x.Version)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new CvDocumentListResponse
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            Items = items.Select(ToResponse).ToList()
        };
    }

    // API-059: Get own CV document metadata.
    public async Task<CvDocumentResponse?> GetDetailsAsync(
        Guid actorUserId,
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        if (documentId == Guid.Empty ||
            !await IsCandidateAsync(actorUserId))
        {
            return null;
        }

        var document = await _db.CvDocuments
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == documentId &&
                     x.OwnerUserId == actorUserId,
                cancellationToken);

        return document is null
            ? null
            : ToResponse(document);
    }

    // API-060: Read authorized clean CV content.
    public async Task<CvDocumentContentResponse> GetContentAsync(
        Guid actorUserId,
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        if (documentId == Guid.Empty ||
            !await IsCandidateAsync(actorUserId))
        {
            return ContentFailure("cv_not_found");
        }

        var document = await _db.CvDocuments
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == documentId &&
                     x.OwnerUserId == actorUserId,
                cancellationToken);

        if (document is null)
            return ContentFailure("cv_not_found");

        if (document.ScanStatus != CvScanStatuses.Clean)
            return ContentFailure("cv_access_blocked");

        Stream? content = null;

        try
        {
            content = _storage.OpenCleanRead(
                document.StorageKey);

            if (content.Length != document.SizeBytes)
                return ContentFailure("cv_integrity_failed");

            var hash = await
                System.Security.Cryptography.SHA256
                    .HashDataAsync(
                        content,
                        cancellationToken);

            if (!string.Equals(
                    Convert.ToHexString(hash),
                    document.Sha256Hash,
                    StringComparison.OrdinalIgnoreCase))
            {
                return ContentFailure("cv_integrity_failed");
            }

            content.Position = 0;

            // Recheck authorization before granting access.
            if (!await IsCandidateAsync(actorUserId))
            {
                return ContentFailure("cv_not_found");
            }

            // Record authorized, integrity-verified access
            // before transferring the content stream.
            _audit.StageEvent(new AuditWriteRequest
            {
                ActorUserId = actorUserId,
                ActorRole = AppRoles.Candidate,
                Module = "Recruitment",
                Action = "CvDocumentContentAccessGranted",
                EntityType = "CvDocument",
                EntityId = document.Id,
                Outcome = "success"
            });

            await _db.SaveChangesAsync(
                cancellationToken);

            var response = new CvDocumentContentResponse
            {
                Success = true,
                Code = "cv_content_available",
                Content = content,
                ContentType = document.ContentType,
                FileName = document.OriginalFileName
            };

            // Transfer stream ownership to the caller.
            content = null;

            return response;
        }
        catch (DbUpdateException)
        {
            return ContentFailure("cv_content_unavailable");
        }
        catch (Microsoft.Data.SqlClient.SqlException)
        {
            return ContentFailure("cv_content_unavailable");
        }
        catch (IOException)
        {
            return ContentFailure("cv_content_unavailable");
        }
        catch (UnauthorizedAccessException)
        {
            return ContentFailure("cv_content_unavailable");
        }
        catch (ArgumentException)
        {
            return ContentFailure("cv_content_unavailable");
        }
        finally
        {
            if (content is not null)
                await content.DisposeAsync();
        }
    }

    private static CvDocumentContentResponse ContentFailure(
        string code)
    {
        return new CvDocumentContentResponse
        {
            Success = false,
            Code = code
        };
    }

    private async Task<bool> IsCandidateAsync(
        Guid actorUserId)
    {
        if (actorUserId == Guid.Empty)
            return false;

        var user = await _db.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == actorUserId);

        if (user is null ||
            !user.IsActive ||
            !user.EmailConfirmed ||
            await _users.IsLockedOutAsync(user))
        {
            return false;
        }

        var roles = await _users.GetRolesAsync(user);

        return roles.Count == 1 &&
               roles.Contains(AppRoles.Candidate);
    }

    private static CvDocumentResponse ToResponse(
        CvDocument document)
    {
        return new CvDocumentResponse
        {
            DocumentId = document.Id,

            Version = document.Version,

            OriginalFileName =
                document.OriginalFileName,

            ContentType = document.ContentType,

            SizeBytes = document.SizeBytes,

            ScanStatus = document.ScanStatus,

            CreatedAtUtc = DateTime.SpecifyKind(
                document.CreatedAtUtc,
                DateTimeKind.Utc),

            ScannedAtUtc =
                document.ScannedAtUtc.HasValue
                    ? DateTime.SpecifyKind(
                        document.ScannedAtUtc.Value,
                        DateTimeKind.Utc)
                    : null,

            CanDownload =
                document.ScanStatus ==
                    CvScanStatuses.Clean
        };
    }

    private static CvDocumentOperationResponse Failure(
        string code,
        string message)
    {
        return new CvDocumentOperationResponse
        {
            Success = false,
            Code = code,
            Message = message
        };
    }
}
