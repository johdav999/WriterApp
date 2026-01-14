using System;

namespace WriterApp.AI.Abstractions
{
    public sealed record AiUsage(int InputTokens, int OutputTokens, TimeSpan Latency);
}
