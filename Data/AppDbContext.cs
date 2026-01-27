using Microsoft.EntityFrameworkCore;
using WriterApp.Data.AI;
using WriterApp.Data.Documents;
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
        public DbSet<PageNoteRecord> PageNotes => Set<PageNoteRecord>();
        public DbSet<DocumentOutlineRecord> DocumentOutlines => Set<DocumentOutlineRecord>();
        public DbSet<AiActionHistoryEntryRecord> AiActionHistoryEntries => Set<AiActionHistoryEntryRecord>();
        public DbSet<AiActionAppliedEventRecord> AiActionAppliedEvents => Set<AiActionAppliedEventRecord>();

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
                entity.HasMany(document => document.Sections)
                    .WithOne(section => section.Document)
                    .HasForeignKey(section => section.DocumentId)
                    .OnDelete(DeleteBehavior.Cascade);
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
                new PlanEntitlement { PlanId = professionalPlanId, Key = "ai.images.cover", Value = "true" }
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
