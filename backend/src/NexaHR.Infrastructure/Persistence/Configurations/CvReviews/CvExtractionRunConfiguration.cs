using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexaHR.Domain.Entities;

namespace NexaHR.Infrastructure.Persistence.Configurations.CvReviews;

public sealed class CvExtractionRunConfiguration
    : IEntityTypeConfiguration<CvExtractionRun>
{
    public void Configure(
        EntityTypeBuilder<CvExtractionRun> builder)
    {
        builder.ToTable("CvExtractionRuns", table =>
        {
            table.HasCheckConstraint(
                "CK_CvExtractionRun_Version",
                "[Version] > 0");

            table.HasCheckConstraint(
                "CK_CvExtractionRun_Status",
                "[Status] IN ('Queued','Processing'," +
                "'ReadyForReview','Failed')");

            table.HasCheckConstraint(
                "CK_CvExtractionRun_SourceHash",
                "LEN([SourceSha256Hash]) = 64");

            table.HasCheckConstraint(
                "CK_CvExtractionRun_ParserVersion",
                "LEN(LTRIM(RTRIM([ParserVersion]))) > 0");

            table.HasCheckConstraint(
                "CK_CvExtractionRun_TimestampOrder",
                "([StartedAtUtc] IS NULL OR " +
                "[StartedAtUtc] >= [CreatedAtUtc]) AND " +
                "([CompletedAtUtc] IS NULL OR " +
                "([StartedAtUtc] IS NOT NULL AND " +
                "[CompletedAtUtc] >= [StartedAtUtc]))");

            table.HasCheckConstraint(
                "CK_CvExtractionRun_ResultJson",
                "[ResultJson] IS NULL OR " +
                "ISJSON([ResultJson]) = 1");

            table.HasCheckConstraint(
                "CK_CvExtractionRun_EvidenceJson",
                "[EvidenceJson] IS NULL OR " +
                "ISJSON([EvidenceJson]) = 1");

            table.HasCheckConstraint(
                "CK_CvExtractionRun_Lifecycle",
                "(" +
                "([Status] = 'Queued' AND " +
                "[StartedAtUtc] IS NULL AND " +
                "[CompletedAtUtc] IS NULL AND " +
                "[ResultJson] IS NULL AND " +
                "[EvidenceJson] IS NULL AND " +
                "[FailureCode] IS NULL)" +

                " OR " +

                "([Status] = 'Processing' AND " +
                "[StartedAtUtc] IS NOT NULL AND " +
                "[CompletedAtUtc] IS NULL AND " +
                "[ResultJson] IS NULL AND " +
                "[EvidenceJson] IS NULL AND " +
                "[FailureCode] IS NULL)" +

                " OR " +

                "([Status] = 'ReadyForReview' AND " +
                "[StartedAtUtc] IS NOT NULL AND " +
                "[CompletedAtUtc] IS NOT NULL AND " +
                "[ResultJson] IS NOT NULL AND " +
                "[FailureCode] IS NULL)" +

                " OR " +

                "([Status] = 'Failed' AND " +
                "[StartedAtUtc] IS NOT NULL AND " +
                "[CompletedAtUtc] IS NOT NULL AND " +
                "[ResultJson] IS NULL AND " +
                "[EvidenceJson] IS NULL AND " +
                "[FailureCode] IS NOT NULL AND " +
                "LEN(LTRIM(RTRIM([FailureCode]))) > 0)" +
                ")");
        });

        builder.HasKey(e => e.Id);

        builder.Property(e => e.SourceSha256Hash)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(e => e.ParserVersion)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.Status)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(e => e.ResultJson)
            .HasColumnType("nvarchar(max)");

        builder.Property(e => e.EvidenceJson)
            .HasColumnType("nvarchar(max)");

        builder.Property(e => e.FailureCode)
            .HasMaxLength(100);

        builder.Property(e => e.CreatedAtUtc)
            .IsRequired();

        builder.Property(e => e.RowVersion)
            .IsRowVersion();

        builder.HasIndex(e => new
        {
            e.ApplicationId,
            e.Version
        }).IsUnique();

        builder.HasIndex(e => new
        {
            e.Status,
            e.CreatedAtUtc
        });

        builder.HasAlternateKey(e => new
        {
            e.ApplicationId,
            e.DocumentId,
            e.Id
        });

        builder.HasOne<CandidateApplication>()
            .WithMany()
            .HasForeignKey(e => new
            {
                e.ApplicationId,
                e.DocumentId
            })
            .HasPrincipalKey(a => new
            {
                a.Id,
                a.DocumentId
            })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<CvDocument>()
            .WithMany()
            .HasForeignKey(e => new
            {
                e.DocumentId,
                e.SourceSha256Hash
            })
            .HasPrincipalKey(d => new
            {
                d.Id,
                d.Sha256Hash
            })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
