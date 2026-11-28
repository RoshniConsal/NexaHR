using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexaHR.Domain.Entities;
using NexaHR.Infrastructure.Identity;

namespace NexaHR.Infrastructure.Persistence.Configurations.Applications;

public sealed class CandidateProfileConfiguration
    : IEntityTypeConfiguration<CandidateProfile>
{
    public void Configure(
        EntityTypeBuilder<CandidateProfile> builder)
    {
        builder.ToTable("CandidateProfiles");

        builder.HasKey(p => p.UserId);

        builder.Property(p => p.UserId)
            .ValueGeneratedNever();

        builder.Property(p => p.Skills)
            .HasMaxLength(4000);

        builder.Property(p => p.Education)
            .HasMaxLength(4000);

        builder.Property(p => p.CreatedAtUtc)
            .IsRequired();

        builder.Property(p => p.RowVersion)
            .IsRowVersion();

        builder.HasOne<ApplicationUser>()
            .WithOne()
            .HasForeignKey<CandidateProfile>(p => p.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
