namespace WriterApp.Application.Billing
{
    public sealed record CheckoutStatusDto(
        string State,
        string? SubscriptionId,
        string? CustomerId,
        string? PlanKey,
        string? Message);
}
