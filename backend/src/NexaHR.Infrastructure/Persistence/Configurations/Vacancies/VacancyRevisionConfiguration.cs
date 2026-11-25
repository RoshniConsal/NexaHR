using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexaHR.Domain.Entities;
using NexaHR.Infrastructure.Identity;

namespace NexaHR.Infrastructure.Persistence.Configurations.Vacancies;

public sealed class VacancyRevisionConfiguration
    : IEntityTypeConfiguration<VacancyRevision>
{
    public void Configure(EntityTypeBuilder<VacancyRevision> builder)
    {
        builder.ToTable("VacancyRevisions", table =>
        {
            table.HasCheckConstraint(
                "CK_VacancyRevision_Revision",
                "[Revision] > 0");

            table.HasCheckConstraint(
                "CK_VacancyRevision_Openings",
                "[Openings] > 0");

            table.HasCheckConstraint(
                "CK_VacancyRevision_Experience",
                "[MinimumExperienceYears] IS NULL OR [MinimumExperienceYears] >= 0");

            table.HasCheckConstraint(
                "CK_VacancyRevision_Submission",
                "([SubmittedAtUtc] IS NULL AND [SubmittedByUserId] IS NULL) OR " +
                "([SubmittedAtUtc] IS NOT NULL AND [SubmittedByUserId] IS NOT NULL)");
        });

        builder.HasKey(v => v.Id);

        builder.HasIndex(v => new
        {
            v.VacancyId,
            v.Revision
        }).IsUnique();

        builder.Property(v => v.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(v => v.EmploymentType)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(v => v.Location)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(v => v.WorkMode)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(v => v.Description)
            .HasMaxLength(10000)
            .IsRequired();

        builder.Property(v => v.HiringReason)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(v => v.MinimumExperienceYears)
            .HasPrecision(5, 2);

        builder.Property(v => v.ClosingDate)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(v => v.RowVersion)
            .IsRowVersion();

        builder.HasOne<Vacancy>()
            .WithMany()
            .HasForeignKey(v => v.VacancyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Department>()
            .WithMany()
            .HasForeignKey(v => v.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(v => v.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(v => v.SubmittedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
