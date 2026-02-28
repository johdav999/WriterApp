namespace WriterApp.Application.Billing
{
    public interface IStripePriceResolver
    {
        string ResolvePriceId(string planKey, out string normalizedPlanKey);
        string? ResolvePlanKey(string priceId);
    }
}
