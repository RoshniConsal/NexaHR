using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NexaHR.Domain.Entities;
using NexaHR.Infrastructure.Identity;

namespace NexaHR.Infrastructure.Persistence;

public class NexaHRDbContext
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public NexaHRDbContext(
        DbContextOptions<NexaHRDbContext> options)
        : base(options)
    {
    }

    public DbSet<AuthSession> AuthSessions =>
        Set<AuthSession>();


    public DbSet<AuthEmailOutbox> AuthEmailOutbox =>
        Set<AuthEmailOutbox>();

    public DbSet<AccountInvitation> AccountInvitations =>
        Set<AccountInvitation>();

    public DbSet<CompanySettings> CompanySettings =>
        Set<CompanySettings>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<CompanySettings>(entity =>
        {
            entity.ToTable(
                "CompanySettings",
                table => table.HasCheckConstraint(
                    "CK_CompanySettings_SingleRecord",
                    "[Id] = 1"));

            entity.HasKey(c => c.Id);

            entity.Property(c => c.Id)
                .ValueGeneratedNever();

            entity.Property(c => c.CompanyName)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(c => c.SupportEmail)
                .HasMaxLength(256);

            entity.Property(c => c.SupportPhone)
                .HasMaxLength(30);

            entity.Property(c => c.BusinessTimeZone)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(c => c.RowVersion)
                .IsRowVersion();

            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(c => c.UpdatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });


        builder.Entity<AccountInvitation>(entity =>
        {
            entity.ToTable("AccountInvitations");

            entity.HasKey(i => i.Id);

            entity.Property(i => i.Email)
                .HasMaxLength(256)
                .IsRequired();

            entity.Property(i => i.NormalizedEmail)
                .HasMaxLength(256)
                .IsRequired();

            entity.Property(i => i.Role)
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(i => i.TokenHash)
                .HasMaxLength(64)
                .IsRequired();

            entity.Property(i => i.RevocationReason)
                .HasMaxLength(500);

            entity.HasIndex(i => i.TokenHash)
                .IsUnique();

            entity.HasIndex(i => new
            {
                i.NormalizedEmail,
                i.AcceptedAtUtc,
                i.RevokedAtUtc
            });

            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(i => i.IssuedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(i => i.AcceptedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(i => i.RevokedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<AccountInvitation>()
                .WithMany()
                .HasForeignKey(i => i.ReissuedFromInvitationId)
                .OnDelete(DeleteBehavior.Restrict);
        });


        builder.Entity<AuthEmailOutbox>(entity =>
        {
            entity.ToTable("AuthEmailOutbox");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.RecipientEmail)
                .HasMaxLength(256)
                .IsRequired();

            entity.Property(e => e.EmailType)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.ProtectedToken)
                .IsRequired();

            entity.HasIndex(e => new
            {
                e.SentAtUtc,
                e.NextAttemptAtUtc
            });
        });


        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(user => user.FullName)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(user => user.CreatedAtUtc)
                .IsRequired();

            entity.HasIndex(user => user.IsActive);
        });

        builder.Entity<AuthSession>(entity =>
        {
            entity.ToTable("AuthSessions");

            entity.HasKey(session => session.Id);

            entity.Property(session => session.RefreshTokenHash)
                .HasMaxLength(64)
                .IsRequired()
                .IsConcurrencyToken();

            entity.HasIndex(session => session.RefreshTokenHash)
                .IsUnique();

            entity.HasIndex(session => session.UserId);

            entity.Property(session => session.CreatedAtUtc)
                .IsRequired();

            entity.Property(session => session.ExpiresAtUtc)
                .IsRequired();

            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(session => session.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
