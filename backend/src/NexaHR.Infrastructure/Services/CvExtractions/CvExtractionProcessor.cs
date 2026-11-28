using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NexaHR.Application.Interfaces.CvDocuments;
using NexaHR.Domain.Constants;
using NexaHR.Infrastructure.Persistence;

namespace NexaHR.Infrastructure.Services.CvExtractions;

public sealed class CvExtractionProcessor
{
    private readonly NexaHRDbContext _db;
    private readonly ICvFileStorage _storage;

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public CvExtractionProcessor(
        NexaHRDbContext db,
        ICvFileStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    public async Task<bool> ProcessNextAsync(
        CancellationToken cancellationToken)
    {
        var run = await _db.CvExtractionRuns
            .Where(r =>
                r.Status == CvExtractionStatuses.Queued)
            .OrderBy(r => r.CreatedAtUtc)
            .ThenBy(r => r.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (run is null)
            return false;

        run.Status = CvExtractionStatuses.Processing;
        run.StartedAtUtc = DateTime.UtcNow;

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            _db.ChangeTracker.Clear();
            return false;
        }

        string? resultJson = null;
        string? evidenceJson = null;
        string? failureCode = null;

        try
        {
            var application = await _db.CandidateApplications
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    a => a.Id == run.ApplicationId &&
                         a.DocumentId == run.DocumentId,
                    cancellationToken);

            if (application is null)
            {
                failureCode = "application_source_invalid";
            }
            else
            {
                var document = await _db.CvDocuments
                    .AsNoTracking()
                    .SingleOrDefaultAsync(
                        d => d.Id == run.DocumentId &&
                             d.OwnerUserId ==
                                 application.CandidateUserId,
                        cancellationToken);

                if (document is null ||
                    document.ScanStatus != CvScanStatuses.Clean ||
                    !document.ScannedAtUtc.HasValue)
                {
                    failureCode = "document_not_available";
                }
                else if (!string.Equals(
                    document.Sha256Hash,
                    run.SourceSha256Hash,
                    StringComparison.OrdinalIgnoreCase))
                {
                    failureCode = "source_hash_mismatch";
                }
                else if (run.ParserVersion !=
                         CvDocumentTextParser.ParserVersion &&
                         run.ParserVersion !=
                         CvStructuredFieldExtractorV2.ParserVersion)
                {
                    failureCode = "parser_version_mismatch";
                }
                else
                {
                    await using var source =
                        _storage.OpenCleanRead(document.StorageKey);

                    var actualHash = Convert.ToHexString(
                        await SHA256.HashDataAsync(
                            source, cancellationToken));

                    if (!string.Equals(
                            actualHash,
                            run.SourceSha256Hash,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        failureCode = "source_hash_mismatch";
                    }
                    else
                    {
                        source.Position = 0;

                        var parser =
                            new CvDocumentTextParser();

                        var segments = await parser.ParseAsync(
                            source,
                            document.ContentType,
                            cancellationToken);

                        var extracted = run.ParserVersion switch
                        {
                            CvDocumentTextParser.ParserVersion =>
                                new CvStructuredFieldExtractor()
                                    .Extract(
                                        segments,
                                        cancellationToken),

                            CvStructuredFieldExtractorV2.ParserVersion =>
                                new CvStructuredFieldExtractorV2()
                                    .Extract(
                                        segments,
                                        cancellationToken),

                            _ => throw new InvalidDataException(
                                "Unsupported parser version.")
                        };

                        resultJson = JsonSerializer.Serialize(
                            extracted.Values,
                            JsonOptions);

                        evidenceJson = JsonSerializer.Serialize(
                            extracted.Evidence,
                            JsonOptions);
                    }
                }
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            failureCode = "processing_interrupted";
        }
        catch (OperationCanceledException)
        {
            failureCode = "extraction_timeout";
        }
        catch (InvalidDataException)
        {
            failureCode = "document_extraction_failed";
        }
        catch (IOException)
        {
            failureCode = "document_read_failed";
        }
        catch (Exception)
        {
            failureCode = "extraction_failed";
        }

        run.CompletedAtUtc = DateTime.UtcNow;

        if (failureCode is null && resultJson is not null)
        {
            run.Status = CvExtractionStatuses.ReadyForReview;
            run.ResultJson = resultJson;
            run.EvidenceJson = evidenceJson;
            run.FailureCode = null;
        }
        else
        {
            run.Status = CvExtractionStatuses.Failed;
            run.ResultJson = null;
            run.EvidenceJson = null;
            run.FailureCode =
                failureCode ?? "extraction_failed";
        }

        using var finalSaveTimeout =
            new CancellationTokenSource(
                TimeSpan.FromSeconds(15));

        await _db.SaveChangesAsync(finalSaveTimeout.Token);

        return true;
    }
}
