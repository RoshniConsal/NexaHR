using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexaHR.Domain.Entities;
using NexaHR.Infrastructure.Identity;

namespace NexaHR.Infrastructure.Persistence.Configurations.CvDocuments;

public sealed class CvDocumentConfiguration
    : IEntityTypeConfiguration<CvDocument>
{
    public void Configure(
        EntityTypeBuilder<CvDocument> builder)
    {
        builder.ToTable("CvDocuments", table =>
        {
            table.HasCheckConstraint(
                "CK_CvDocument_Version",
                "[Version] > 0");

            table.HasCheckConstraint(
                "CK_CvDocument_SizeBytes",
                "[SizeBytes] > 0 AND [SizeBytes] <= 5242880");

            table.HasCheckConstraint(
                "CK_CvDocument_ContentType",
                "[ContentType] IN (" +
                "'application/pdf'," +
                "'application/vnd.openxmlformats-officedocument.wordprocessingml.document')");

            table.HasCheckConstraint(
                "CK_CvDocument_ScanStatus",
                "[ScanStatus] IN ('Pending','Clean','Infected','Failed')");

            table.HasCheckConstraint(
                "CK_CvDocument_ScanMetadata",
                "([ScanStatus] = 'Pending' AND " +
                "[ScannedAtUtc] IS NULL) OR " +
                "([ScanStatus] IN ('Clean','Infected','Failed') AND " +
                "[ScannedAtUtc] IS NOT NULL)");

            table.HasCheckConstraint(
                "CK_CvDocument_Sha256Hash",
                "LEN([Sha256Hash]) = 64");
        });

        builder.HasKey(c => c.Id);

        builder.Property(c => c.OriginalFileName)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(c => c.StorageKey)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(c => c.ContentType)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(c => c.Sha256Hash)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(c => c.ScanStatus)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(c => c.ScanResultCode)
            .HasMaxLength(100);

        builder.Property(c => c.CreatedAtUtc)
            .IsRequired();

        builder.Property(c => c.RowVersion)
            .IsRowVersion();

        builder.HasIndex(c => new
        {
            c.OwnerUserId,
            c.Version
        }).IsUnique();

        builder.HasIndex(c => c.StorageKey)
            .IsUnique();

        builder.HasIndex(c => new
        {
            c.OwnerUserId,
            c.CreatedAtUtc
        });

        builder.HasIndex(c => new
        {
            c.ScanStatus,
            c.CreatedAtUtc
        });

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(c => c.OwnerUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
