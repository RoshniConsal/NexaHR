using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexaHR.Domain.Entities;
using NexaHR.Infrastructure.Identity;

namespace NexaHR.Infrastructure.Persistence.Configurations.CvAiAnalysis;

public sealed class CvAiAnalysisRunConfiguration
    : IEntityTypeConfiguration<CvAiAnalysisRun>
{
    public void Configure(
        EntityTypeBuilder<CvAiAnalysisRun> builder)
    {
        builder.ToTable("CvAiAnalysisRuns", table =>
        {
            table.HasCheckConstraint(
                "CK_CvAiAnalysisRun_Version",
                "[Version] > 0");

            table.HasCheckConstraint(
                "CK_CvAiAnalysisRun_SchemaVersions",
                "[InputSchemaVersion] > 0 AND " +
                "[OutputSchemaVersion] > 0");

            table.HasCheckConstraint(
                "CK_CvAiAnalysisRun_Status",
                "[Status] IN ('Queued','Processing'," +
                "'Ready','Failed')");

            table.HasCheckConstraint(
                "CK_CvAiAnalysisRun_Attempts",
                "[AttemptCount] >= 0 AND " +
                "[AttemptCount] <= 2");

            table.HasCheckConstraint(
                "CK_CvAiAnalysisRun_Provider",
                "LEN(LTRIM(RTRIM([Provider]))) > 0");

            table.HasCheckConstraint(
                "CK_CvAiAnalysisRun_Model",
                "LEN(LTRIM(RTRIM([Model]))) > 0");

            table.HasCheckConstraint(
                "CK_CvAiAnalysisRun_ModelDigest",
                "LEN([ModelDigest]) = 64 AND " +
                "[ModelDigest] COLLATE Latin1_General_100_BIN2 " +
                "NOT LIKE '%[^0-9A-Fa-f]%'");

            table.HasCheckConstraint(
                "CK_CvAiAnalysisRun_PromptVersion",
                "LEN(LTRIM(RTRIM([PromptVersion]))) > 0");

            table.HasCheckConstraint(
                "CK_CvAiAnalysisRun_SourceHash",
                "LEN([SourceSha256Hash]) = 64 AND " +
                "[SourceSha256Hash] COLLATE Latin1_General_100_BIN2 " +
                "NOT LIKE '%[^0-9A-Fa-f]%'");

            table.HasCheckConstraint(
                "CK_CvAiAnalysisRun_InputHash",
                "LEN([InputSha256Hash]) = 64 AND " +
                "[InputSha256Hash] COLLATE Latin1_General_100_BIN2 " +
                "NOT LIKE '%[^0-9A-Fa-f]%'");

            table.HasCheckConstraint(
                "CK_CvAiAnalysisRun_InputJson",
                "ISJSON([InputJson], OBJECT) = 1 AND " +
                "DATALENGTH([InputJson]) <= 524288");

            table.HasCheckConstraint(
                "CK_CvAiAnalysisRun_ResultJson",
                "[ResultJson] IS NULL OR " +
                "(ISJSON([ResultJson], OBJECT) = 1 AND " +
                "DATALENGTH([ResultJson]) <= 524288)");

            table.HasCheckConstraint(
                "CK_CvAiAnalysisRun_TimestampOrder",
                "([StartedAtUtc] IS NULL OR " +
                "[StartedAtUtc] >= [CreatedAtUtc]) AND " +
                "([CompletedAtUtc] IS NULL OR " +
                "([StartedAtUtc] IS NOT NULL AND " +
                "[CompletedAtUtc] >= [StartedAtUtc]))");

            table.HasCheckConstraint(
                "CK_CvAiAnalysisRun_Lifecycle",
                "(" +

                "([Status] = 'Queued' AND " +
                "[AttemptCount] = 0 AND " +
                "[StartedAtUtc] IS NULL AND " +
                "[CompletedAtUtc] IS NULL AND " +
                "[ResultJson] IS NULL AND " +
                "[FailureCode] IS NULL)" +

                " OR " +

                "([Status] = 'Processing' AND " +
                "[StartedAtUtc] IS NOT NULL AND " +
                "[CompletedAtUtc] IS NULL AND " +
                "[ResultJson] IS NULL AND " +
                "[FailureCode] IS NULL)" +

                " OR " +

                "([Status] = 'Ready' AND " +
                "[AttemptCount] BETWEEN 1 AND 2 AND " +
                "[StartedAtUtc] IS NOT NULL AND " +
                "[CompletedAtUtc] IS NOT NULL AND " +
                "[ResultJson] IS NOT NULL AND " +
                "[FailureCode] IS NULL)" +

                " OR " +

                "([Status] = 'Failed' AND " +
                "[StartedAtUtc] IS NOT NULL AND " +
                "[CompletedAtUtc] IS NOT NULL AND " +
                "[ResultJson] IS NULL AND " +
                "[FailureCode] IS NOT NULL AND " +
                "LEN(LTRIM(RTRIM([FailureCode]))) > 0)" +

                ")");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Provider)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Model)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.ModelDigest)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.PromptVersion)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.SourceSha256Hash)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.InputSha256Hash)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.InputJson)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(x => x.ResultJson)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.Status)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.FailureCode)
            .HasMaxLength(100);

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        builder.HasIndex(x => new
        {
            x.ApplicationId,
            x.Version
        }).IsUnique();

        builder.HasIndex(x => new
        {
            x.Status,
            x.CreatedAtUtc
        });

        builder.HasIndex(x => x.CvReviewVersionId);

        builder.HasIndex(x => x.CvMatchRunId);

        builder.HasIndex(x => x.ProcessingPolicyId);

        builder.HasIndex(x => x.ApplicationConsentId);

        // Preserve application and pinned document.
        builder.HasOne<CandidateApplication>()
            .WithMany()
            .HasForeignKey(x => new
            {
                x.ApplicationId,
                x.DocumentId
            })
            .HasPrincipalKey(a => new
            {
                a.Id,
                a.DocumentId
            })
            .OnDelete(DeleteBehavior.Restrict);

        // Preserve exact document and source hash.
        builder.HasOne<CvDocument>()
            .WithMany()
            .HasForeignKey(x => new
            {
                x.DocumentId,
                x.SourceSha256Hash
            })
            .HasPrincipalKey(d => new
            {
                d.Id,
                d.Sha256Hash
            })
            .OnDelete(DeleteBehavior.Restrict);

        // Preserve selected vacancy revision.
        builder.HasOne<VacancyRevision>()
            .WithMany()
            .HasForeignKey(x => x.VacancyRevisionId)
            .OnDelete(DeleteBehavior.Restrict);

        // Preserve confirmed review provenance.
        builder.HasOne<CvReviewVersion>()
            .WithMany()
            .HasForeignKey(x => new
            {
                x.ApplicationId,
                x.DocumentId,
                x.CvReviewVersionId,
                x.VacancyRevisionId
            })
            .HasPrincipalKey(r => new
            {
                r.ApplicationId,
                r.DocumentId,
                r.Id,
                r.VacancyRevisionId
            })
            .OnDelete(DeleteBehavior.Restrict);

        // Preserve matching run from the same source.
        builder.HasOne<CvMatchRun>()
            .WithMany()
            .HasForeignKey(x => new
            {
                x.ApplicationId,
                x.DocumentId,
                x.VacancyRevisionId,
                x.CvReviewVersionId,
                x.SourceSha256Hash,
                x.CvMatchRunId
            })
            .HasPrincipalKey(m => new
            {
                m.ApplicationId,
                m.DocumentId,
                m.VacancyRevisionId,
                m.CvReviewVersionId,
                m.SourceSha256Hash,
                m.Id
            })
            .OnDelete(DeleteBehavior.Restrict);

        // Preserve processing policy.
        builder.HasOne<ProcessingPolicy>()
            .WithMany()
            .HasForeignKey(x => x.ProcessingPolicyId)
            .OnDelete(DeleteBehavior.Restrict);

        // Preserve consent from the same application.
        builder.HasOne<ApplicationConsent>()
            .WithMany()
            .HasForeignKey(x => new
            {
                x.ApplicationId,
                x.ApplicationConsentId
            })
            .HasPrincipalKey(c => new
            {
                c.ApplicationId,
                c.Id
            })
            .OnDelete(DeleteBehavior.Restrict);

        // Preserve requesting HR identity.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.RequestedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
