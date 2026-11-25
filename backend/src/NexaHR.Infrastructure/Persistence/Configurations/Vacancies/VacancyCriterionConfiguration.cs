using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexaHR.Domain.Entities;

namespace NexaHR.Infrastructure.Persistence.Configurations.Vacancies;

public sealed class VacancyCriterionConfiguration
    : IEntityTypeConfiguration<VacancyCriterion>
{
    public void Configure(EntityTypeBuilder<VacancyCriterion> builder)
    {
        builder.ToTable("VacancyCriteria", table =>
        {
            table.HasCheckConstraint(
                "CK_VacancyCriterion_Kind",
                "[Kind] IN ('RequiredSkill','PreferredSkill','InterviewCriterion','ReviewFlag')");

            table.HasCheckConstraint(
                "CK_VacancyCriterion_Weight",
                "[Weight] >= 0 AND " +
                "([Kind] <> 'RequiredSkill' OR [Weight] = 2) AND " +
                "([Kind] <> 'PreferredSkill' OR [Weight] = 1)");
        });

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Kind)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(c => c.Label)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(c => c.Weight)
            .HasPrecision(5, 2);

        builder.HasIndex(c => new
        {
            c.VacancyRevisionId,
            c.Kind,
            c.Label
        }).IsUnique();

        builder.HasOne<VacancyRevision>()
            .WithMany()
            .HasForeignKey(c => c.VacancyRevisionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
