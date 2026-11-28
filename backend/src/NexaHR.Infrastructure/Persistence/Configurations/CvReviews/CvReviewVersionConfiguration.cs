using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexaHR.Domain.Entities;
using NexaHR.Infrastructure.Identity;

namespace NexaHR.Infrastructure.Persistence.Configurations.CvReviews;

public sealed class CvReviewVersionConfiguration
    : IEntityTypeConfiguration<CvReviewVersion>
{
    public void Configure(
        EntityTypeBuilder<CvReviewVersion> builder)
    {
        builder.ToTable("CvReviewVersions", table =>
        {
            table.HasCheckConstraint(
                "CK_CvReviewVersion_Version",
                "[Version] > 0");

            table.HasCheckConstraint(
                "CK_CvReviewVersion_Status",
                "[Status] IN ('Draft','Confirmed')");

            table.HasCheckConstraint(
                "CK_CvReviewVersion_Confirmation",
                "([Status] = 'Draft' AND " +
                "[ConfirmedAtUtc] IS NULL) OR " +
                "([Status] = 'Confirmed' AND " +
                "[ConfirmedAtUtc] IS NOT NULL AND " +
                "[ConfirmedAtUtc] >= [CreatedAtUtc])");

            table.HasCheckConstraint(
                "CK_CvReviewVersion_History",
                "([Version] = 1 AND " +
                "[PreviousReviewVersionId] IS NULL) OR " +
                "([Version] > 1 AND " +
                "[PreviousReviewVersionId] IS NOT NULL AND " +
                "[PreviousReviewVersionId] <> [Id])");

            table.HasCheckConstraint(
                "CK_CvReviewVersion_ValuesJson",
                "ISJSON([ValuesJson]) = 1");

            table.HasCheckConstraint(
                "CK_CvReviewVersion_DuplicateResolutionJson",
                "ISJSON([DuplicateResolutionJson]) = 1");

            table.HasCheckConstraint(
                "CK_CvReviewVersion_ChangedFieldsJson",
                "ISJSON([ChangedFieldsJson]) = 1");
        });

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Status)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(r => r.ValuesJson)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(r => r.DuplicateResolutionJson)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(r => r.ChangedFieldsJson)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(r => r.CreatedAtUtc)
            .IsRequired();

        builder.Property(r => r.RowVersion)
            .IsRowVersion();

        builder.HasIndex(r => new
        {
            r.ApplicationId,
            r.Version
        }).IsUnique();

        builder.HasIndex(r => new
        {
            r.ApplicationId,
            r.Status,
            r.Version
        });

        builder.HasIndex(r => r.PreviousReviewVersionId)
            .IsUnique()
            .HasFilter("[PreviousReviewVersionId] IS NOT NULL");

        builder.HasAlternateKey(r => new
        {
            r.ApplicationId,
            r.DocumentId,
            r.Id
        });

        // Preserve the application and its pinned CV.
        builder.HasOne<CandidateApplication>()
            .WithMany()
            .HasForeignKey(r => new
            {
                r.ApplicationId,
                r.DocumentId
            })
            .HasPrincipalKey(a => new
            {
                a.Id,
                a.DocumentId
            })
            .OnDelete(DeleteBehavior.Restrict);

        // Optional extraction from the same application/CV.
        builder.HasOne<CvExtractionRun>()
            .WithMany()
            .HasForeignKey(r => new
            {
                r.ApplicationId,
                r.DocumentId,
                r.ExtractionRunId
            })
            .HasPrincipalKey(e => new
            {
                e.ApplicationId,
                e.DocumentId,
                e.Id
            })
            .OnDelete(DeleteBehavior.Restrict);

        // Preserve selected review criteria revision.
        builder.HasOne<VacancyRevision>()
            .WithMany()
            .HasForeignKey(r => r.VacancyRevisionId)
            .OnDelete(DeleteBehavior.Restrict);

        // Preserve reviewer identity.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(r => r.ReviewerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Review history must belong to the same application/CV.
        builder.HasOne<CvReviewVersion>()
            .WithMany()
            .HasForeignKey(r => new
            {
                r.ApplicationId,
                r.DocumentId,
                r.PreviousReviewVersionId
            })
            .HasPrincipalKey(r => new
            {
                r.ApplicationId,
                r.DocumentId,
                r.Id
            })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
