using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexaHR.Domain.Entities;
using NexaHR.Infrastructure.Identity;

namespace NexaHR.Infrastructure.Persistence.Configurations.CvMatching;

public sealed class CvMatchRunConfiguration
    : IEntityTypeConfiguration<CvMatchRun>
{
    public void Configure(
        EntityTypeBuilder<CvMatchRun> builder)
    {
        builder.ToTable("CvMatchRuns", table =>
        {
            table.HasCheckConstraint(
                "CK_CvMatchRun_Version",
                "[Version] > 0");

            table.HasCheckConstraint(
                "CK_CvMatchRun_Weights",
                "[MatchedWeight] >= 0 AND " +
                "[TotalWeight] >= 0 AND " +
                "[MatchedWeight] <= [TotalWeight]");

            table.HasCheckConstraint(
                "CK_CvMatchRun_Score",
                "([TotalWeight] = 0 AND [Score] IS NULL) OR " +
                "([TotalWeight] > 0 AND [Score] IS NOT NULL " +
                "AND [Score] >= 0 AND [Score] <= 100)");

            table.HasCheckConstraint(
                "CK_CvMatchRun_SourceHash",
                "LEN([SourceSha256Hash]) = 64");

            table.HasCheckConstraint(
                "CK_CvMatchRun_InputHash",
                "LEN([InputSha256Hash]) = 64");

            table.HasCheckConstraint(
                "CK_CvMatchRun_RuleVersion",
                "LEN(LTRIM(RTRIM([RuleVersion]))) > 0");

            table.HasCheckConstraint(
                "CK_CvMatchRun_CriteriaJson",
                "ISJSON([CriteriaSnapshotJson], ARRAY) = 1");

            table.HasCheckConstraint(
                "CK_CvMatchRun_EvidenceJson",
                "ISJSON([EvidenceJson], ARRAY) = 1");

            table.HasCheckConstraint(
                "CK_CvMatchRun_ExperienceJson",
                "ISJSON([ExperienceAssessmentJson], OBJECT) = 1");
        });

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => new
        {
            x.ApplicationId,
            x.Version
        }).IsUnique();

        builder.HasIndex(x => x.CvReviewVersionId);

        builder.HasIndex(x => x.SkillCatalogueVersionId);

        builder.HasIndex(x => x.CreatedAtUtc);

        builder.Property(x => x.RuleVersion)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.SourceSha256Hash)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.InputSha256Hash)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.MatchedWeight)
            .HasPrecision(12, 2);

        builder.Property(x => x.TotalWeight)
            .HasPrecision(12, 2);

        builder.Property(x => x.Score)
            .HasPrecision(5, 2);

        builder.Property(x => x.CriteriaSnapshotJson)
            .IsRequired();

        builder.Property(x => x.EvidenceJson)
            .IsRequired();

        builder.Property(x => x.ExperienceAssessmentJson)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.RowVersion)
            .IsRowVersion();

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

        builder.HasOne<VacancyRevision>()
            .WithMany()
            .HasForeignKey(x => x.VacancyRevisionId)
            .OnDelete(DeleteBehavior.Restrict);

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

        builder.HasOne<SkillCatalogueVersion>()
            .WithMany()
            .HasForeignKey(x => x.SkillCatalogueVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
