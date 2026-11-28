using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexaHR.Domain.Entities;
using NexaHR.Infrastructure.Identity;

namespace NexaHR.Infrastructure.Persistence.Configurations.Policies;

public sealed class HrPolicyVersionConfiguration
    : IEntityTypeConfiguration<HrPolicyVersion>
{
    public void Configure(
        EntityTypeBuilder<HrPolicyVersion> builder)
    {
        builder.ToTable("HrPolicyVersions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Content)
            .IsRequired()
            .HasMaxLength(50000);

        builder.HasIndex(x => new
        {
            x.PolicyId,
            x.Version
        })
        .IsUnique();

        builder.HasOne<HrPolicy>()
            .WithMany()
            .HasForeignKey(x => x.PolicyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.PublishedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
