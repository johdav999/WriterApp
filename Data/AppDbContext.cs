using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using WriterApp.Data.AI;
using WriterApp.Data.Admin;
using WriterApp.Data.Continuity;
using WriterApp.Data.Documents;
using WriterApp.Data.Exporting;
using WriterApp.Data.Security;
using WriterApp.Data.Subscriptions;
using WriterApp.Data.Usage;

namespace WriterApp.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions options)
            : base(options)
        {
        }

        public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
        public DbSet<UserEntitlement> UserEntitlements => Set<UserEntitlement>();
        public DbSet<AdminAuditEvent> AdminAuditEvents => Set<AdminAuditEvent>();
        public DbSet<AdminRoleAssignment> AdminRoleAssignments => Set<AdminRoleAssignment>();
        public DbSet<DeletedUserIdentity> DeletedUserIdentities => Set<DeletedUserIdentity>();
        public DbSet<StripeEventLog> StripeEventLogs => Set<StripeEventLog>();
        public DbSet<Plan> Plans => Set<Plan>();
        public DbSet<PlanEntitlement> PlanEntitlements => Set<PlanEntitlement>();
        public DbSet<UserPlanAssignment> UserPlanAssignments => Set<UserPlanAssignment>();
        public DbSet<TokenAdjustment> TokenAdjustments => Set<TokenAdjustment>();
        public DbSet<UsageEvent> UsageEvents => Set<UsageEvent>();
        public DbSet<UserEvent> UserEvents => Set<UserEvent>();
        public DbSet<UsageAggregate> UsageAggregates => Set<UsageAggregate>();
        public DbSet<DocumentRecord> Documents => Set<DocumentRecord>();
        public DbSet<SectionRecord> Sections => Set<SectionRecord>();
        public DbSet<PageRecord> Pages => Set<PageRecord>();
        public DbSet<PageAnnotationRecord> PageAnnotations => Set<PageAnnotationRecord>();
        public DbSet<PageQualityIssueRecord> PageQualityIssues => Set<PageQualityIssueRecord>();
        public DbSet<PageQualityIssueDismissalRecord> PageQualityIssueDismissals => Set<PageQualityIssueDismissalRecord>();
        public DbSet<PageVersionRecord> PageVersions => Set<PageVersionRecord>();
        public DbSet<DocumentOutlineNodeRecord> DocumentOutlineNodes => Set<DocumentOutlineNodeRecord>();
        public DbSet<PageNoteRecord> PageNotes => Set<PageNoteRecord>();
        public DbSet<SectionNoteRecord> SectionNotes => Set<SectionNoteRecord>();
        public DbSet<SectionSceneCardRecord> SectionSceneCards => Set<SectionSceneCardRecord>();
        public DbSet<SceneContentRecord> SceneContents => Set<SceneContentRecord>();
        public DbSet<SceneNoteRecord> SceneNotes => Set<SceneNoteRecord>();
        public DbSet<SceneCardRecord> SceneCards => Set<SceneCardRecord>();
        public DbSet<SceneAnnotationRecord> SceneAnnotations => Set<SceneAnnotationRecord>();
        public DbSet<SceneQualityIssueRecord> SceneQualityIssues => Set<SceneQualityIssueRecord>();
        public DbSet<SceneVersionRecord> SceneVersions => Set<SceneVersionRecord>();
        public DbSet<OutlineTemplateRecord> OutlineTemplates => Set<OutlineTemplateRecord>();
        public DbSet<ProjectRecord> Projects => Set<ProjectRecord>();
        public DbSet<ProjectNodeRecord> ProjectNodes => Set<ProjectNodeRecord>();
        public DbSet<ProjectGoalRecord> ProjectGoals => Set<ProjectGoalRecord>();
        public DbSet<ProjectProgressDailyRecord> ProjectProgressDaily => Set<ProjectProgressDailyRecord>();
        public DbSet<ProjectProgressEventRecord> ProjectProgressEvents => Set<ProjectProgressEventRecord>();
        public DbSet<ProjectMilestoneRecord> ProjectMilestones => Set<ProjectMilestoneRecord>();
        public DbSet<WritingSessionRecord> WritingSessions => Set<WritingSessionRecord>();
        public DbSet<DocumentOutlineRecord> DocumentOutlines => Set<DocumentOutlineRecord>();
        public DbSet<DocumentSynopsisRecord> DocumentSynopses => Set<DocumentSynopsisRecord>();
        public DbSet<DocumentGlossaryEntryRecord> DocumentGlossaryEntries => Set<DocumentGlossaryEntryRecord>();
        public DbSet<AiActionHistoryEntryRecord> AiActionHistoryEntries => Set<AiActionHistoryEntryRecord>();
        public DbSet<AiActionAppliedEventRecord> AiActionAppliedEvents => Set<AiActionAppliedEventRecord>();
        public DbSet<PromptPresetRecord> PromptPresets => Set<PromptPresetRecord>();
        public DbSet<BibleSnapshotRecord> BibleSnapshots => Set<BibleSnapshotRecord>();
        public DbSet<ExportTemplate> ExportTemplates => Set<ExportTemplate>();
        public DbSet<ExportPreset> ExportPresets => Set<ExportPreset>();
        public DbSet<ProjectExportSettings> ProjectExportSettings => Set<ProjectExportSettings>();
        public DbSet<SearchIndexEntryRecord> SearchIndexEntries => Set<SearchIndexEntryRecord>();
        public DbSet<ExternalIdentityLink> ExternalIdentityLinks => Set<ExternalIdentityLink>();

        public override int SaveChanges()
        {
            NormalizeStringIds();
            SyncDocumentUnixTimestamps();
            return base.SaveChanges();
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            NormalizeStringIds();
            SyncDocumentUnixTimestamps();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            NormalizeStringIds();
            SyncDocumentUnixTimestamps();
            return base.SaveChangesAsync(cancellationToken);
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            NormalizeStringIds();
            SyncDocumentUnixTimestamps();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            ValueConverter<DateTime?, DateTime?> nullableUtcDateTimeConverter = new(
                value => NormalizeUtc(value),
                value => NormalizeUtc(value));

            // SQL Server hardening:
            // - Explicit precision avoids provider-dependent decimal defaults.
            // - Explicit max lengths prevent nvarchar(max) for indexed/key columns.
            // - Composite indexes align with frequent query predicates/sorts.
            builder.Entity<UserProfile>(entity =>
            {
                entity.HasKey(profile => profile.UserId);
                entity.Property(profile => profile.UserId).HasMaxLength(128).IsRequired();
                entity.Property(profile => profile.Email).HasMaxLength(320);
                entity.Property(profile => profile.CreatedUtc).IsRequired();
                entity.Property(profile => profile.HasOnboarded).IsRequired();
                entity.Property(profile => profile.HasCompletedOnboarding).HasDefaultValue(false).IsRequired();
                entity.Property(profile => profile.OnboardingStep).HasDefaultValue(0).IsRequired();
                entity.Property(profile => profile.OnboardingStartedUtc);
                entity.Property(profile => profile.OnboardingCompletedUtc);
                entity.Property(profile => profile.PrimaryWritingIntent);
                entity.Property(profile => profile.UpdatedUtc).IsRequired();
                entity.HasIndex(profile => profile.HasCompletedOnboarding);
            });

            builder.Entity<UserEntitlement>(entity =>
            {
                entity.HasKey(entitlement => entitlement.UserId);
                entity.Property(entitlement => entitlement.UserId).HasMaxLength(128).IsRequired();
                entity.Property(entitlement => entitlement.PlanKey).IsRequired();
                entity.Property(entitlement => entitlement.SubscriptionStatus).IsRequired();
                entity.Property(entitlement => entitlement.CreatedAt).IsRequired();
                entity.Property(entitlement => entitlement.AiMonthlyTokenBudget).IsRequired();
                entity.Property(entitlement => entitlement.AiTokensUsedThisPeriod).IsRequired();
                entity.Property(entitlement => entitlement.PeriodStartUtc).IsRequired();
                entity.Property(entitlement => entitlement.StripeCustomerId);
                entity.Property(entitlement => entitlement.StripeSubscriptionId);
                entity.Property(entitlement => entitlement.StripePriceId);
                entity.Property(entitlement => entitlement.CurrentPeriodEndUtc);
                entity.Property(entitlement => entitlement.CancelAtPeriodEnd).HasDefaultValue(false).IsRequired();
                entity.Property(entitlement => entitlement.UpdatedUtc).IsRequired();
            });

            builder.Entity<AdminAuditEvent>(entity =>
            {
                entity.HasKey(audit => audit.Id);
                entity.Property(audit => audit.OccurredAtUtc).IsRequired();
                entity.Property(audit => audit.AdminUserId).HasMaxLength(128).IsRequired();
                entity.Property(audit => audit.Action).HasMaxLength(128).IsRequired();
                entity.Property(audit => audit.TargetUserId).HasMaxLength(128);
                entity.HasIndex(audit => audit.OccurredAtUtc);
                entity.HasIndex(audit => audit.AdminUserId);
                entity.HasIndex(audit => audit.TargetUserId);
                entity.HasIndex(audit => audit.Action);
            });

            builder.Entity<AdminRoleAssignment>(entity =>
            {
                entity.HasKey(item => item.UserId);
                entity.Property(item => item.UserId).HasMaxLength(128).IsRequired();
                entity.Property(item => item.AssignedByUserId).HasMaxLength(128);
                entity.Property(item => item.AssignedByEmail).HasMaxLength(320);
                entity.Property(item => item.AssignedUtc).IsRequired();
                entity.HasIndex(item => item.AssignedUtc);
            });

            builder.Entity<DeletedUserIdentity>(entity =>
            {
                entity.HasKey(item => item.UserId);
                entity.Property(item => item.UserId).HasMaxLength(128).IsRequired();
                entity.Property(item => item.Email).HasMaxLength(320);
                entity.Property(item => item.DisplayName).HasMaxLength(256);
                entity.Property(item => item.DeletedByAdminUserId).HasMaxLength(128);
                entity.Property(item => item.DeletedByAdminEmail).HasMaxLength(320);
                entity.Property(item => item.Reason).HasMaxLength(256);
                entity.Property(item => item.DeletedAtUtc).IsRequired();
                entity.HasIndex(item => item.DeletedAtUtc);
            });

            builder.Entity<ExternalIdentityLink>(entity =>
            {
                entity.HasKey(item => item.Id);
                entity.Property(item => item.UserId).HasMaxLength(128).IsRequired();
                entity.Property(item => item.Provider).HasMaxLength(64);
                entity.Property(item => item.Issuer).HasMaxLength(512);
                entity.Property(item => item.Subject).HasMaxLength(256);
                entity.Property(item => item.ObjectIdentifier).HasMaxLength(128);
                entity.Property(item => item.EmailAtLinkTime).HasMaxLength(320);
                entity.Property(item => item.CreatedUtc).IsRequired();
                entity.Property(item => item.LastSeenUtc).IsRequired();
                entity.HasIndex(item => item.UserId);
                entity.HasIndex(item => item.EmailAtLinkTime);
                entity.HasIndex(item => new { item.Provider, item.Issuer, item.Subject, item.ObjectIdentifier });
            });

            builder.Entity<StripeEventLog>(entity =>
            {
                entity.HasKey(x => x.StripeEventId);

                entity.Property(x => x.StripeEventId)
                    .HasMaxLength(100);

                entity.Property(x => x.Type)
                    .HasMaxLength(100)
                    .IsRequired();

                entity.Property(x => x.Status)
                    .HasMaxLength(50)
                    .IsRequired();

                entity.Property(x => x.Error)
                    .HasMaxLength(2000);

                entity.Property(x => x.UserId)
                    .HasMaxLength(100);

                entity.HasIndex(x => x.ReceivedUtc);
            });

            builder.Entity<Plan>(entity =>
            {
                entity.HasKey(plan => plan.PlanId);
                entity.Property(plan => plan.Key).IsRequired();
                entity.Property(plan => plan.Name).IsRequired();
                entity.HasIndex(plan => plan.Key).IsUnique();
            });

            builder.Entity<PlanEntitlement>(entity =>
            {
                entity.HasKey(entitlement => new { entitlement.PlanId, entitlement.Key });
                entity.Property(entitlement => entitlement.Key).IsRequired();
                entity.Property(entitlement => entitlement.Value).IsRequired();
                entity.HasOne(entitlement => entitlement.Plan)
                    .WithMany(plan => plan.Entitlements)
                    .HasForeignKey(entitlement => entitlement.PlanId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<UserPlanAssignment>(entity =>
            {
                entity.HasKey(assignment => new { assignment.UserId, assignment.PlanId });
                entity.Property(assignment => assignment.AssignedUtc).IsRequired();
                entity.Property(assignment => assignment.AssignedBy).IsRequired();
                entity.HasOne(assignment => assignment.Plan)
                    .WithMany()
                    .HasForeignKey(assignment => assignment.PlanId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<TokenAdjustment>(entity =>
            {
                entity.HasKey(adjustment => adjustment.Id);
                entity.Property(adjustment => adjustment.UserId).IsRequired();
                entity.Property(adjustment => adjustment.DeltaTokens).IsRequired();
                entity.Property(adjustment => adjustment.Reason).IsRequired();
                entity.Property(adjustment => adjustment.AdjustedBy).IsRequired();
                entity.Property(adjustment => adjustment.OccurredAtUtc).IsRequired();
                entity.HasIndex(adjustment => adjustment.UserId);
                entity.HasIndex(adjustment => adjustment.OccurredAtUtc);
            });

            builder.Entity<UsageEvent>(entity =>
            {
                entity.HasKey(usage => usage.Id);
                entity.Property(usage => usage.UserId).IsRequired();
                entity.Property(usage => usage.Kind).IsRequired();
                entity.Property(usage => usage.Provider).IsRequired();
                entity.Property(usage => usage.Model).IsRequired();
                entity.Property(usage => usage.TimestampUtc).IsRequired();
            });

            builder.Entity<UserEvent>(entity =>
            {
                entity.HasKey(item => item.Id);
                entity.Property(item => item.UserId).HasMaxLength(128).IsRequired();
                entity.Property(item => item.EventName).HasMaxLength(128).IsRequired();
                entity.Property(item => item.MetadataJson);
                entity.Property(item => item.CreatedUtc).IsRequired();
                entity.HasIndex(item => item.UserId);
                entity.HasIndex(item => item.EventName);
                entity.HasIndex(item => item.CreatedUtc);
            });

            builder.Entity<UsageAggregate>(entity =>
            {
                entity.HasKey(aggregate => new { aggregate.UserId, aggregate.PeriodStartUtc, aggregate.PeriodEndUtc, aggregate.Kind });
                entity.Property(aggregate => aggregate.UserId).IsRequired();
                entity.Property(aggregate => aggregate.Kind).IsRequired();
                entity.Property(aggregate => aggregate.UpdatedUtc).IsRequired();
            });

            builder.Entity<DocumentRecord>(entity =>
            {
                entity.HasKey(document => document.Id);
                entity.Property(document => document.ProjectId).IsRequired();
                entity.Property(document => document.OwnerUserId).HasMaxLength(128).IsRequired();
                entity.Property(document => document.Title).IsRequired();
                entity.Property(document => document.DocumentKind).IsRequired();
                entity.Property(document => document.CreatedAt).IsRequired();
                entity.Property(document => document.UpdatedAt).IsRequired();
                entity.Property(document => document.CreatedAtUnixSeconds).IsRequired();
                entity.Property(document => document.UpdatedAtUnixSeconds).IsRequired();
                entity.Property(document => document.IsArchived).IsRequired();
                entity.Property(document => document.ArchivedAt);
                entity.Property(document => document.DeletedAtUtc)
                    .HasConversion(nullableUtcDateTimeConverter);
                entity.HasIndex(document => document.ProjectId);
                entity.HasIndex(document => new { document.ProjectId, document.UpdatedAtUnixSeconds });
                entity.HasIndex(document => new { document.OwnerUserId, document.UpdatedAtUnixSeconds });
                entity.HasIndex(document => document.DocumentKind);
                entity.HasIndex(document => new { document.ProjectId, document.DocumentKind })
                    .IsUnique()
                    .HasFilter($"\"DocumentKind\" = {(int)DocumentKind.Manuscript}");
                entity.HasIndex(document => document.DeletedAtUtc);
                entity.HasIndex(document => document.IsArchived);
                entity.HasOne(document => document.Project)
                    .WithMany(project => project.Documents)
                    .HasForeignKey(document => document.ProjectId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasMany(document => document.Sections)
                    .WithOne(section => section.Document)
                    .HasForeignKey(section => section.DocumentId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasMany(document => document.OutlineNodes)
                    .WithOne(node => node.Document)
                    .HasForeignKey(node => node.DocumentId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<ProjectRecord>(entity =>
            {
                entity.HasKey(project => project.Id);
                entity.Property(project => project.OwnerUserId).HasMaxLength(128).IsRequired();
                entity.Property(project => project.Title).IsRequired();
                entity.Property(project => project.CreatedUtc).IsRequired();
                entity.Property(project => project.UpdatedUtc).IsRequired();
                entity.HasIndex(project => project.OwnerUserId);
                entity.HasIndex(project => project.UpdatedUtc);
                entity.HasMany(project => project.Documents)
                    .WithOne(document => document.Project)
                    .HasForeignKey(document => document.ProjectId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasMany(project => project.Nodes)
                    .WithOne(node => node.Project)
                    .HasForeignKey(node => node.ProjectId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<ProjectNodeRecord>(entity =>
            {
                entity.HasKey(node => node.Id);
                entity.Property(node => node.ProjectId).IsRequired();
                entity.Property(node => node.NodeType).IsRequired();
                entity.Property(node => node.Title).IsRequired();
                entity.Property(node => node.OrderIndex).IsRequired();
                entity.Property(node => node.WordCountCache).IsRequired();
                entity.Property(node => node.UpdatedUtc).IsRequired();
                entity.HasIndex(node => new { node.ProjectId, node.ParentId, node.OrderIndex });
                entity.HasIndex(node => node.LinkedSectionId);
                entity.HasOne(node => node.Parent)
                    .WithMany(node => node.Children)
                    .HasForeignKey(node => node.ParentId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(node => node.LinkedSection)
                    .WithMany()
                    .HasForeignKey(node => node.LinkedSectionId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            builder.Entity<ProjectGoalRecord>(entity =>
            {
                entity.HasKey(goal => goal.ProjectId);
                entity.Property(goal => goal.DailyTargetWords).IsRequired();
                entity.Property(goal => goal.WeeklyTargetWords).IsRequired();
                entity.Property(goal => goal.Timezone).IsRequired();
                entity.Property(goal => goal.UpdatedUtc).IsRequired();
                entity.HasOne(goal => goal.Project)
                    .WithOne(project => project.Goal)
                    .HasForeignKey<ProjectGoalRecord>(goal => goal.ProjectId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<ProjectProgressDailyRecord>(entity =>
            {
                entity.HasKey(day => new { day.ProjectId, day.Date });
                entity.Property(day => day.Date).HasMaxLength(10).IsRequired();
                entity.Property(day => day.WordsDelta).IsRequired();
                entity.Property(day => day.UpdatedUtc).IsRequired();
                entity.HasOne(day => day.Project)
                    .WithMany(project => project.ProgressDays)
                    .HasForeignKey(day => day.ProjectId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<ProjectProgressEventRecord>(entity =>
            {
                entity.HasKey(item => item.Id);
                entity.Property(item => item.EventKey).IsRequired();
                entity.Property(item => item.Date).HasMaxLength(10).IsRequired();
                entity.Property(item => item.WordsDelta).IsRequired();
                entity.Property(item => item.CreatedUtc).IsRequired();
                entity.HasIndex(item => new { item.ProjectId, item.EventKey }).IsUnique();
                entity.HasIndex(item => item.ProjectId);
                entity.HasOne(item => item.Project)
                    .WithMany()
                    .HasForeignKey(item => item.ProjectId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<ProjectMilestoneRecord>(entity =>
            {
                entity.HasKey(milestone => milestone.Id);
                entity.Property(milestone => milestone.Title).IsRequired();
                entity.Property(milestone => milestone.Status).IsRequired();
                entity.Property(milestone => milestone.UpdatedUtc).IsRequired();
                entity.HasIndex(milestone => milestone.ProjectId);
                entity.HasIndex(milestone => milestone.Status);
                entity.HasOne(milestone => milestone.Project)
                    .WithMany(project => project.Milestones)
                    .HasForeignKey(milestone => milestone.ProjectId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<WritingSessionRecord>(entity =>
            {
                entity.HasKey(session => session.Id);
                entity.Property(session => session.StartedUtc)
                    .HasConversion(
                        value => value,
                        value => DateTime.SpecifyKind(value, DateTimeKind.Utc))
                    .IsRequired();
                entity.Property(session => session.EndedUtc)
                    .HasConversion(
                        value => value,
                        value => value.HasValue
                            ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
                            : (DateTime?)null);
                entity.Property(session => session.DurationSeconds).IsRequired();
                entity.Property(session => session.WordsDelta).IsRequired();
                entity.Property(session => session.StartWordCount).IsRequired();
                entity.HasIndex(session => new { session.ProjectId, session.StartedUtc });
                entity.HasOne(session => session.Project)
                    .WithMany(project => project.Sessions)
                    .HasForeignKey(session => session.ProjectId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<DocumentOutlineNodeRecord>(entity =>
            {
                entity.HasKey(node => node.Id);
                entity.Property(node => node.DocumentId).IsRequired();
                entity.Property(node => node.Title).IsRequired();
                entity.Property(node => node.Order).IsRequired();
                entity.HasIndex(node => new { node.DocumentId, node.ParentId, node.Order });
                entity.Property(node => node.MetadataJson);
                entity.HasOne(node => node.Parent)
                    .WithMany(node => node.Children)
                    .HasForeignKey(node => node.ParentId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(node => node.LinkedSection)
                    .WithMany()
                    .HasForeignKey(node => node.LinkedSectionId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            builder.Entity<SectionRecord>(entity =>
            {
                entity.HasKey(section => section.Id);
                entity.Property(section => section.DocumentId).IsRequired();
                entity.Property(section => section.Title).IsRequired();
                entity.Property(section => section.OrderIndex).IsRequired();
                entity.Property(section => section.CreatedAt).IsRequired();
                entity.Property(section => section.UpdatedAt).IsRequired();
                entity.HasIndex(section => new { section.DocumentId, section.OrderIndex });
                entity.HasMany(section => section.Pages)
                    .WithOne(page => page.Section)
                    .HasForeignKey(page => page.SectionId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<PageRecord>(entity =>
            {
                entity.HasKey(page => page.Id);
                entity.Property(page => page.DocumentId).IsRequired();
                entity.Property(page => page.SectionId).IsRequired();
                entity.Property(page => page.Title).IsRequired();
                entity.Property(page => page.Content).IsRequired();
                entity.Property(page => page.OrderIndex).IsRequired();
                entity.Property(page => page.CreatedAt).IsRequired();
                entity.Property(page => page.UpdatedAt).IsRequired();
                entity.HasIndex(page => new { page.SectionId, page.OrderIndex });
                entity.HasIndex(page => page.DocumentId);
                entity.HasOne(page => page.Document)
                    .WithMany()
                    .HasForeignKey(page => page.DocumentId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<PageAnnotationRecord>(entity =>
            {
                entity.HasKey(annotation => annotation.Id);
                entity.Property(annotation => annotation.DocumentId).IsRequired();
                entity.Property(annotation => annotation.PageId).IsRequired();
                entity.Property(annotation => annotation.Kind).IsRequired();
                entity.Property(annotation => annotation.Status).IsRequired();
                entity.Property(annotation => annotation.AnchorFrom).IsRequired();
                entity.Property(annotation => annotation.AnchorTo).IsRequired();
                entity.Property(annotation => annotation.AnchorText).IsRequired();
                entity.Property(annotation => annotation.Content).IsRequired();
                entity.Property(annotation => annotation.AuthorUserId).IsRequired();
                entity.Property(annotation => annotation.CreatedAt).IsRequired();
                entity.Property(annotation => annotation.ResolvedAt);
                entity.HasIndex(annotation => annotation.PageId);
                entity.HasIndex(annotation => annotation.DocumentId);
                entity.HasIndex(annotation => annotation.Status);
                entity.HasIndex(annotation => annotation.Kind);
                entity.HasIndex(annotation => annotation.CreatedAt);
                entity.HasOne(annotation => annotation.Page)
                    .WithMany()
                    .HasForeignKey(annotation => annotation.PageId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(annotation => annotation.Document)
                    .WithMany()
                    .HasForeignKey(annotation => annotation.DocumentId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<PageQualityIssueRecord>(entity =>
            {
                entity.HasKey(issue => issue.Id);
                entity.Property(issue => issue.DocumentId).IsRequired();
                entity.Property(issue => issue.PageId).IsRequired();
                entity.Property(issue => issue.Scope).IsRequired();
                entity.Property(issue => issue.IssueKey).HasMaxLength(128).IsRequired();
                entity.Property(issue => issue.RuleId).IsRequired();
                entity.Property(issue => issue.Kind).IsRequired();
                entity.Property(issue => issue.Severity).IsRequired();
                entity.Property(issue => issue.Message).IsRequired();
                entity.Property(issue => issue.ContentHash).IsRequired();
                entity.Property(issue => issue.CreatedAt).IsRequired();
                entity.HasIndex(issue => issue.PageId);
                entity.HasIndex(issue => issue.DocumentId);
                entity.HasIndex(issue => issue.Scope);
                entity.HasIndex(issue => issue.ContentHash);
                entity.HasIndex(issue => issue.IssueKey);
                entity.HasOne(issue => issue.Page)
                    .WithMany()
                    .HasForeignKey(issue => issue.PageId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(issue => issue.Document)
                    .WithMany()
                    .HasForeignKey(issue => issue.DocumentId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<PageQualityIssueDismissalRecord>(entity =>
            {
                entity.HasKey(dismissal => new { dismissal.UserId, dismissal.PageId, dismissal.IssueKey });
                entity.Property(dismissal => dismissal.UserId).HasMaxLength(128).IsRequired();
                entity.Property(dismissal => dismissal.PageId).IsRequired();
                entity.Property(dismissal => dismissal.IssueKey).HasMaxLength(128).IsRequired();
                entity.Property(dismissal => dismissal.DismissedAt).IsRequired();
                entity.HasIndex(dismissal => dismissal.PageId);
                entity.HasOne(dismissal => dismissal.Page)
                    .WithMany()
                    .HasForeignKey(dismissal => dismissal.PageId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<PageVersionRecord>(entity =>
            {
                entity.HasKey(version => version.Id);
                entity.Property(version => version.PageId).IsRequired();
                entity.Property(version => version.DocumentId).IsRequired();
                entity.Property(version => version.CreatedAt).IsRequired();
                entity.Property(version => version.Reason).IsRequired();
                entity.Property(version => version.ContentCompressed).IsRequired();
                entity.Property(version => version.ContentTextHash).IsRequired();
                entity.Property(version => version.SizeBytes).IsRequired();
                entity.Property(version => version.WordCount).IsRequired();
                entity.HasIndex(version => version.PageId);
                entity.HasIndex(version => version.DocumentId);
                entity.HasIndex(version => version.CreatedAt);
                entity.HasOne(version => version.Page)
                    .WithMany()
                    .HasForeignKey(version => version.PageId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<PageNoteRecord>(entity =>
            {
                entity.HasKey(note => note.PageId);
                entity.Property(note => note.Notes).IsRequired();
                entity.Property(note => note.UpdatedAt).IsRequired();
                entity.HasOne(note => note.Page)
                    .WithOne()
                    .HasForeignKey<PageNoteRecord>(note => note.PageId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<SectionNoteRecord>(entity =>
            {
                entity.HasKey(note => note.SectionId);
                entity.Property(note => note.NotesText).IsRequired();
                entity.Property(note => note.UpdatedAtUtc).IsRequired();
                entity.HasOne(note => note.Section)
                    .WithOne()
                    .HasForeignKey<SectionNoteRecord>(note => note.SectionId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<SectionSceneCardRecord>(entity =>
            {
                entity.HasKey(card => card.SectionId);
                entity.Property(card => card.UpdatedUtc).IsRequired();
                entity.Property(card => card.TimeRef).HasMaxLength(120);
                entity.HasOne(card => card.Section)
                    .WithOne(section => section.SceneCard)
                    .HasForeignKey<SectionSceneCardRecord>(card => card.SectionId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<SceneContentRecord>(entity =>
            {
                entity.HasKey(item => item.SceneNodeId);
                entity.Property(item => item.ContentJson).IsRequired();
                entity.Property(item => item.UpdatedAtUtc).IsRequired();
                entity.HasOne(item => item.SceneNode)
                    .WithOne()
                    .HasForeignKey<SceneContentRecord>(item => item.SceneNodeId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<SceneNoteRecord>(entity =>
            {
                entity.HasKey(item => item.SceneNodeId);
                entity.Property(item => item.NotesText).IsRequired();
                entity.Property(item => item.UpdatedAtUtc).IsRequired();
                entity.HasOne(item => item.SceneNode)
                    .WithOne()
                    .HasForeignKey<SceneNoteRecord>(item => item.SceneNodeId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<SceneCardRecord>(entity =>
            {
                entity.HasKey(item => item.SceneNodeId);
                entity.Property(item => item.UpdatedAtUtc).IsRequired();
                entity.Property(item => item.TimeRef).HasMaxLength(120);
                entity.HasOne(item => item.SceneNode)
                    .WithOne()
                    .HasForeignKey<SceneCardRecord>(item => item.SceneNodeId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<SceneAnnotationRecord>(entity =>
            {
                entity.HasKey(item => item.Id);
                entity.Property(item => item.SceneNodeId).IsRequired();
                entity.Property(item => item.Kind).IsRequired();
                entity.Property(item => item.Status).IsRequired();
                entity.Property(item => item.AnchorFrom).IsRequired();
                entity.Property(item => item.AnchorTo).IsRequired();
                entity.Property(item => item.AnchorText).IsRequired();
                entity.Property(item => item.Content).IsRequired();
                entity.Property(item => item.AuthorUserId).IsRequired();
                entity.Property(item => item.CreatedAt).IsRequired();
                entity.Property(item => item.ResolvedAt);
                entity.HasIndex(item => item.SceneNodeId);
                entity.HasIndex(item => item.Status);
                entity.HasIndex(item => item.Kind);
                entity.HasIndex(item => item.CreatedAt);
                entity.HasOne(item => item.SceneNode)
                    .WithMany()
                    .HasForeignKey(item => item.SceneNodeId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<SceneQualityIssueRecord>(entity =>
            {
                entity.HasKey(item => item.Id);
                entity.Property(item => item.SceneNodeId).IsRequired();
                entity.Property(item => item.Scope).IsRequired();
                entity.Property(item => item.IssueKey).HasMaxLength(128).IsRequired();
                entity.Property(item => item.RuleId).IsRequired();
                entity.Property(item => item.Kind).IsRequired();
                entity.Property(item => item.Severity).IsRequired();
                entity.Property(item => item.Message).IsRequired();
                entity.Property(item => item.ContentHash).IsRequired();
                entity.Property(item => item.CreatedAt).IsRequired();
                entity.HasIndex(item => item.SceneNodeId);
                entity.HasIndex(item => item.Scope);
                entity.HasIndex(item => item.ContentHash);
                entity.HasIndex(item => item.IssueKey);
                entity.HasOne(item => item.SceneNode)
                    .WithMany()
                    .HasForeignKey(item => item.SceneNodeId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<SceneVersionRecord>(entity =>
            {
                entity.HasKey(item => item.Id);
                entity.Property(item => item.SceneNodeId).IsRequired();
                entity.Property(item => item.CreatedAt).IsRequired();
                entity.Property(item => item.Reason).IsRequired();
                entity.Property(item => item.ContentCompressed).IsRequired();
                entity.Property(item => item.ContentTextHash).IsRequired();
                entity.Property(item => item.SizeBytes).IsRequired();
                entity.Property(item => item.WordCount).IsRequired();
                entity.HasIndex(item => item.SceneNodeId);
                entity.HasIndex(item => item.CreatedAt);
                entity.HasOne(item => item.SceneNode)
                    .WithMany()
                    .HasForeignKey(item => item.SceneNodeId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<OutlineTemplateRecord>(entity =>
            {
                entity.HasKey(template => template.Id);
                entity.Property(template => template.OwnerUserId).HasMaxLength(128).IsRequired();
                entity.Property(template => template.Name).IsRequired();
                entity.Property(template => template.TemplateJson).IsRequired();
                entity.Property(template => template.CreatedUtc).IsRequired();
                entity.Property(template => template.UpdatedUtc).IsRequired();
                entity.HasIndex(template => template.OwnerUserId);
                entity.HasIndex(template => template.UpdatedUtc);
            });

            builder.Entity<DocumentOutlineRecord>(entity =>
            {
                entity.HasKey(outline => outline.DocumentId);
                entity.Property(outline => outline.Outline).IsRequired();
                entity.Property(outline => outline.UpdatedAt).IsRequired();
                entity.HasOne(outline => outline.Document)
                    .WithOne()
                    .HasForeignKey<DocumentOutlineRecord>(outline => outline.DocumentId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<DocumentSynopsisRecord>(entity =>
            {
                entity.HasKey(synopsis => synopsis.DocumentId);
                entity.Property(synopsis => synopsis.Logline).IsRequired();
                entity.Property(synopsis => synopsis.Premise).IsRequired();
                entity.Property(synopsis => synopsis.Theme).IsRequired();
                entity.Property(synopsis => synopsis.ProtagonistArc).IsRequired();
                entity.Property(synopsis => synopsis.CentralConflict).IsRequired();
                entity.Property(synopsis => synopsis.Stakes).IsRequired();
                entity.Property(synopsis => synopsis.Setting).IsRequired();
                entity.Property(synopsis => synopsis.EndingIntent).IsRequired();
                entity.Property(synopsis => synopsis.OpenQuestions).IsRequired();
                entity.Property(synopsis => synopsis.Notes).IsRequired();
                entity.Property(synopsis => synopsis.UpdatedAt).IsRequired();
                entity.HasOne(synopsis => synopsis.Document)
                    .WithOne()
                    .HasForeignKey<DocumentSynopsisRecord>(synopsis => synopsis.DocumentId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(synopsis => synopsis.UpdatedAt);
            });

            builder.Entity<DocumentGlossaryEntryRecord>(entity =>
            {
                entity.HasKey(entry => entry.Id);
                entity.Property(entry => entry.DocumentId).IsRequired();
                entity.Property(entry => entry.Term).IsRequired();
                entity.Property(entry => entry.NormalizedTerm).HasMaxLength(256).IsRequired();
                entity.Property(entry => entry.CreatedAt).IsRequired();
                entity.Property(entry => entry.UpdatedAt).IsRequired();
                entity.HasIndex(entry => entry.DocumentId);
                entity.HasIndex(entry => entry.NormalizedTerm);
                entity.HasOne(entry => entry.Document)
                    .WithMany()
                    .HasForeignKey(entry => entry.DocumentId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<AiActionHistoryEntryRecord>(entity =>
            {
                entity.HasKey(entry => entry.Id);
                entity.Property(entry => entry.OwnerUserId).HasMaxLength(128).IsRequired();
                entity.Property(entry => entry.ActionKey).HasMaxLength(128).IsRequired();
                entity.Property(entry => entry.RequestJson).IsRequired();
                entity.Property(entry => entry.ResultJson).IsRequired();
                entity.Property(entry => entry.CreatedAt).IsRequired();
                entity.HasIndex(entry => entry.OwnerUserId);
                entity.HasIndex(entry => entry.DocumentId);
                entity.HasIndex(entry => entry.ActionKey);
                entity.HasIndex(entry => entry.CreatedAt);
            });

            builder.Entity<AiActionAppliedEventRecord>(entity =>
            {
                entity.HasKey(applied => applied.Id);
                entity.Property(applied => applied.OwnerUserId).HasMaxLength(128).IsRequired();
                entity.Property(applied => applied.AppliedAt).IsRequired();
                entity.Property(applied => applied.BeforeContent);
                entity.Property(applied => applied.AfterContent);
                entity.HasIndex(applied => applied.OwnerUserId);
                entity.HasIndex(applied => applied.HistoryEntryId);
                entity.HasIndex(applied => applied.AppliedAt);
                entity.HasIndex(applied => applied.UndoneAt);
                entity.HasOne(applied => applied.HistoryEntry)
                    .WithMany()
                    .HasForeignKey(applied => applied.HistoryEntryId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<PromptPresetRecord>(entity =>
            {
                entity.HasKey(preset => preset.Id);
                entity.Property(preset => preset.OwnerUserId).HasMaxLength(128).IsRequired();
                entity.Property(preset => preset.Name).IsRequired();
                entity.Property(preset => preset.Kind).HasMaxLength(64).IsRequired();
                entity.Property(preset => preset.ParametersJson).IsRequired();
                entity.Property(preset => preset.CreatedUtc).IsRequired();
                entity.Property(preset => preset.UpdatedUtc).IsRequired();
                entity.HasIndex(preset => preset.OwnerUserId);
                entity.HasIndex(preset => new { preset.OwnerUserId, preset.ProjectId });
                entity.HasIndex(preset => new { preset.OwnerUserId, preset.Kind });
                entity.HasIndex(preset => preset.UpdatedUtc);
            });

            builder.Entity<BibleSnapshotRecord>(entity =>
            {
                entity.HasKey(snapshot => snapshot.Id);
                entity.Property(snapshot => snapshot.DocumentId).IsRequired();
                entity.Property(snapshot => snapshot.BibleType).IsRequired();
                entity.Property(snapshot => snapshot.SchemaVersion).IsRequired();
                entity.Property(snapshot => snapshot.ContentJson).IsRequired();
                entity.Property(snapshot => snapshot.CreatedUtc).IsRequired();
                entity.Property(snapshot => snapshot.UpdatedUtc).IsRequired();
                entity.Property(snapshot => snapshot.LastRefreshSourceHash).IsRequired();
                entity.Property(snapshot => snapshot.LastRefreshStatsJson).IsRequired();
                entity.Property(snapshot => snapshot.LastRefreshCursorJson).IsRequired();
                entity.HasIndex(snapshot => new { snapshot.DocumentId, snapshot.BibleType }).IsUnique();
                entity.HasIndex(snapshot => snapshot.LastRefreshUtc);
                entity.HasOne<DocumentRecord>()
                    .WithMany()
                    .HasForeignKey(snapshot => snapshot.DocumentId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<ExportTemplate>(entity =>
            {
                entity.HasKey(template => template.Id);
                entity.Property(template => template.OwnerUserId).HasMaxLength(128).IsRequired();
                entity.Property(template => template.Name).IsRequired();
                entity.Property(template => template.PresetKey).HasMaxLength(64);
                entity.Property(template => template.FontFamily).IsRequired();
                entity.Property(template => template.LineHeight).HasPrecision(5, 2);
                entity.Property(template => template.CreatedAt).IsRequired();
                entity.Property(template => template.UpdatedAt).IsRequired();
                entity.HasIndex(template => template.OwnerUserId);
                entity.HasIndex(template => new { template.OwnerUserId, template.PresetKey });
            });

            builder.Entity<ExportPreset>(entity =>
            {
                entity.HasKey(preset => preset.Id);
                entity.Property(preset => preset.OwnerUserId).HasMaxLength(128).IsRequired();
                entity.Property(preset => preset.Name).IsRequired();
                entity.Property(preset => preset.SettingsJson).IsRequired();
                entity.Property(preset => preset.CreatedAt).IsRequired();
                entity.Property(preset => preset.UpdatedAt).IsRequired();
                entity.HasIndex(preset => preset.OwnerUserId);
                entity.HasIndex(preset => new { preset.OwnerUserId, preset.IsGlobalDefault });
                entity.HasIndex(preset => preset.UpdatedAt);
            });

            builder.Entity<ProjectExportSettings>(entity =>
            {
                entity.HasKey(settings => new { settings.DocumentId, settings.UserId });
                entity.Property(settings => settings.UserId).HasMaxLength(128).IsRequired();
                entity.Property(settings => settings.UpdatedAt).IsRequired();
                entity.HasIndex(settings => settings.UserId);
                entity.HasOne<DocumentRecord>()
                    .WithMany()
                    .HasForeignKey(settings => settings.DocumentId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne<ExportPreset>()
                    .WithMany()
                    .HasForeignKey(settings => settings.DefaultPresetId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            builder.Entity<SearchIndexEntryRecord>(entity =>
            {
                entity.ToTable("SearchIndexEntries");
                entity.HasKey(entry => entry.Id);
                entity.Property(entry => entry.EntityType).HasMaxLength(32).IsRequired();
                entity.Property(entry => entry.EntityId).HasMaxLength(64).IsRequired();
                entity.Property(entry => entry.DocumentId).HasMaxLength(64).IsRequired();
                entity.Property(entry => entry.ProjectId).HasMaxLength(64).IsRequired();
                entity.Property(entry => entry.SectionId).HasMaxLength(64);
                entity.Property(entry => entry.PageId).HasMaxLength(64);
                entity.Property(entry => entry.Title).IsRequired();
                entity.Property(entry => entry.Content).IsRequired();
                entity.Property(entry => entry.UpdatedAt).IsRequired();
                entity.HasIndex(entry => new { entry.EntityType, entry.EntityId, entry.DocumentId }).IsUnique();
                entity.HasIndex(entry => entry.DocumentId);
                entity.HasIndex(entry => entry.ProjectId);
            });

            SeedSubscriptionData(builder);
        }

        private void SyncDocumentUnixTimestamps()
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            foreach (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<DocumentRecord> entry in ChangeTracker.Entries<DocumentRecord>())
            {
                if (entry.State == EntityState.Added)
                {
                    if (entry.Entity.CreatedAt == default)
                    {
                        entry.Entity.CreatedAt = now;
                    }

                    if (entry.Entity.UpdatedAt == default)
                    {
                        entry.Entity.UpdatedAt = entry.Entity.CreatedAt;
                    }
                }

                if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
                {
                    entry.Entity.DeletedAtUtc = NormalizeUtc(entry.Entity.DeletedAtUtc);
                    entry.Entity.CreatedAtUnixSeconds = entry.Entity.CreatedAt.ToUnixTimeSeconds();
                    entry.Entity.UpdatedAtUnixSeconds = entry.Entity.UpdatedAt.ToUnixTimeSeconds();
                }
            }
        }

        private static DateTime? NormalizeUtc(DateTime? value)
        {
            if (!value.HasValue)
            {
                return null;
            }

            return value.Value.Kind switch
            {
                DateTimeKind.Utc => value.Value,
                DateTimeKind.Local => value.Value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
            };
        }

        private void NormalizeStringIds()
        {
            foreach (var entry in ChangeTracker.Entries()
                         .Where(item => item.State is EntityState.Added or EntityState.Modified))
            {
                foreach (var property in entry.Properties)
                {
                    if (property.Metadata.ClrType != typeof(string))
                    {
                        continue;
                    }

                    if (!ShouldNormalizeIdProperty(property.Metadata.Name))
                    {
                        continue;
                    }

                    if (property.CurrentValue is not string value)
                    {
                        continue;
                    }

                    if (!IdNorm.TryNormGuidString(value, out string normalized))
                    {
                        continue;
                    }

                    if (!string.Equals(value, normalized, StringComparison.Ordinal))
                    {
                        property.CurrentValue = normalized;
                    }
                }
            }
        }

        private static bool ShouldNormalizeIdProperty(string propertyName)
        {
            return propertyName.EndsWith("Id", StringComparison.Ordinal)
                   || propertyName.EndsWith("UserId", StringComparison.Ordinal)
                   || propertyName.EndsWith("ProjectId", StringComparison.Ordinal)
                   || propertyName.EndsWith("DocumentId", StringComparison.Ordinal)
                   || propertyName.EndsWith("SectionId", StringComparison.Ordinal)
                   || propertyName.EndsWith("PageId", StringComparison.Ordinal);
        }

        private static void SeedSubscriptionData(ModelBuilder builder)
        {
            DateTime seededUtc = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            Guid freePlanId = Guid.Parse("5f4d2c6f-98fd-4a26-9c0f-0a2a1f2d7c4b");
            Guid standardPlanId = Guid.Parse("83d8f8f0-6d2f-4d68-b7df-4192dce1a6f5");
            Guid professionalPlanId = Guid.Parse("6d1d34ef-2a0f-4b24-8b3f-7f3f4a4b9f0b");

            builder.Entity<Plan>().HasData(
                new Plan
                {
                    PlanId = freePlanId,
                    Key = "free",
                    Name = "Free",
                    IsActive = true
                },
                new Plan
                {
                    PlanId = standardPlanId,
                    Key = "standard",
                    Name = "Standard",
                    IsActive = true
                },
                new Plan
                {
                    PlanId = professionalPlanId,
                    Key = "professional",
                    Name = "Professional",
                    IsActive = true
                });

            builder.Entity<PlanEntitlement>().HasData(
                new PlanEntitlement { PlanId = freePlanId, Key = "ai.enabled", Value = "false" },
                new PlanEntitlement { PlanId = standardPlanId, Key = "ai.enabled", Value = "true" },
                new PlanEntitlement { PlanId = professionalPlanId, Key = "ai.enabled", Value = "true" },
                new PlanEntitlement { PlanId = freePlanId, Key = "ai.monthly_tokens", Value = "0" },
                new PlanEntitlement { PlanId = standardPlanId, Key = "ai.monthly_tokens", Value = "200000" },
                new PlanEntitlement { PlanId = professionalPlanId, Key = "ai.monthly_tokens", Value = "1000000" },
                new PlanEntitlement { PlanId = freePlanId, Key = "export.pdf", Value = "false" },
                new PlanEntitlement { PlanId = standardPlanId, Key = "export.pdf", Value = "true" },
                new PlanEntitlement { PlanId = professionalPlanId, Key = "export.pdf", Value = "true" },
                new PlanEntitlement { PlanId = freePlanId, Key = "ai.images.cover", Value = "false" },
                new PlanEntitlement { PlanId = standardPlanId, Key = "ai.images.cover", Value = "false" },
                new PlanEntitlement { PlanId = professionalPlanId, Key = "ai.images.cover", Value = "true" },
                new PlanEntitlement { PlanId = freePlanId, Key = "history.enabled", Value = "true" },
                new PlanEntitlement { PlanId = standardPlanId, Key = "history.enabled", Value = "true" },
                new PlanEntitlement { PlanId = professionalPlanId, Key = "history.enabled", Value = "true" },
                new PlanEntitlement { PlanId = freePlanId, Key = "history.max_versions", Value = "5" },
                new PlanEntitlement { PlanId = standardPlanId, Key = "history.retention_days", Value = "30" },
                new PlanEntitlement { PlanId = professionalPlanId, Key = "history.retention_days", Value = "30" }
            );

            builder.Entity<UserProfile>().HasData(
                new UserProfile
                {
                    UserId = "seed-system",
                    DisplayName = "System",
                    CreatedUtc = seededUtc,
                    HasOnboarded = true,
                    HasCompletedOnboarding = true,
                    OnboardingStep = 0,
                    UpdatedUtc = seededUtc
                }
            );
        }
    }

    public sealed class SearchIndexEntryRecord
    {
        public long Id { get; set; }
        public string EntityType { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
        public string DocumentId { get; set; } = string.Empty;
        public string ProjectId { get; set; } = string.Empty;
        public string? SectionId { get; set; }
        public string? PageId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string UpdatedAt { get; set; } = string.Empty;
    }
}
