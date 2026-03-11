using System;
using Microsoft.AspNetCore.Mvc;

namespace WriterApp.Application.Subscriptions
{
    public sealed record EntitlementDeniedApiError(
        string Code,
        string FeatureKey,
        string UpgradeUrl,
        string UserMessage)
    {
        public static EntitlementDeniedApiError FromException(EntitlementDeniedException ex)
        {
            string featureKey = string.IsNullOrWhiteSpace(ex.FeatureKey) ? "ai.feature" : ex.FeatureKey;
            string escapedFeature = Uri.EscapeDataString(featureKey);
            string userMessage = string.IsNullOrWhiteSpace(ex.Message)
                ? "Upgrade to access this feature."
                : ex.Message;

            return new EntitlementDeniedApiError(
                "entitlement_denied",
                featureKey,
                $"/upgrade?feature={escapedFeature}",
                userMessage);
        }

        public static ProblemDetails ToProblemDetails(EntitlementDeniedException ex)
        {
            string featureKey = string.IsNullOrWhiteSpace(ex.FeatureKey) ? "ai.feature" : ex.FeatureKey;
            string detail = string.IsNullOrWhiteSpace(ex.Message)
                ? "Upgrade to access this feature."
                : ex.Message;

            ProblemDetails problem = new()
            {
                Type = "https://prosa-app.com/problems/entitlement-denied",
                Title = "Upgrade required",
                Status = 402,
                Detail = detail
            };
            problem.Extensions["featureKey"] = featureKey;
            problem.Extensions["upgradePath"] = BuildUpgradePath(featureKey);
            return problem;
        }

        public static string BuildUpgradePath(string? featureKey)
        {
            string resolvedFeature = string.IsNullOrWhiteSpace(featureKey)
                ? "ai.feature"
                : featureKey.Trim();
            return $"/upgrade?feature={Uri.EscapeDataString(resolvedFeature)}";
        }

        public static ProblemDetails ForFeature(string featureKey, string? userMessage = null)
        {
            string resolvedFeature = string.IsNullOrWhiteSpace(featureKey) ? "ai.feature" : featureKey.Trim();
            ProblemDetails problem = new()
            {
                Type = "https://prosa-app.com/problems/entitlement-denied",
                Title = "Upgrade required",
                Status = 402,
                Detail = string.IsNullOrWhiteSpace(userMessage)
                    ? "Upgrade to access this feature."
                    : userMessage
            };
            problem.Extensions["featureKey"] = resolvedFeature;
            problem.Extensions["upgradePath"] = BuildUpgradePath(resolvedFeature);
            return problem;
        }
    }
}

