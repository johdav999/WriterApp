using System;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using WriterApp.Application.Subscriptions;
using WriterApp.Client.State;
using WriterApp.Client.Utilities;

namespace WriterApp.Client.Services
{
    internal sealed class FeatureAccessService
    {
        private readonly AuthMeStateService _authMeStateService;
        private readonly NavigationManager _navigation;
        private readonly ILogger<FeatureAccessService> _logger;
        private readonly HashSet<string> _loggedDeniedFeatures = new(StringComparer.Ordinal);

        public FeatureAccessService(
            AuthMeStateService authMeStateService,
            NavigationManager navigation,
            ILogger<FeatureAccessService> logger)
        {
            _authMeStateService = authMeStateService ?? throw new ArgumentNullException(nameof(authMeStateService));
            _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool CanUse(FeatureKey feature)
        {
            PlanTier userTier = GetCurrentTier();
            PlanTier requiredTier = GetRequiredTier(feature);
            bool allowed = FeatureRegistry.IsFeatureAllowed(feature, userTier);
            if (!allowed)
            {
                string fingerprint = $"{feature}:{userTier}:{requiredTier}";
                if (_loggedDeniedFeatures.Add(fingerprint))
                {
                    _logger.LogInformation(
                        "FeatureAccessDenied FeatureKey={FeatureKey} UserTier={UserTier} RequiredTier={RequiredTier}",
                        feature,
                        userTier,
                        requiredTier);
                }
            }

            return allowed;
        }

        public PlanTier GetCurrentTier()
        {
            return NormalizePlanKey(_authMeStateService.PlanKey);
        }

        public PlanTier GetRequiredTier(FeatureKey feature)
        {
            if (!FeatureRegistry.FeatureMinimumTier.TryGetValue(feature, out PlanTier requiredTier))
            {
                throw new ArgumentOutOfRangeException(nameof(feature), feature, "Feature is not registered.");
            }

            return requiredTier;
        }

        public string GetUpgradeMessage(FeatureKey feature)
        {
            return $"Upgrade to {GetPlanLabel(GetRequiredTier(feature))} to use {GetFeatureDisplayName(feature)}";
        }

        public string GetRequiredPlanMessage(FeatureKey feature)
        {
            return $"{GetFeatureDisplayName(feature)} requires {GetPlanLabel(GetRequiredTier(feature))}";
        }

        public string GetUpgradePath()
        {
            return "/upgrade";
        }

        public string GetUpgradePath(FeatureKey feature)
        {
            return $"/upgrade?feature={Uri.EscapeDataString(feature.ToString())}";
        }

        public string GetUpgradePathWithReturn(FeatureKey feature, string? returnUrl = null)
        {
            return AppendReturnUrl(GetUpgradePath(feature), returnUrl);
        }

        public string GetUpgradePathWithCurrentReturn(FeatureKey feature)
        {
            return GetUpgradePathWithReturn(feature, GetCurrentRelativePath());
        }

        public string AppendReturnUrl(string upgradePath, string? returnUrl = null)
        {
            string normalizedReturnUrl = SafeReturnUrl.NormalizeOrFallback(returnUrl ?? GetCurrentRelativePath(), fallback: string.Empty);
            if (string.IsNullOrWhiteSpace(normalizedReturnUrl))
            {
                return upgradePath;
            }

            string separator = upgradePath.Contains('?', StringComparison.Ordinal) ? "&" : "?";
            return $"{upgradePath}{separator}returnUrl={Uri.EscapeDataString(normalizedReturnUrl)}";
        }

        public string GetCurrentRelativePath()
        {
            try
            {
                string currentUri = _navigation.Uri;
                if (string.IsNullOrWhiteSpace(currentUri))
                {
                    return SafeReturnUrl.DefaultProjectsPath;
                }

                string candidate;
                if (Uri.TryCreate(currentUri, UriKind.Absolute, out Uri? absoluteCurrent))
                {
                    string baseRelative = _navigation.ToBaseRelativePath(absoluteCurrent.ToString());
                    candidate = string.IsNullOrWhiteSpace(baseRelative)
                        ? "/"
                        : "/" + baseRelative.TrimStart('/');
                }
                else
                {
                    candidate = currentUri.StartsWith("/", StringComparison.Ordinal)
                        ? currentUri
                        : "/" + currentUri;
                }

                string normalized = SafeReturnUrl.NormalizeOrFallback(candidate, SafeReturnUrl.DefaultProjectsPath);
                return IsUpgradeRoute(normalized)
                    ? SafeReturnUrl.DefaultProjectsPath
                    : normalized;
            }
            catch (ArgumentException)
            {
                return SafeReturnUrl.DefaultProjectsPath;
            }
        }

        private static bool IsUpgradeRoute(string path)
        {
            return string.Equals(path, "/upgrade", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/upgrade?", StringComparison.OrdinalIgnoreCase);
        }

        public bool TryResolveFeature(string? featureKey, out FeatureKey feature)
        {
            if (string.IsNullOrWhiteSpace(featureKey))
            {
                feature = default;
                return false;
            }

            return Enum.TryParse(featureKey.Trim(), ignoreCase: true, out feature);
        }

        public string GetFeatureDisplayName(FeatureKey feature)
        {
            return feature switch
            {
                FeatureKey.AiSynopsisEvaluation => "Evaluate Synopsis",
                FeatureKey.QualityChecks => "Quality Checks",
                FeatureKey.AiGuidingQuestions => "Ask Guiding Questions",
                FeatureKey.AiSynopsisSuggestions => "Synopsis Alternatives",
                FeatureKey.SceneAiSuggestions => "Scene Suggestions",
                FeatureKey.StoryCoach => "Story Coach",
                FeatureKey.ContinuityCheck => "Continuity Check",
                FeatureKey.CanonRefresh => "Refresh Canon",
                FeatureKey.AiUndoRedo => "AI Undo",
                FeatureKey.AiActionHistory => "AI History",
                FeatureKey.CoverGeneration => "Cover Generation",
                FeatureKey.PromptLibrary => "Prompt Library",
                FeatureKey.VersionHistory => "Version History",
                FeatureKey.ExportTemplates => "Export Templates",
                FeatureKey.ExportPresets => "Export Presets",
                FeatureKey.ProjectStructureEditing => "Project Structure Editing",
                FeatureKey.ProjectProgressDashboard => "Project Progress Dashboard",
                FeatureKey.OutlineTemplates => "Outline Templates",
                _ => HumanizeFeatureName(feature.ToString())
            };
        }

        public string GetPlanLabel(PlanTier tier)
        {
            return GetPlanLabelInternal(tier);
        }

        private static PlanTier NormalizePlanKey(string? planKey)
        {
            if (string.IsNullOrWhiteSpace(planKey))
            {
                return PlanTier.Free;
            }

            return planKey.Trim().ToLowerInvariant() switch
            {
                "standard" => PlanTier.Standard,
                "professional" => PlanTier.Professional,
                "pro" => PlanTier.Professional,
                _ => PlanTier.Free
            };
        }

        private static string GetPlanLabelInternal(PlanTier tier)
        {
            return tier switch
            {
                PlanTier.Standard => "Standard",
                PlanTier.Professional => "Professional",
                _ => "Free"
            };
        }

        private static string HumanizeFeatureName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "This feature";
            }

            return Regex.Replace(value, "([a-z])([A-Z])", "$1 $2");
        }
    }
}
