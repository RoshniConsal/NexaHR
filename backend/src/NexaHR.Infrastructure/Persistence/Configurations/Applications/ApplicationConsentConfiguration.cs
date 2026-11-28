using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexaHR.Domain.Entities;
using NexaHR.Infrastructure.Identity;

namespace NexaHR.Infrastructure.Persistence.Configurations.Applications;

public sealed class ApplicationConsentConfiguration
    : IEntityTypeConfiguration<ApplicationConsent>
{
    public void Configure(
        EntityTypeBuilder<ApplicationConsent> builder)
    {
        builder.ToTable("ApplicationConsents", table =>
        {
            table.HasCheckConstraint(
                "CK_ApplicationConsent_Purpose",
                "[Purpose] IN ('Submission','AiProcessing')");

            table.HasCheckConstraint(
                "CK_ApplicationConsent_Submission",
                "[Purpose] <> 'Submission' OR " +
                "([Granted] = 1 AND [Provider] IS NULL)");

            table.HasCheckConstraint(
                "CK_ApplicationConsent_AiProvider",
                "[Purpose] <> 'AiProcessing' OR " +
                "([Provider] IS NOT NULL AND " +
                "LEN(LTRIM(RTRIM([Provider]))) > 0 AND " +
                "[ProcessingPolicyId] IS NOT NULL)");

            table.HasCheckConstraint(
                "CK_ApplicationConsent_Text",
                "LEN(LTRIM(RTRIM([TextVersion]))) > 0 AND " +
                "LEN(LTRIM(RTRIM([ConsentText]))) > 0");
        });

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Purpose)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(c => c.Provider)
            .HasMaxLength(200);

        builder.Property(c => c.TextVersion)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(c => c.ConsentText)
            .HasMaxLength(10000)
            .IsRequired();

        builder.Property(c => c.AtUtc)
            .IsRequired();

        builder.HasIndex(c => new
        {
            c.ApplicationId,
            c.Purpose,
            c.Provider,
            c.AtUtc
        });

        builder.HasIndex(c => c.ApplicationId)
            .IsUnique()
            .HasFilter("[Purpose] = 'Submission'");

        builder.HasOne<CandidateApplication>()
            .WithMany()
            .HasForeignKey(c => c.ApplicationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(c => c.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ProcessingPolicy>()
            .WithMany()
            .HasForeignKey(c => c.ProcessingPolicyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
