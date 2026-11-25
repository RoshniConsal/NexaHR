using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexaHR.Domain.Entities;
using NexaHR.Infrastructure.Identity;

namespace NexaHR.Infrastructure.Persistence.Configurations.Vacancies;

public sealed class VacancyDecisionConfiguration
    : IEntityTypeConfiguration<VacancyDecision>
{
    public void Configure(EntityTypeBuilder<VacancyDecision> builder)
    {
        builder.ToTable("VacancyDecisions", table =>
        {
            table.HasCheckConstraint(
                "CK_VacancyDecision_ApproverRole",
                "[ApproverRole] = 'CEO'");

            table.HasCheckConstraint(
                "CK_VacancyDecision_Decision",
                "[Decision] IN ('Approved','Rejected')");

            table.HasCheckConstraint(
                "CK_VacancyDecision_RejectionComment",
                "[Decision] <> 'Rejected' OR " +
                "([Comment] IS NOT NULL AND LEN(LTRIM(RTRIM([Comment]))) > 0)");
        });

        builder.HasKey(d => d.Id);

        builder.HasIndex(d => d.VacancyRevisionId)
            .IsUnique();

        builder.Property(d => d.ApproverRole)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(d => d.Decision)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(d => d.Comment)
            .HasMaxLength(2000);

        builder.Property(d => d.AtUtc)
            .IsRequired();

        builder.HasOne<VacancyRevision>()
            .WithOne()
            .HasForeignKey<VacancyDecision>(d => d.VacancyRevisionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(d => d.ApproverUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
