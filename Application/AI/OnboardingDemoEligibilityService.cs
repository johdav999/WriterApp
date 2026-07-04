using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WriterApp.Application.Documents;
using WriterApp.Data;
using WriterApp.Data.Documents;
using WriterApp.Data.Subscriptions;

namespace WriterApp.Application.AI
{
    public interface IOnboardingDemoEligibilityService
    {
        Task<OnboardingDemoEligibilityResult> EvaluateSectionAiDemoAsync(
            string userId,
            Guid documentId,
            Guid sectionId,
            string actionKey,
            CancellationToken ct);
    }

    public sealed record OnboardingDemoEligibilityResult(
        bool IsEligible,
        string Reason,
        Guid? MatchedSceneNodeId,
        Guid? MatchedSectionId,
        bool MatchedByMetadata,
        int CandidateSceneCount)
    {
        public static OnboardingDemoEligibilityResult Denied(
            string reason,
            Guid? matchedSectionId = null,
            int candidateSceneCount = 0)
            => new(false, reason, null, matchedSectionId, false, candidateSceneCount);

        public static OnboardingDemoEligibilityResult Allowed(
            string reason,
            Guid sceneNodeId,
            Guid sectionId,
            int candidateSceneCount)
            => new(true, reason, sceneNodeId, sectionId, true, candidateSceneCount);
    }

    public sealed class OnboardingDemoEligibilityService : IOnboardingDemoEligibilityService
    {
        private readonly AppDbContext _dbContext;
        private readonly ILogger<OnboardingDemoEligibilityService> _logger;

        public OnboardingDemoEligibilityService(
            AppDbContext dbContext,
            ILogger<OnboardingDemoEligibilityService> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<OnboardingDemoEligibilityResult> EvaluateSectionAiDemoAsync(
            string userId,
            Guid documentId,
            Guid sectionId,
            string actionKey,
            CancellationToken ct)
        {
            _logger.LogInformation(
                "Onboarding demo eligibility evaluation started. UserId={UserId} ActionKey={ActionKey} DocumentId={DocumentId} SectionId={SectionId}",
                userId,
                actionKey,
                documentId,
                sectionId);

            if (!OnboardingDemoAiUsage.IsAllowedAction(actionKey))
            {
                _logger.LogInformation(
                    "Onboarding demo eligibility denied. UserId={UserId} ActionKey={ActionKey} DocumentId={DocumentId} SectionId={SectionId} Reason={Reason}",
                    userId,
                    actionKey,
                    documentId,
                    sectionId,
                    "action-not-allowlisted");
                return OnboardingDemoEligibilityResult.Denied("action-not-allowlisted", sectionId);
            }

            UserProfile? profile = await _dbContext.UserProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.UserId == userId, ct);
            if (profile is null)
            {
                _logger.LogInformation(
                    "Onboarding demo eligibility denied. UserId={UserId} ActionKey={ActionKey} DocumentId={DocumentId} SectionId={SectionId} Reason={Reason}",
                    userId,
                    actionKey,
                    documentId,
                    sectionId,
                    "profile-missing");
                return OnboardingDemoEligibilityResult.Denied("profile-missing", sectionId);
            }

            if (profile.HasCompletedOnboarding)
            {
                _logger.LogInformation(
                    "Onboarding demo eligibility denied. UserId={UserId} ActionKey={ActionKey} DocumentId={DocumentId} SectionId={SectionId} Reason={Reason}",
                    userId,
                    actionKey,
                    documentId,
                    sectionId,
                    "onboarding-complete");
                return OnboardingDemoEligibilityResult.Denied("onboarding-complete", sectionId);
            }

            SceneMetadataCandidate[] candidates = await (
                    from node in _dbContext.ProjectNodes.AsNoTracking()
                    join project in _dbContext.Projects.AsNoTracking() on node.ProjectId equals project.Id
                    join section in _dbContext.Sections.AsNoTracking() on node.LinkedSectionId equals section.Id
                    where project.OwnerUserId == userId
                          && node.NodeType == ProjectNodeType.Scene
                          && node.LinkedSectionId == sectionId
                          && section.DocumentId == documentId
                    orderby node.UpdatedUtc descending, node.Id
                    select new SceneMetadataCandidate(
                        node.Id,
                        section.Id,
                        node.MetadataJson,
                        node.UpdatedUtc))
                .ToArrayAsync(ct);

            _logger.LogInformation(
                "Onboarding demo metadata candidates found. UserId={UserId} ActionKey={ActionKey} DocumentId={DocumentId} SectionId={SectionId} Count={Count}",
                userId,
                actionKey,
                documentId,
                sectionId,
                candidates.Length);

            if (candidates.Length == 0)
            {
                _logger.LogInformation(
                    "Onboarding demo eligibility denied. UserId={UserId} ActionKey={ActionKey} DocumentId={DocumentId} SectionId={SectionId} Reason={Reason}",
                    userId,
                    actionKey,
                    documentId,
                    sectionId,
                    "no-linked-scene-nodes");
                return OnboardingDemoEligibilityResult.Denied("no-linked-scene-nodes", sectionId);
            }

            SceneMetadataCandidate? matched = candidates
                .FirstOrDefault(candidate => OnboardingDemoSceneMetadata.IsDemoScene(candidate.MetadataJson));

            if (matched is null)
            {
                _logger.LogInformation(
                    "Onboarding demo eligibility denied. UserId={UserId} ActionKey={ActionKey} DocumentId={DocumentId} SectionId={SectionId} Reason={Reason}",
                    userId,
                    actionKey,
                    documentId,
                    sectionId,
                    "linked-scenes-missing-demo-metadata");
                return OnboardingDemoEligibilityResult.Denied(
                    "linked-scenes-missing-demo-metadata",
                    sectionId,
                    candidates.Length);
            }

            _logger.LogInformation(
                "Onboarding demo eligibility granted. UserId={UserId} ActionKey={ActionKey} DocumentId={DocumentId} SectionId={SectionId} SceneNodeId={SceneNodeId}",
                userId,
                actionKey,
                documentId,
                sectionId,
                matched.SceneNodeId);

            return OnboardingDemoEligibilityResult.Allowed(
                "demo-scene",
                matched.SceneNodeId,
                matched.SectionId,
                candidates.Length);
        }

        private sealed record SceneMetadataCandidate(
            Guid SceneNodeId,
            Guid SectionId,
            string? MetadataJson,
            DateTimeOffset UpdatedUtc);
    }
}
