using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexaHR.Domain.Entities;
using NexaHR.Infrastructure.Identity;

namespace NexaHR.Infrastructure.Persistence.Configurations.Employees;

public sealed class EmploymentHistoryConfiguration
    : IEntityTypeConfiguration<EmploymentHistory>
{
    public void Configure(
        EntityTypeBuilder<EmploymentHistory> builder)
    {
        builder.ToTable("EmploymentHistories");

        builder.HasKey(e => e.Id);

        builder.HasIndex(e => new
        {
            e.EmployeeId,
            e.ChangedAtUtc
        });

        builder.Property(e => e.ChangeType)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.Reason)
            .HasMaxLength(1000)
            .IsRequired();

        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(e => e.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(e => e.ChangedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
