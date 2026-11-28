using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexaHR.Domain.Entities;
using NexaHR.Infrastructure.Identity;

namespace NexaHR.Infrastructure.Persistence.Configurations.Policies;

public sealed class PolicyAcknowledgementConfiguration
    : IEntityTypeConfiguration<PolicyAcknowledgement>
{
    public void Configure(
        EntityTypeBuilder<PolicyAcknowledgement> builder)
    {
        builder.ToTable("PolicyAcknowledgements");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => new
        {
            x.PolicyVersionId,
            x.UserId
        })
        .IsUnique();

        builder.HasIndex(x => new
        {
            x.UserId,
            x.AcknowledgedAtUtc
        });

        builder.HasOne<HrPolicyVersion>()
            .WithMany()
            .HasForeignKey(x => x.PolicyVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
