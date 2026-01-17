using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WriterApp.Data.Subscriptions;
using WriterApp.Data.Usage;

namespace WriterApp.Data
{
    public sealed class AppDbContext : IdentityDbContext<ApplicationUser>
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

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

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
