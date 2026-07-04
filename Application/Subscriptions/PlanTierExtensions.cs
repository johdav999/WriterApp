namespace WriterApp.Application.Subscriptions
{
    public static class PlanTierExtensions
    {
        public static bool HasPaidPlan(this PlanTier plan)
            => plan == PlanTier.Standard || plan == PlanTier.Professional;

        public static PlanTier FromPlanKey(string? planKey)
        {
            return UserEntitlementDefaults.NormalizePlanKey(planKey) switch
            {
                UserEntitlementDefaults.StandardPlanKey => PlanTier.Standard,
                UserEntitlementDefaults.ProfessionalPlanKey => PlanTier.Professional,
                _ => PlanTier.Free
            };
        }
    }
}
