using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexaHR.Domain.Entities;
using NexaHR.Infrastructure.Identity;

namespace NexaHR.Infrastructure.Persistence.Configurations.Vacancies;

public sealed class VacancyConfiguration
    : IEntityTypeConfiguration<Vacancy>
{
    public void Configure(EntityTypeBuilder<Vacancy> builder)
    {
        builder.ToTable("Vacancies", table =>
        {
            table.HasCheckConstraint(
                "CK_Vacancy_PublicationMetadata",
                "([PublishedRevisionId] IS NULL AND [PublishedAtUtc] IS NULL AND [PublishedByUserId] IS NULL) OR " +
                "([PublishedRevisionId] IS NOT NULL AND [PublishedAtUtc] IS NOT NULL AND [PublishedByUserId] IS NOT NULL)");

            table.HasCheckConstraint(
                "CK_Vacancy_PublishedStatus",
                "[Status] <> 'Published' OR [PublishedRevisionId] IS NOT NULL");

            table.HasCheckConstraint(
                "CK_Vacancy_ClosureMetadata",
                "([Status] = 'Closed' AND [ClosedAtUtc] IS NOT NULL AND [ClosedByUserId] IS NOT NULL AND [ClosureReason] IS NOT NULL AND LEN(LTRIM(RTRIM([ClosureReason]))) > 0) OR " +
                "([Status] <> 'Closed' AND [ClosedAtUtc] IS NULL AND [ClosedByUserId] IS NULL AND [ClosureReason] IS NULL)");

            table.HasCheckConstraint(
                "CK_Vacancy_Status",
                "[Status] IN ('Draft','PendingApproval','Approved','Rejected','Published','Paused','Closed')");
        });

        builder.HasKey(v => v.Id);

        builder.Property(v => v.Status)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(v => v.CreatedAtUtc)
            .IsRequired();

        builder.HasOne<VacancyRevision>()
            .WithMany()
            .HasForeignKey(v => new
            {
                v.Id,
                v.PublishedRevisionId
            })
            .HasPrincipalKey(r => new
            {
                r.VacancyId,
                r.Id
            })
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(v => v.ClosureReason)
            .HasMaxLength(2000);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(v => v.PublishedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(v => v.ClosedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(v => v.RowVersion)
            .IsRowVersion();

        builder.HasIndex(v => v.Status);

        builder.HasIndex(v => v.DepartmentId);

        builder.HasOne<Department>()
            .WithMany()
            .HasForeignKey(v => v.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(v => v.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
