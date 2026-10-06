namespace WriterApp.Application.Usage
{
    public sealed class AiUsageStatusDto
    {
        public string PlanKey { get; init; } = string.Empty;
        public string Plan { get; init; } = string.Empty;
        public bool AiEnabled { get; init; }
        public bool UiEnabled { get; init; }
        public bool SupportsDocumentVersionChecks { get; init; }
        public bool SupportsCanonVersionChecks { get; init; }
        public bool SupportsStructuredTranslation { get; init; }
        public bool SupportsStyleQualityReview { get; init; }
        public bool SupportsRecommendedWriting { get; init; }
        public bool SupportsSynopsisCoaching { get; init; }
        public bool SupportsWebCheckedSources { get; init; }
        public long QuotaTotal { get; init; }
        public long QuotaRemaining { get; init; }
        public bool HasReachedAiLimit => QuotaRemaining <= 0;
        public bool ShouldShowAiLimitMessage => UiEnabled && AiEnabled && HasPaidPlan() && HasReachedAiLimit;
        public bool ShouldShowAiUpgradeHint => IsFreePlan() && HasReachedAiLimit;

        private bool HasPaidPlan()
            => IsStandardPlan() || IsProfessionalPlan();

        private bool IsFreePlan()
            => NormalizePlanKey() == "free";

        private bool IsStandardPlan()
            => NormalizePlanKey() == "standard";

        private bool IsProfessionalPlan()
            => NormalizePlanKey() == "professional";

        private string NormalizePlanKey()
        {
            string raw = string.IsNullOrWhiteSpace(PlanKey) ? Plan : PlanKey;
            if (string.IsNullOrWhiteSpace(raw))
            {
                return "free";
            }

            string normalized = raw.Trim().ToLowerInvariant();
            return normalized switch
            {
                "standard" => "standard",
                "professional" => "professional",
                "pro" => "professional",
                _ => "free"
            };
        }
    }
}
