namespace WriterApp.AI.Abstractions
{
    public sealed record AiUsageDecision(
        bool Allowed,
        string UserId,
        string? ErrorCode,
        string? ErrorMessage);
}
