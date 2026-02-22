using System;

namespace WriterApp.Application.Subscriptions
{
    public static class UserEntitlementDefaults
    {
        public const string FreePlanKey = "Free";
        public const string StandardPlanKey = "Standard";
        public const string ProfessionalPlanKey = "Professional";

        public const int FreeMonthlyTokenBudget = 0;
        public const int StandardMonthlyTokenBudget = 200000;
        public const int ProfessionalMonthlyTokenBudget = 1000000;
        public const int FREE_MONTHLY_TOKEN_BUDGET = FreeMonthlyTokenBudget;
        public const int STANDARD_MONTHLY_TOKEN_BUDGET = StandardMonthlyTokenBudget;
        public const int PROFESSIONAL_MONTHLY_TOKEN_BUDGET = ProfessionalMonthlyTokenBudget;

        public static string NormalizePlanKey(string? rawPlanKey)
        {
            if (string.IsNullOrWhiteSpace(rawPlanKey))
            {
                return FreePlanKey;
            }

            string normalized = rawPlanKey.Trim();
            if (normalized.Equals(FreePlanKey, StringComparison.OrdinalIgnoreCase))
            {
                return FreePlanKey;
            }

            if (normalized.Equals(StandardPlanKey, StringComparison.OrdinalIgnoreCase))
            {
                return StandardPlanKey;
            }

            if (normalized.Equals(ProfessionalPlanKey, StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("Pro", StringComparison.OrdinalIgnoreCase))
            {
                return ProfessionalPlanKey;
            }

            return FreePlanKey;
        }

        public static int ResolveMonthlyTokenBudget(string planKey)
        {
            string normalized = NormalizePlanKey(planKey);
            return normalized switch
            {
                StandardPlanKey => StandardMonthlyTokenBudget,
                ProfessionalPlanKey => ProfessionalMonthlyTokenBudget,
                _ => FreeMonthlyTokenBudget
            };
        }

        public static string ToPlanLookupKey(string planKey)
        {
            return NormalizePlanKey(planKey).ToLowerInvariant();
        }
    }
}
