using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexaHR.Domain.Entities;
using NexaHR.Infrastructure.Identity;

namespace NexaHR.Infrastructure.Persistence.Configurations.Policies;

public sealed class HrPolicyConfiguration
    : IEntityTypeConfiguration<HrPolicy>
{
    public void Configure(EntityTypeBuilder<HrPolicy> builder)
    {
        builder.ToTable("HrPolicies");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.DraftTitle)
            .HasMaxLength(200);

        builder.Property(x => x.DraftContent)
            .HasMaxLength(50000);

        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        builder.HasIndex(x => x.CurrentPublishedVersion);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
