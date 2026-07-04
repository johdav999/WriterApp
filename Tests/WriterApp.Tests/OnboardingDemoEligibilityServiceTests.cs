using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WriterApp.AI.Actions;
using WriterApp.Application.AI;
using WriterApp.Application.Documents;
using WriterApp.Data;
using WriterApp.Data.Documents;
using WriterApp.Data.Subscriptions;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class OnboardingDemoEligibilityServiceTests
    {
        [Fact]
        public async Task EvaluateSectionAiDemoAsync_AllowsFreeUser_WhenDemoMetadataPresent()
        {
            await using AppDbContext db = BuildDbContext();
            SeedDocumentGraph(db, out Guid documentId, out Guid sectionId);
            SeedUserProfile(db, completedOnboarding: false);
            SeedLinkedScene(db, sectionId, OnboardingDemoSceneMetadata.Merge(null), DateTimeOffset.UtcNow);
            db.SaveChanges();

            OnboardingDemoEligibilityService service = BuildService(db);

            OnboardingDemoEligibilityResult result = await service.EvaluateSectionAiDemoAsync(
                "user-1",
                documentId,
                sectionId,
                OnboardingDemoAiUsage.DemoActionKey,
                CancellationToken.None);

            Assert.True(result.IsEligible);
            Assert.Equal("demo-scene", result.Reason);
            Assert.Equal(sectionId, result.MatchedSectionId);
            Assert.True(result.MatchedSceneNodeId.HasValue);
            Assert.True(result.MatchedByMetadata);
            Assert.Equal(1, result.CandidateSceneCount);
        }

        [Fact]
        public async Task EvaluateSectionAiDemoAsync_Allows_WhenFirstLinkedSceneIsNotDemo_AndSecondIsDemo()
        {
            await using AppDbContext db = BuildDbContext();
            SeedDocumentGraph(db, out Guid documentId, out Guid sectionId);
            SeedUserProfile(db, completedOnboarding: false);
            SeedLinkedScene(db, sectionId, metadataJson: null, DateTimeOffset.UtcNow.AddMinutes(-10));
            Guid demoSceneId = SeedLinkedScene(db, sectionId, OnboardingDemoSceneMetadata.Merge(null), DateTimeOffset.UtcNow);
            db.SaveChanges();

            OnboardingDemoEligibilityService service = BuildService(db);

            OnboardingDemoEligibilityResult result = await service.EvaluateSectionAiDemoAsync(
                "user-1",
                documentId,
                sectionId,
                OnboardingDemoAiUsage.DemoActionKey,
                CancellationToken.None);

            Assert.True(result.IsEligible);
            Assert.Equal(demoSceneId, result.MatchedSceneNodeId);
            Assert.Equal(2, result.CandidateSceneCount);
        }

        [Fact]
        public async Task EvaluateSectionAiDemoAsync_Denies_WhenNoDemoMetadataExists()
        {
            await using AppDbContext db = BuildDbContext();
            SeedDocumentGraph(db, out Guid documentId, out Guid sectionId);
            SeedUserProfile(db, completedOnboarding: false);
            SeedLinkedScene(db, sectionId, "{\"type\":\"scene\"}", DateTimeOffset.UtcNow);
            db.SaveChanges();

            OnboardingDemoEligibilityService service = BuildService(db);

            OnboardingDemoEligibilityResult result = await service.EvaluateSectionAiDemoAsync(
                "user-1",
                documentId,
                sectionId,
                OnboardingDemoAiUsage.DemoActionKey,
                CancellationToken.None);

            Assert.False(result.IsEligible);
            Assert.Equal("linked-scenes-missing-demo-metadata", result.Reason);
            Assert.Equal(1, result.CandidateSceneCount);
        }

        [Fact]
        public async Task EvaluateSectionAiDemoAsync_Denies_WhenActionNotAllowlisted()
        {
            await using AppDbContext db = BuildDbContext();
            SeedDocumentGraph(db, out Guid documentId, out Guid sectionId);
            SeedUserProfile(db, completedOnboarding: false);
            SeedLinkedScene(db, sectionId, OnboardingDemoSceneMetadata.Merge(null), DateTimeOffset.UtcNow);
            db.SaveChanges();

            OnboardingDemoEligibilityService service = BuildService(db);

            OnboardingDemoEligibilityResult result = await service.EvaluateSectionAiDemoAsync(
                "user-1",
                documentId,
                sectionId,
                ExpandSectionAction.ActionIdValue,
                CancellationToken.None);

            Assert.False(result.IsEligible);
            Assert.Equal("action-not-allowlisted", result.Reason);
            Assert.Equal(0, result.CandidateSceneCount);
        }

        private static OnboardingDemoEligibilityService BuildService(AppDbContext db)
            => new(db, NullLogger<OnboardingDemoEligibilityService>.Instance);

        private static AppDbContext BuildDbContext()
        {
            DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite("Filename=:memory:")
                .Options;

            AppDbContext context = new(options);
            context.Database.OpenConnection();
            context.Database.EnsureCreated();
            return context;
        }

        private static void SeedDocumentGraph(AppDbContext db, out Guid documentId, out Guid sectionId)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            Guid projectId = Guid.NewGuid();
            documentId = Guid.NewGuid();
            sectionId = Guid.NewGuid();

            db.Projects.Add(new ProjectRecord
            {
                Id = projectId,
                OwnerUserId = "user-1",
                Title = "Project",
                CreatedUtc = now,
                UpdatedUtc = now
            });

            db.Documents.Add(new DocumentRecord
            {
                Id = documentId,
                ProjectId = projectId,
                OwnerUserId = "user-1",
                Title = "Doc",
                DocumentKind = DocumentKind.Manuscript,
                CreatedAt = now,
                UpdatedAt = now
            });

            db.Sections.Add(new SectionRecord
            {
                Id = sectionId,
                DocumentId = documentId,
                Title = "Scene",
                OrderIndex = 0,
                CreatedAt = now,
                UpdatedAt = now
            });

            db.SaveChanges();
        }

        private static void SeedUserProfile(AppDbContext db, bool completedOnboarding)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            string userId = db.Projects.Select(project => project.OwnerUserId).Single();

            db.UserProfiles.Add(new UserProfile
            {
                UserId = userId,
                DisplayName = "User",
                HasCompletedOnboarding = completedOnboarding,
                OnboardingStep = completedOnboarding ? 10 : 5,
                CreatedUtc = now.UtcDateTime,
                UpdatedUtc = now.UtcDateTime
            });
        }

        private static Guid SeedLinkedScene(AppDbContext db, Guid sectionId, string? metadataJson, DateTimeOffset updatedUtc)
        {
            Guid sceneNodeId = Guid.NewGuid();
            Guid projectId = db.Projects.Select(project => project.Id).Single();

            db.ProjectNodes.Add(new ProjectNodeRecord
            {
                Id = sceneNodeId,
                ProjectId = projectId,
                NodeType = ProjectNodeType.Scene,
                LinkedSectionId = sectionId,
                MetadataJson = metadataJson,
                Title = "Scene",
                UpdatedUtc = updatedUtc
            });

            return sceneNodeId;
        }
    }
}
