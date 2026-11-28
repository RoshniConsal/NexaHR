using System.Data;
using Microsoft.EntityFrameworkCore;
using NexaHR.Application.DTOs.Audit;
using NexaHR.Application.Interfaces.Audit;
using NexaHR.Domain.Constants;
using NexaHR.Infrastructure.Persistence;

namespace NexaHR.Infrastructure.Services.CvAiAnalysis;

public sealed class CvAiAnalysisRecoveryService
{
    private readonly NexaHRDbContext _db;
    private readonly IAuditWriter _audit;

    // Conservative recovery threshold.
    // Must exceed the bounded inference and finalization
    // duration of the normal processor.
    public static readonly TimeSpan RecoveryThreshold =
        TimeSpan.FromMinutes(15);

    public CvAiAnalysisRecoveryService(
        NexaHRDbContext db,
        IAuditWriter audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<int> RecoverInterruptedAsync(
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var cutoff = now.Subtract(RecoveryThreshold);

        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        var interrupted = await _db.CvAiAnalysisRuns
            .Where(x =>
                x.Status == CvAiAnalysisStatuses.Processing &&
                x.StartedAtUtc.HasValue &&
                x.StartedAtUtc.Value <= cutoff)
            .OrderBy(x => x.StartedAtUtc)
            .ThenBy(x => x.Id)
            .Take(20)
            .ToListAsync(cancellationToken);

        if (interrupted.Count == 0)
        {
            await transaction.CommitAsync(
                cancellationToken);

            return 0;
        }

        foreach (var run in interrupted)
        {
            run.Status = CvAiAnalysisStatuses.Failed;
            run.CompletedAtUtc = now;
            run.ResultJson = null;
            run.FailureCode = "ai_processing_interrupted";

            // Preserve the original attempt count.
            // Never automatically redispatch inference.

            _audit.StageEvent(new AuditWriteRequest
            {
                ActorRole = "System",
                Module = "Recruitment",
                EntityType = "CvAiAnalysisRun",
                EntityId = run.Id,
                CorrelationId = run.Id,
                Action = "CvAiAnalysisFailed",
                Outcome = "failure",
                ReasonCode = run.FailureCode
            });
        }

        try
        {
            await _db.SaveChangesAsync(
                cancellationToken);

            await transaction.CommitAsync(
                cancellationToken);

            return interrupted.Count;
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(
                cancellationToken);

            _db.ChangeTracker.Clear();

            return 0;
        }
    }
}
