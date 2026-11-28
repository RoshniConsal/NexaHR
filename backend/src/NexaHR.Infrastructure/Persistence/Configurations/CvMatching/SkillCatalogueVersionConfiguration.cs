using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexaHR.Domain.Entities;
using NexaHR.Infrastructure.Identity;

namespace NexaHR.Infrastructure.Persistence.Configurations.CvMatching;

public sealed class SkillCatalogueVersionConfiguration
    : IEntityTypeConfiguration<SkillCatalogueVersion>
{
    public void Configure(
        EntityTypeBuilder<SkillCatalogueVersion> builder)
    {
        builder.ToTable("SkillCatalogueVersions", table =>
        {
            table.HasCheckConstraint(
                "CK_SkillCatalogueVersion_Version",
                "[Version] > 0");

            table.HasCheckConstraint(
                "CK_SkillCatalogueVersion_Json",
                "ISJSON([CatalogueJson], ARRAY) = 1");

            table.HasCheckConstraint(
                "CK_SkillCatalogueVersion_Hash",
                "LEN([ContentSha256Hash]) = 64");

            table.HasCheckConstraint(
                "CK_SkillCatalogueVersion_Parent",
                "([Version] = 1 AND [PreviousVersionId] IS NULL) OR " +
                "([Version] > 1 AND [PreviousVersionId] IS NOT NULL " +
                "AND [PreviousVersionId] <> [Id])");

            table.HasCheckConstraint(
                "CK_SkillCatalogueVersion_ReviewReason",
                "LEN(LTRIM(RTRIM([ReviewReason]))) > 0");
        });

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.Version)
            .IsUnique();

        builder.HasIndex(x => x.PreviousVersionId)
            .IsUnique()
            .HasFilter("[PreviousVersionId] IS NOT NULL");

        builder.Property(x => x.CatalogueJson)
            .IsRequired();

        builder.Property(x => x.ContentSha256Hash)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.ReviewReason)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(x => x.PublishedAtUtc)
            .IsRequired();

        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        builder.HasOne<SkillCatalogueVersion>()
            .WithMany()
            .HasForeignKey(x => x.PreviousVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.PublishedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
