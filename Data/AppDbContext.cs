using Microsoft.EntityFrameworkCore;
using WriterApp.Data.AI;
using WriterApp.Data.Continuity;
using WriterApp.Data.Documents;
using WriterApp.Data.Exporting;
using WriterApp.Data.Subscriptions;
using WriterApp.Data.Usage;

namespace WriterApp.Data
{
    public sealed class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
        public DbSet<Plan> Plans => Set<Plan>();
        public DbSet<PlanEntitlement> PlanEntitlements => Set<PlanEntitlement>();
        public DbSet<UserPlanAssignment> UserPlanAssignments => Set<UserPlanAssignment>();
        public DbSet<UsageEvent> UsageEvents => Set<UsageEvent>();
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
        public DbSet<SectionSceneCardRecord> SectionSceneCards => Set<SectionSceneCardRecord>();
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

        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.Entity<UserProfile>(entity =>
            {
                entity.HasKey(profile => profile.UserId);
                entity.Property(profile => profile.CreatedUtc).IsRequired();
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

            builder.Entity<UsageEvent>(entity =>
            {
                entity.HasKey(usage => usage.Id);
                entity.Property(usage => usage.UserId).IsRequired();
                entity.Property(usage => usage.Kind).IsRequired();
                entity.Property(usage => usage.Provider).IsRequired();
                entity.Property(usage => usage.Model).IsRequired();
                entity.Property(usage => usage.TimestampUtc).IsRequired();
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
                entity.Property(document => document.OwnerUserId).IsRequired();
                entity.Property(document => document.Title).IsRequired();
                entity.Property(document => document.CreatedAt).IsRequired();
                entity.Property(document => document.UpdatedAt).IsRequired();
                entity.Property(document => document.IsArchived).IsRequired();
                entity.Property(document => document.ArchivedAt);
                entity.Property(document => document.DeletedAt);
                entity.HasIndex(document => document.DeletedAt);
                entity.HasIndex(document => document.IsArchived);
                entity.HasMany(document => document.Sections)
                    .WithOne(section => section.Document)
                    .HasForeignKey(section => section.DocumentId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasMany(document => document.OutlineNodes)
                    .WithOne(node => node.Document)
                    .HasForeignKey(node => node.DocumentId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<DocumentOutlineNodeRecord>(entity =>
            {
                entity.HasKey(node => node.Id);
                entity.Property(node => node.DocumentId).IsRequired();
                entity.Property(node => node.Title).IsRequired();
                entity.Property(node => node.Order).IsRequired();
                entity.HasIndex(node => new { node.DocumentId, node.ParentId, node.Order });
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
                entity.Property(issue => issue.IssueKey).IsRequired();
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
                entity.Property(dismissal => dismissal.UserId).IsRequired();
                entity.Property(dismissal => dismissal.PageId).IsRequired();
                entity.Property(dismissal => dismissal.IssueKey).IsRequired();
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

            builder.Entity<SectionSceneCardRecord>(entity =>
            {
                entity.HasKey(card => card.SectionId);
                entity.Property(card => card.UpdatedUtc).IsRequired();
                entity.HasOne(card => card.Section)
                    .WithOne(section => section.SceneCard)
                    .HasForeignKey<SectionSceneCardRecord>(card => card.SectionId)
                    .OnDelete(DeleteBehavior.Cascade);
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
                entity.Property(entry => entry.NormalizedTerm).IsRequired();
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
                entity.Property(entry => entry.OwnerUserId).IsRequired();
                entity.Property(entry => entry.ActionKey).IsRequired();
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
                entity.Property(applied => applied.OwnerUserId).IsRequired();
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
                entity.Property(preset => preset.OwnerUserId).IsRequired();
                entity.Property(preset => preset.Name).IsRequired();
                entity.Property(preset => preset.Kind).IsRequired();
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
                entity.Property(template => template.OwnerUserId).IsRequired();
                entity.Property(template => template.Name).IsRequired();
                entity.Property(template => template.FontFamily).IsRequired();
                entity.Property(template => template.CreatedAt).IsRequired();
                entity.Property(template => template.UpdatedAt).IsRequired();
                entity.HasIndex(template => template.OwnerUserId);
                entity.HasIndex(template => new { template.OwnerUserId, template.PresetKey });
            });

            builder.Entity<ExportPreset>(entity =>
            {
                entity.HasKey(preset => preset.Id);
                entity.Property(preset => preset.OwnerUserId).IsRequired();
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
                entity.Property(settings => settings.UserId).IsRequired();
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

            SeedSubscriptionData(builder);
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
                    CreatedUtc = seededUtc
                }
            );
        }
    }
}
