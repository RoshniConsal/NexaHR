using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexaHR.Domain.Entities;
using NexaHR.Infrastructure.Identity;

namespace NexaHR.Infrastructure.Persistence.Configurations.Employees;

public sealed class WorkEmailVerificationConfiguration
    : IEntityTypeConfiguration<WorkEmailVerification>
{
    public void Configure(
        EntityTypeBuilder<WorkEmailVerification> builder)
    {
        builder.ToTable("WorkEmailVerifications");

        builder.HasKey(e => e.Id);

        builder.HasIndex(e => e.TokenHash)
            .IsUnique();

        builder.HasIndex(e => e.EmployeeId)
            .IsUnique()
            .HasFilter(
                "[ConfirmedAtUtc] IS NULL AND " +
                "[RevokedAtUtc] IS NULL");

        builder.Property(e => e.ProposedEmail)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(e => e.NormalizedProposedEmail)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(e => e.TokenHash)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(e => e.RowVersion)
            .IsRowVersion();

        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(e => e.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(e => e.RequestedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
