using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexaHR.Domain.Entities;

namespace NexaHR.Infrastructure.Persistence.Configurations.Applications;

public sealed class CandidateApplicationConfiguration
    : IEntityTypeConfiguration<CandidateApplication>
{
    public void Configure(
        EntityTypeBuilder<CandidateApplication> builder)
    {
        builder.ToTable("CandidateApplications", table =>
        {
            table.HasCheckConstraint(
                "CK_CandidateApplication_Stage",
                "[Stage] IN ('Applied','UnderReview'," +
                "'Shortlisted','Interview','OfferSent'," +
                "'OfferAccepted','Hired','Rejected'," +
                "'Withdrawn','OfferDeclined')");
        });

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Stage)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(a => a.DuplicateEmailFlag)
            .IsRequired();

        builder.Property(a => a.DuplicatePhoneFlag)
            .IsRequired();

        builder.Property(a => a.AppliedAtUtc)
            .IsRequired();

        builder.Property(a => a.RowVersion)
            .IsRowVersion();

        builder.HasIndex(a => new
        {
            a.CandidateUserId,
            a.VacancyId
        }).IsUnique();

        builder.HasIndex(a => new
        {
            a.VacancyId,
            a.Stage
        });

        builder.HasIndex(a => new
        {
            a.CandidateUserId,
            a.AppliedAtUtc
        });

        builder.HasOne<CandidateProfile>()
            .WithMany()
            .HasForeignKey(a => a.CandidateUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Vacancy>()
            .WithMany()
            .HasForeignKey(a => a.VacancyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<VacancyRevision>()
            .WithMany()
            .HasForeignKey(a => new
            {
                a.VacancyId,
                a.VacancyRevisionId
            })
            .HasPrincipalKey(r => new
            {
                r.VacancyId,
                r.Id
            })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<CvDocument>()
            .WithMany()
            .HasForeignKey(a => new
            {
                a.CandidateUserId,
                a.DocumentId
            })
            .HasPrincipalKey(d => new
            {
                d.OwnerUserId,
                d.Id
            })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
