namespace WriterApp.AI.Abstractions
{
    public sealed record AiProviderSelection(
        IAiProvider Provider,
        string SelectedProviderId,
        bool WasFallbackUsed,
        string Reason);
}
