using NexaHR.Infrastructure.Persistence.Configurations.Employees;
using NexaHR.Infrastructure.Persistence.Configurations.CvReviews;
using NexaHR.Infrastructure.Persistence.Configurations.CvMatching;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NexaHR.Domain.Entities;
using NexaHR.Infrastructure.Identity;
using NexaHR.Infrastructure.Persistence.Configurations.Vacancies;
using NexaHR.Infrastructure.Persistence.Configurations.CvDocuments;
using NexaHR.Infrastructure.Persistence.Configurations.Applications;

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

    public DbSet<ConsumedRefreshToken> ConsumedRefreshTokens =>
        Set<ConsumedRefreshToken>();



    public DbSet<AuthEmailOutbox> AuthEmailOutbox =>
        Set<AuthEmailOutbox>();

    public DbSet<AccountInvitation> AccountInvitations =>
        Set<AccountInvitation>();

    public DbSet<CompanySettings> CompanySettings =>
        Set<CompanySettings>();

    public DbSet<Department> Departments =>
        Set<Department>();

    public DbSet<Notification> Notifications =>
        Set<Notification>();

    public DbSet<AuditEvent> AuditEvents =>
        Set<AuditEvent>();

    public DbSet<ProcessingPolicy> ProcessingPolicies =>
        Set<ProcessingPolicy>();

    public DbSet<LegalHold> LegalHolds =>
        Set<LegalHold>();

    public DbSet<LegalHoldEvent> LegalHoldEvents =>
        Set<LegalHoldEvent>();

    public DbSet<RetentionRun> RetentionRuns =>
        Set<RetentionRun>();

    public DbSet<CvDocument> CvDocuments =>
        Set<CvDocument>();

    public DbSet<CandidateProfile> CandidateProfiles =>
        Set<CandidateProfile>();

    public DbSet<CvExtractionRun> CvExtractionRuns
        => Set<CvExtractionRun>();

    public DbSet<CvReviewVersion> CvReviewVersions
        => Set<CvReviewVersion>();

    public DbSet<CandidateApplication> CandidateApplications =>
        Set<CandidateApplication>();

    public DbSet<ApplicationStageEvent> ApplicationStageEvents =>
        Set<ApplicationStageEvent>();

    public DbSet<ApplicationConsent> ApplicationConsents =>
        Set<ApplicationConsent>();

    public DbSet<Vacancy> Vacancies =>
        Set<Vacancy>();

    public DbSet<VacancyRevision> VacancyRevisions =>
        Set<VacancyRevision>();

    public DbSet<VacancyCriterion> VacancyCriteria =>
        Set<VacancyCriterion>();

    public DbSet<VacancyDecision> VacancyDecisions =>
        Set<VacancyDecision>();

    public DbSet<CvMatchRun> CvMatchRuns =>
        Set<CvMatchRun>();

    public DbSet<SkillCatalogueVersion> SkillCatalogueVersions =>
        Set<SkillCatalogueVersion>();

    public DbSet<CvAiAnalysisRun> CvAiAnalysisRuns =>
        Set<CvAiAnalysisRun>();

    public DbSet<Employee> Employees =>
        Set<Employee>();

    public DbSet<EmploymentHistory> EmploymentHistories =>
        Set<EmploymentHistory>();

    public DbSet<WorkEmailVerification> WorkEmailVerifications =>
        Set<WorkEmailVerification>();

    private void EnsureEmploymentHistoryImmutable()
    {
        ChangeTracker.DetectChanges();

        var invalidChanges = ChangeTracker
            .Entries<EmploymentHistory>()
            .Any(entry =>
                entry.State == EntityState.Modified ||
                entry.State == EntityState.Deleted);

        if (invalidChanges)
        {
            throw new InvalidOperationException(
                "Employment history cannot be modified or deleted.");
        }
    }

    private void EnsureAiAnalysisHistoryImmutable()
    {
        ChangeTracker.DetectChanges();

        foreach (var entry in ChangeTracker
            .Entries<CvAiAnalysisRun>())
        {
            if (entry.State == EntityState.Deleted)
            {
                throw new InvalidOperationException(
                    "AI analysis history cannot be deleted.");
            }

            if (entry.State == EntityState.Added)
            {
                if (entry.Entity.Status != "Queued" ||
                    entry.Entity.AttemptCount != 0 ||
                    entry.Entity.StartedAtUtc.HasValue ||
                    entry.Entity.CompletedAtUtc.HasValue ||
                    entry.Entity.ResultJson is not null ||
                    entry.Entity.FailureCode is not null)
                {
                    throw new InvalidOperationException(
                        "New AI analysis runs must start in Queued status.");
                }

                continue;
            }

            if (entry.State != EntityState.Modified)
            {
                continue;
            }

            var previousStatus = entry
                .Property(x => x.Status)
                .OriginalValue;

            var currentStatus = entry.Entity.Status;

            if (previousStatus is "Ready" or "Failed")
            {
                throw new InvalidOperationException(
                    "Completed AI analysis runs are immutable.");
            }

            string[] mutableProperties =
            [
                nameof(CvAiAnalysisRun.Status),
                nameof(CvAiAnalysisRun.AttemptCount),
                nameof(CvAiAnalysisRun.StartedAtUtc),
                nameof(CvAiAnalysisRun.CompletedAtUtc),
                nameof(CvAiAnalysisRun.ResultJson),
                nameof(CvAiAnalysisRun.FailureCode)
            ];

            if (entry.Properties.Any(property =>
                property.IsModified &&
                !mutableProperties.Contains(
                    property.Metadata.Name,
                    StringComparer.Ordinal)))
            {
                throw new InvalidOperationException(
                    "AI analysis source provenance cannot be changed.");
            }

            var previousAttempts = entry
                .Property(x => x.AttemptCount)
                .OriginalValue;

            var currentAttempts =
                entry.Entity.AttemptCount;

            if (currentAttempts < previousAttempts ||
                currentAttempts > 2 ||
                currentAttempts < 0 ||
                currentAttempts - previousAttempts > 1)
            {
                throw new InvalidOperationException(
                    "Invalid AI analysis attempt count.");
            }

            var validTransition =
                (previousStatus == "Queued" &&
                 currentStatus == "Processing") ||
                (previousStatus == "Processing" &&
                 currentStatus is
                     "Processing" or "Ready" or "Failed");

            if (!validTransition)
            {
                throw new InvalidOperationException(
                    "Invalid AI analysis lifecycle transition.");
            }

            if (previousStatus == "Queued" &&
                (currentAttempts != 0 ||
                 !entry.Entity.StartedAtUtc.HasValue))
            {
                throw new InvalidOperationException(
                    "AI analysis must be claimed before inference.");
            }

            if (previousStatus == "Processing" &&
                entry.Property(x => x.StartedAtUtc).IsModified)
            {
                throw new InvalidOperationException(
                    "AI analysis start timestamp cannot be changed.");
            }

            if (currentStatus == "Processing" &&
                (entry.Entity.CompletedAtUtc.HasValue ||
                 entry.Entity.ResultJson is not null ||
                 entry.Entity.FailureCode is not null))
            {
                throw new InvalidOperationException(
                    "Processing analysis cannot contain a terminal result.");
            }

            if (currentStatus == "Ready" &&
                (currentAttempts < 1 ||
                 !entry.Entity.CompletedAtUtc.HasValue ||
                 string.IsNullOrWhiteSpace(
                     entry.Entity.ResultJson) ||
                 entry.Entity.FailureCode is not null))
            {
                throw new InvalidOperationException(
                    "Ready AI analysis requires a validated result.");
            }

            if (currentStatus == "Failed" &&
                (!entry.Entity.CompletedAtUtc.HasValue ||
                 entry.Entity.ResultJson is not null ||
                 string.IsNullOrWhiteSpace(
                     entry.Entity.FailureCode)))
            {
                throw new InvalidOperationException(
                    "Failed AI analysis requires a safe failure code.");
            }
        }
    }

    private void EnsureMatchingHistoryImmutable()
    {
        ChangeTracker.DetectChanges();

        var invalidMatchChanges = ChangeTracker
            .Entries<CvMatchRun>()
            .Any(entry =>
                entry.State == EntityState.Modified ||
                entry.State == EntityState.Deleted);

        if (invalidMatchChanges)
        {
            throw new InvalidOperationException(
                "Existing CV matching runs cannot be modified or deleted.");
        }

        var invalidCatalogueChanges = ChangeTracker
            .Entries<SkillCatalogueVersion>()
            .Any(entry =>
                entry.State == EntityState.Modified ||
                entry.State == EntityState.Deleted);

        if (invalidCatalogueChanges)
        {
            throw new InvalidOperationException(
                "Published skill catalogue versions cannot be modified or deleted.");
        }
    }

    private void EnsureAuditAppendOnly()
    {
        ChangeTracker.DetectChanges();

        var invalidChanges = ChangeTracker
            .Entries<AuditEvent>()
            .Any(entry =>
                entry.State == EntityState.Modified ||
                entry.State == EntityState.Deleted);

        var invalidPrivacyHistoryChanges =
            ChangeTracker.Entries<ProcessingPolicy>()
                .Any(entry =>
                    entry.State == EntityState.Modified ||
                    entry.State == EntityState.Deleted)
            ||
            ChangeTracker.Entries<LegalHoldEvent>()
                .Any(entry =>
                    entry.State == EntityState.Modified ||
                    entry.State == EntityState.Deleted)
            ||
            ChangeTracker.Entries<LegalHold>()
                .Any(entry =>
                    entry.State == EntityState.Deleted);

        if (invalidPrivacyHistoryChanges)
        {
            throw new InvalidOperationException(
                "Privacy policy and legal hold history must be preserved.");
        }

        if (invalidChanges)
        {
            throw new InvalidOperationException(
                "Existing audit events cannot be modified or deleted.");
        }
    }

    private void EnsureVacancyHistoryImmutable()
    {
        ChangeTracker.DetectChanges();

        var invalidDecisionChanges = ChangeTracker
            .Entries<VacancyDecision>()
            .Any(entry =>
                entry.State == EntityState.Modified ||
                entry.State == EntityState.Deleted);

        if (invalidDecisionChanges)
        {
            throw new InvalidOperationException(
                "Existing vacancy decisions cannot be modified or deleted.");
        }

        var invalidRevisionChanges = ChangeTracker
            .Entries<VacancyRevision>()
            .Any(entry =>
                (entry.State == EntityState.Modified ||
                 entry.State == EntityState.Deleted) &&
                entry.Property(r => r.SubmittedAtUtc)
                    .OriginalValue.HasValue);

        if (invalidRevisionChanges)
        {
            throw new InvalidOperationException(
                "Submitted vacancy revisions cannot be modified or deleted.");
        }

        var changedCriteria = ChangeTracker
            .Entries<VacancyCriterion>()
            .Where(entry =>
                entry.State == EntityState.Added ||
                entry.State == EntityState.Modified ||
                entry.State == EntityState.Deleted)
            .ToList();

        var affectedRevisionIds = changedCriteria
            .Select(entry => entry.Entity.VacancyRevisionId)
            .Concat(changedCriteria
                .Where(entry => entry.State != EntityState.Added)
                .Select(entry => entry
                    .Property(c => c.VacancyRevisionId)
                    .OriginalValue))
            .Distinct()
            .ToList();

        if (affectedRevisionIds.Count > 0)
        {
            var submittedTrackedRevision = ChangeTracker
                .Entries<VacancyRevision>()
                .Any(entry =>
                    affectedRevisionIds.Contains(entry.Entity.Id) &&
                    (entry.Entity.SubmittedAtUtc.HasValue ||
                     (entry.State != EntityState.Added &&
                      entry.Property(r => r.SubmittedAtUtc)
                          .OriginalValue.HasValue)));

            var submittedStoredRevision = VacancyRevisions
                .AsNoTracking()
                .Any(revision =>
                    affectedRevisionIds.Contains(revision.Id) &&
                    revision.SubmittedAtUtc != null);

            var invalidCriterionChanges =
                submittedTrackedRevision ||
                submittedStoredRevision;

            if (invalidCriterionChanges)
            {
                throw new InvalidOperationException(
                    "Criteria of submitted vacancy revisions cannot be changed.");
            }
        }
    }

    private void EnsureCvHistoryImmutable()
    {
        ChangeTracker.DetectChanges();

        var invalidReviewChanges = ChangeTracker
            .Entries<CvReviewVersion>()
            .Any(entry =>
                entry.State == EntityState.Modified ||
                entry.State == EntityState.Deleted);

        if (invalidReviewChanges)
        {
            throw new InvalidOperationException(
                "Existing CV review versions cannot be modified or deleted.");
        }

        foreach (var entry in ChangeTracker
            .Entries<CvExtractionRun>())
        {
            if (entry.State == EntityState.Deleted)
            {
                throw new InvalidOperationException(
                    "CV extraction history cannot be deleted.");
            }

            if (entry.State == EntityState.Added)
            {
                if (entry.Entity.Status != "Queued")
                {
                    throw new InvalidOperationException(
                        "New extraction runs must start in Queued status.");
                }

                continue;
            }

            if (entry.State != EntityState.Modified)
                continue;

            var previousStatus = entry
                .Property(e => e.Status)
                .OriginalValue;

            var currentStatus = entry.Entity.Status;

            if (previousStatus is "ReadyForReview" or "Failed")
            {
                throw new InvalidOperationException(
                    "Completed CV extraction runs cannot be modified.");
            }

            var immutablePropertiesChanged =
                entry.Property(e => e.Id).IsModified ||
                entry.Property(e => e.ApplicationId).IsModified ||
                entry.Property(e => e.DocumentId).IsModified ||
                entry.Property(e => e.Version).IsModified ||
                entry.Property(e => e.SourceSha256Hash).IsModified ||
                entry.Property(e => e.ParserVersion).IsModified ||
                entry.Property(e => e.CreatedAtUtc).IsModified;

            if (immutablePropertiesChanged)
            {
                throw new InvalidOperationException(
                    "CV extraction source and provenance must be preserved.");
            }

            var allowedTransition =
                (previousStatus, currentStatus) switch
                {
                    ("Queued", "Processing") => true,
                    ("Processing", "ReadyForReview") => true,
                    ("Processing", "Failed") => true,
                    _ => false
                };

            if (!allowedTransition)
            {
                throw new InvalidOperationException(
                    "Invalid CV extraction lifecycle transition.");
            }

            if (previousStatus == "Processing" &&
                entry.Property(e => e.StartedAtUtc).IsModified)
            {
                throw new InvalidOperationException(
                    "Extraction start timestamp cannot be modified.");
            }
        }
    }

    private void EnsureApplicationHistoryImmutable()
    {
        ChangeTracker.DetectChanges();

        var invalidStageHistoryChanges = ChangeTracker
            .Entries<ApplicationStageEvent>()
            .Any(entry =>
                entry.State == EntityState.Modified ||
                entry.State == EntityState.Deleted);

        if (invalidStageHistoryChanges)
        {
            throw new InvalidOperationException(
                "Application stage history cannot be modified or deleted.");
        }

        var invalidConsentChanges = ChangeTracker
            .Entries<ApplicationConsent>()
            .Any(entry =>
                entry.State == EntityState.Modified ||
                entry.State == EntityState.Deleted);

        if (invalidConsentChanges)
        {
            throw new InvalidOperationException(
                "Existing application consent events cannot be modified or deleted.");
        }

        var invalidApplicationChanges = ChangeTracker
            .Entries<CandidateApplication>()
            .Any(entry =>
                entry.State == EntityState.Deleted ||
                (entry.State == EntityState.Modified &&
                    (entry.Property(a => a.CandidateUserId).IsModified ||
                     entry.Property(a => a.VacancyId).IsModified ||
                     entry.Property(a => a.VacancyRevisionId).IsModified ||
                     entry.Property(a => a.DocumentId).IsModified ||
                     entry.Property(a => a.DuplicateEmailFlag).IsModified ||
                     entry.Property(a => a.DuplicatePhoneFlag).IsModified ||
                     entry.Property(a => a.AppliedAtUtc).IsModified)));

        if (invalidApplicationChanges)
        {
            throw new InvalidOperationException(
                "Submitted application identity and source references must be preserved.");
        }
    }

    public override int SaveChanges(
        bool acceptAllChangesOnSuccess)
    {
        EnsureAuditAppendOnly();
        EnsureEmploymentHistoryImmutable();
        EnsureVacancyHistoryImmutable();
        EnsureApplicationHistoryImmutable();
        EnsureCvHistoryImmutable();
        EnsureMatchingHistoryImmutable();
        EnsureAiAnalysisHistoryImmutable();

        return base.SaveChanges(
            acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        EnsureAuditAppendOnly();
        EnsureEmploymentHistoryImmutable();
        EnsureVacancyHistoryImmutable();
        EnsureApplicationHistoryImmutable();
        EnsureCvHistoryImmutable();
        EnsureMatchingHistoryImmutable();
        EnsureAiAnalysisHistoryImmutable();

        return base.SaveChangesAsync(
            acceptAllChangesOnSuccess,
            cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfiguration(
            new EmployeeConfiguration());

        builder.ApplyConfiguration(
            new EmploymentHistoryConfiguration());

        builder.ApplyConfiguration(
            new WorkEmailVerificationConfiguration());


        builder.ApplyConfiguration(new CvDocumentConfiguration());
        builder.ApplyConfiguration(new CandidateProfileConfiguration());
        builder.ApplyConfiguration(new CandidateApplicationConfiguration());
        builder.ApplyConfiguration(new CvExtractionRunConfiguration());
        builder.ApplyConfiguration(new CvReviewVersionConfiguration());
        builder.ApplyConfiguration(new CvMatchRunConfiguration());
        builder.ApplyConfiguration(
            new NexaHR.Infrastructure.Persistence.Configurations
                .CvAiAnalysis.CvAiAnalysisRunConfiguration());
        builder.ApplyConfiguration(new SkillCatalogueVersionConfiguration());
        builder.ApplyConfiguration(new ApplicationStageEventConfiguration());
        builder.ApplyConfiguration(new ApplicationConsentConfiguration());
        builder.ApplyConfiguration(new VacancyConfiguration());
        builder.ApplyConfiguration(new VacancyRevisionConfiguration());
        builder.ApplyConfiguration(new VacancyCriterionConfiguration());
        builder.ApplyConfiguration(new VacancyDecisionConfiguration());


        builder.Entity<ProcessingPolicy>(entity =>
        {
            entity.ToTable("ProcessingPolicies", table =>
            {
                table.HasCheckConstraint(
                    "CK_ProcessingPolicy_Version",
                    "[Version] > 0");

                table.HasCheckConstraint(
                    "CK_ProcessingPolicy_RetentionDays",
                    "[CandidateRetentionDays] > 0 AND " +
                    "[EmploymentRetentionDays] > 0 AND " +
                    "[AuditRetentionDays] > 0");

                table.HasCheckConstraint(
                    "CK_ProcessingPolicy_Actions",
                    "[CandidateRetentionAction] IN ('Delete','Anonymise') AND " +
                    "[EmploymentRetentionAction] IN ('Delete','Anonymise') AND " +
                    "[AuditRetentionAction] IN ('Delete','Anonymise')");
            });

            entity.HasKey(p => p.Id);

            entity.HasIndex(p => p.Version)
                .IsUnique();

            entity.Property(p => p.CandidateRetentionAction)
                .HasMaxLength(30).IsRequired();

            entity.Property(p => p.EmploymentRetentionAction)
                .HasMaxLength(30).IsRequired();

            entity.Property(p => p.AuditRetentionAction)
                .HasMaxLength(30).IsRequired();

            entity.Property(p => p.ProcessingPurpose)
                .HasMaxLength(2000).IsRequired();

            entity.Property(p => p.DataCategories)
                .HasMaxLength(2000).IsRequired();

            entity.Property(p => p.ProviderName)
                .HasMaxLength(200).IsRequired();

            entity.Property(p => p.ProviderRetention)
                .HasMaxLength(1000).IsRequired();

            entity.Property(p => p.ProviderAccessPolicy)
                .HasMaxLength(2000).IsRequired();

            entity.Property(p => p.ProcessingLocation)
                .HasMaxLength(500).IsRequired();

            entity.Property(p => p.LegalHoldPolicy)
                .HasMaxLength(2000).IsRequired();

            entity.Property(p => p.ApprovalReference)
                .HasMaxLength(500).IsRequired();

            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(p => p.ApprovedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(p => p.RecordedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<LegalHold>(entity =>
        {
            entity.ToTable("LegalHolds");

            entity.HasKey(h => h.Id);

            entity.HasIndex(h => h.SubjectId)
                .IsUnique();

            entity.HasIndex(h => h.IsActive);

            entity.Property(h => h.Scope)
                .HasMaxLength(100).IsRequired();

            entity.Property(h => h.Reason)
                .HasMaxLength(1000).IsRequired();

            entity.Property(h => h.RowVersion)
                .IsRowVersion();

            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(h => h.SubjectId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(h => h.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(h => h.UpdatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(h => h.ReleasedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<LegalHoldEvent>(entity =>
        {
            entity.ToTable("LegalHoldEvents");

            entity.HasKey(e => e.Id);

            entity.HasIndex(e => new
            {
                e.SubjectId,
                e.CreatedAtUtc
            });

            entity.Property(e => e.Action)
                .HasMaxLength(30).IsRequired();

            entity.Property(e => e.Scope)
                .HasMaxLength(100).IsRequired();

            entity.Property(e => e.Reason)
                .HasMaxLength(1000).IsRequired();

            entity.HasOne<LegalHold>()
                .WithMany()
                .HasForeignKey(e => e.LegalHoldId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(e => e.ActorUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<RetentionRun>(entity =>
        {
            entity.ToTable("RetentionRuns", table =>
            {
                table.HasCheckConstraint(
                    "CK_RetentionRun_MaxRecords",
                    "[MaxRecords] > 0 AND [MaxRecords] <= 1000");

                table.HasCheckConstraint(
                    "CK_RetentionRun_Counts",
                    "[EligibleCount] >= 0 AND " +
                    "[ProcessedCount] >= 0 AND " +
                    "[HeldCount] >= 0 AND " +
                    "[FailedCount] >= 0 AND " +
                    "[ProcessedCount] <= [MaxRecords]");

                table.HasCheckConstraint(
                    "CK_RetentionRun_Action",
                    "[Action] IN ('Delete','Anonymise')");

                table.HasCheckConstraint(
                    "CK_RetentionRun_Status",
                    "[Status] IN ('Pending','Running'," +
                    "'Completed','Failed','Blocked')");
            });

            entity.HasKey(r => r.Id);

            entity.HasIndex(r => new
            {
                r.Status,
                r.CreatedAtUtc
            });

            entity.Property(r => r.DataCategory)
                .HasMaxLength(100).IsRequired();

            entity.Property(r => r.Action)
                .HasMaxLength(30).IsRequired();

            entity.Property(r => r.AuthorizationReason)
                .HasMaxLength(1000).IsRequired();

            entity.Property(r => r.Status)
                .HasMaxLength(30).IsRequired();

            entity.Property(r => r.ResultCode)
                .HasMaxLength(100);

            entity.Property(r => r.RowVersion)
                .IsRowVersion();

            entity.HasOne<ProcessingPolicy>()
                .WithMany()
                .HasForeignKey(r => r.ProcessingPolicyId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(r => r.AuthorizedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });


        builder.Entity<AuditEvent>(entity =>
        {
            entity.ToTable("AuditEvents");

            entity.HasKey(a => a.Id);

            entity.Property(a => a.ActorRole)
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(a => a.Module)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(a => a.Action)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(a => a.EntityType)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(a => a.Outcome)
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(a => a.ReasonCode)
                .HasMaxLength(100);

            entity.Property(a => a.CreatedAtUtc)
                .IsRequired();

            entity.HasIndex(a => a.CreatedAtUtc);

            entity.HasIndex(a => new
            {
                a.Module,
                a.CreatedAtUtc
            });

            entity.HasIndex(a => new
            {
                a.ActorUserId,
                a.CreatedAtUtc
            });

            entity.HasIndex(a => new
            {
                a.EntityType,
                a.EntityId
            });

            entity.HasIndex(a => a.CorrelationId);

            entity.HasIndex(a => new
            {
                a.IsHrVisible,
                a.CreatedAtUtc
            });

            entity.HasIndex(a => new
            {
                a.IsCeoVisible,
                a.CreatedAtUtc
            });
        });


        builder.Entity<Notification>(entity =>
        {
            entity.ToTable("Notifications");

            entity.HasKey(n => n.Id);

            entity.Property(n => n.Type)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(n => n.Title)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(n => n.Message)
                .HasMaxLength(1000)
                .IsRequired();

            entity.Property(n => n.TargetType)
                .HasMaxLength(50);

            entity.Property(n => n.CreatedAtUtc)
                .IsRequired();

            entity.HasIndex(n => new
            {
                n.RecipientUserId,
                n.IsRead,
                n.CreatedAtUtc
            });

            entity.HasIndex(n => new
            {
                n.RecipientUserId,
                n.CreatedAtUtc
            });

            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(n => n.RecipientUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });


        builder.Entity<Department>(entity =>
        {
            entity.ToTable("Departments");

            entity.HasKey(d => d.Id);

            entity.Property(d => d.Name)
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(d => d.NormalizedName)
                .HasMaxLength(150)
                .IsRequired();

            entity.HasIndex(d => d.NormalizedName)
                .IsUnique();

            entity.Property(d => d.Description)
                .HasMaxLength(500);

            entity.HasIndex(d => d.IsArchived);

            entity.Property(d => d.RowVersion)
                .IsRowVersion();

            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(d => d.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(d => d.UpdatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(d => d.ArchivedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });


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

            entity.Property(user =>
                    user.HasPrivacyGovernanceAccess)
                .HasDefaultValue(false)
                .IsRequired();

            entity.HasIndex(user => user.NormalizedEmail)
                .HasDatabaseName("EmailIndex")
                .IsUnique()
                .HasFilter("[NormalizedEmail] IS NOT NULL");

            entity.HasIndex(user => user.IsActive);
        });

        builder.Entity<ConsumedRefreshToken>(entity =>
        {
            entity.ToTable("ConsumedRefreshTokens");

            entity.HasKey(token => token.Id);

            entity.Property(token => token.TokenHash)
                .HasMaxLength(64)
                .IsRequired();

            entity.HasIndex(token => token.TokenHash)
                .IsUnique();

            entity.HasIndex(token => token.AuthSessionId);

            entity.Property(token => token.ConsumedAtUtc)
                .IsRequired();

            entity.HasOne<AuthSession>()
                .WithMany()
                .HasForeignKey(token => token.AuthSessionId)
                .OnDelete(DeleteBehavior.Cascade);
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
