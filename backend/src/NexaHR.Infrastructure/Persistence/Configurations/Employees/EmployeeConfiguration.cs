using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexaHR.Domain.Entities;
using NexaHR.Infrastructure.Identity;

namespace NexaHR.Infrastructure.Persistence.Configurations.Employees;

public sealed class EmployeeConfiguration
    : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("Employees");

        builder.HasKey(e => e.Id);

        builder.HasIndex(e => e.UserId)
            .IsUnique();

        builder.HasIndex(e => e.EmployeeNumber)
            .IsUnique();

        builder.HasIndex(e => e.NormalizedWorkEmail)
            .IsUnique()
            .HasFilter("[NormalizedWorkEmail] IS NOT NULL");

        builder.HasIndex(e => new
        {
            e.DepartmentId,
            e.IsActive
        });

        builder.Property(e => e.EmployeeNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.Designation)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.EmploymentType)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.WorkEmail)
            .HasMaxLength(256);

        builder.Property(e => e.NormalizedWorkEmail)
            .HasMaxLength(256);

        builder.Property(e => e.RowVersion)
            .IsRowVersion();

        builder.HasOne<ApplicationUser>()
            .WithOne()
            .HasForeignKey<Employee>(e => e.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Department>()
            .WithMany()
            .HasForeignKey(e => e.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(e => e.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(e => e.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
