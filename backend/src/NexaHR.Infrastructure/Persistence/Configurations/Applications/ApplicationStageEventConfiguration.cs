using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexaHR.Domain.Entities;
using NexaHR.Infrastructure.Identity;

namespace NexaHR.Infrastructure.Persistence.Configurations.Applications;

public sealed class ApplicationStageEventConfiguration
    : IEntityTypeConfiguration<ApplicationStageEvent>
{
    public void Configure(
        EntityTypeBuilder<ApplicationStageEvent> builder)
    {
        const string stages =
            "('Applied','UnderReview','Shortlisted'," +
            "'Interview','OfferSent','OfferAccepted'," +
            "'Hired','Rejected','Withdrawn','OfferDeclined')";

        builder.ToTable("ApplicationStageEvents", table =>
        {
            table.HasCheckConstraint(
                "CK_ApplicationStageEvent_FromStage",
                "[FromStage] IS NULL OR [FromStage] IN " + stages);

            table.HasCheckConstraint(
                "CK_ApplicationStageEvent_ToStage",
                "[ToStage] IN " + stages);

            table.HasCheckConstraint(
                "CK_ApplicationStageEvent_InitialStage",
                "[FromStage] IS NOT NULL OR [ToStage] = 'Applied'");

            table.HasCheckConstraint(
                "CK_ApplicationStageEvent_ChangedStage",
                "[FromStage] IS NULL OR [FromStage] <> [ToStage]");

            table.HasCheckConstraint(
                "CK_ApplicationStageEvent_Reason",
                "[ToStage] NOT IN ('Rejected','Withdrawn') OR " +
                "([Reason] IS NOT NULL AND " +
                "LEN(LTRIM(RTRIM([Reason]))) > 0)");
        });

        builder.HasKey(e => e.Id);

        builder.Property(e => e.ActorRole)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(e => e.FromStage)
            .HasMaxLength(30);

        builder.Property(e => e.ToStage)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(e => e.Reason)
            .HasMaxLength(2000);

        builder.Property(e => e.AtUtc)
            .IsRequired();

        builder.HasIndex(e => new
        {
            e.ApplicationId,
            e.AtUtc
        });

        builder.HasIndex(e => e.ApplicationId)
            .IsUnique()
            .HasFilter("[FromStage] IS NULL");

        builder.HasOne<CandidateApplication>()
            .WithMany()
            .HasForeignKey(e => e.ApplicationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(e => e.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
