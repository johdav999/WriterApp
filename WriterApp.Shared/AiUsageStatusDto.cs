namespace WriterApp.Application.Usage
{
    public sealed class AiUsageStatusDto
    {
        public string Plan { get; init; } = string.Empty;
        public bool AiEnabled { get; init; }
        public bool UiEnabled { get; init; }
        public long QuotaTotal { get; init; }
        public long QuotaRemaining { get; init; }
    }
}
