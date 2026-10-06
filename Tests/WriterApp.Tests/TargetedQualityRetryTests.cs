using WriterApp.Application.Documents;
using WriterApp.Shared;
using Xunit;

namespace WriterApp.Tests;

public sealed class TargetedQualityRetryTests
{
    public const string Repeated = "The clock rang while another clock answered the clock in the hall.";
    public const string RepeatedValid = "The clock rang while another clock answered the chime in the hall.";
    public const string Passive = "The door was opened by Anna.";
    public const string PassiveValid = "Anna opened the door.";
    public static string Long => string.Join(" ", Enumerable.Range(1, 36).Select(i => "word" + i)) + ".";
    public static string LongValid => string.Join(" ", Enumerable.Range(1, 18).Select(i => "word" + i)) + ". " + string.Join(" ", Enumerable.Range(19, 18).Select(i => "word" + i)) + ".";
    public static PageQualityIssueDto Issue(string rule) => new("test", Guid.NewGuid(), Guid.NewGuid(), rule, rule, "info", "Synthetic", null,
        rule == "style.repeated_words" ? "clock" : rule == "style.passive_voice" ? "was opened" : Long, 0, 10, null, DateTimeOffset.UtcNow);
    public static (string Before, string Valid) Text(string rule) => rule switch {
        "style.repeated_words" => (Repeated, RepeatedValid), "style.passive_voice" => (Passive, PassiveValid), _ => (Long, LongValid) };
    [Theory]
    [InlineData("style.repeated_words", false, false)][InlineData("style.repeated_words", false, true)]
    [InlineData("style.repeated_words", true, false)][InlineData("style.repeated_words", true, true)]
    [InlineData("style.passive_voice", false, false)][InlineData("style.passive_voice", false, true)]
    [InlineData("style.passive_voice", true, false)][InlineData("style.passive_voice", true, true)]
    [InlineData("readability.sentence_length", false, false)][InlineData("readability.sentence_length", false, true)]
    [InlineData("readability.sentence_length", true, false)][InlineData("readability.sentence_length", true, true)]
    public async Task PolicyAllowsExactlyOneOptedInSemanticRetryAndNeverLoops(string rule, bool optIn, bool secondValid)
    {
        var (before, valid) = Text(rule); var calls = new List<bool>();
        Task<string> Generate(bool strict, CancellationToken ct) { calls.Add(strict); return Task.FromResult(strict && secondValid ? valid : before); }
        var task = TargetedQualityRetry.RunAsync(optIn, Issue(rule), before, Generate, text => text, _ => Task.CompletedTask, default);
        if (optIn && secondValid) { var result = await task; Assert.True(result.Retried); Assert.Equal(valid, result.Text); }
        else { var error = await Assert.ThrowsAsync<InvalidDataException>(() => task); Assert.Contains("retry manually", error.Message); }
        Assert.Equal(optIn ? new[] { false, true } : new[] { false }, calls);
    }
    [Theory][InlineData("style.repeated_words")][InlineData("style.passive_voice")][InlineData("readability.sentence_length")]
    public async Task ValidFirstResultUsesOnlyOneGeneration(string rule)
    {
        var (before, valid) = Text(rule); int calls = 0;
        var result = await TargetedQualityRetry.RunAsync(true, Issue(rule), before, (_, _) => { calls++; return Task.FromResult(valid); }, s => s, _ => Task.CompletedTask, default);
        Assert.Equal(1, calls); Assert.False(result.Retried); Assert.Equal(valid, result.Text);
    }
    [Theory][InlineData("")][InlineData("{\"revisedText\":\"prose\"}")][InlineData("<script>inert</script>")]
    [InlineData("Rewrite this into active voice.")][InlineData("Anna.")]
    public async Task MalformedEmptyMetadataAndUnsafeTruncationAreNotSemanticRetryEligibility(string candidate)
    {
        int calls = 0;
        await Assert.ThrowsAsync<InvalidDataException>(() => TargetedQualityRetry.RunAsync(true, Issue("style.passive_voice"), Passive,
            (_, _) => { calls++; return Task.FromResult(candidate); }, s => s, _ => Task.CompletedTask, default));
        Assert.Equal(1, calls);
    }
    [Theory][InlineData("network")][InlineData("quota")][InlineData("source")][InlineData("cancel")]
    public async Task InfrastructureSourceAndCancellationFailuresCannotScheduleSecondGeneration(string failure)
    {
        int calls = 0, checks = 0; using var cancel = new CancellationTokenSource();
        Task Check(CancellationToken ct)
        {
            if (++checks == 2 && failure == "source") throw new InvalidOperationException("Source changed");
            if (checks == 2 && failure == "cancel") cancel.Cancel();
            return Task.CompletedTask;
        }
        var run = TargetedQualityRetry.RunAsync(true, Issue("style.passive_voice"), Passive,
            (_, _) => { calls++; if (failure == "network") throw new HttpRequestException("Network"); if (failure == "quota") throw new InvalidOperationException("Quota"); return Task.FromResult(Passive); }, s => s, Check, cancel.Token);
        await Assert.ThrowsAnyAsync<Exception>(() => run); Assert.Equal(1, calls);
    }
    [Fact]
    public async Task CancellationAtRetryNotificationCannotScheduleAdditionalGeneration()
    {
        int calls = 0; using var cancel = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => TargetedQualityRetry.RunAsync(true, Issue("style.passive_voice"), Passive,
            (_, _) => { calls++; return Task.FromResult(Passive); }, s => s, _ => Task.CompletedTask, cancel.Token,
            () => { cancel.Cancel(); return Task.CompletedTask; }));
        Assert.Equal(1, calls);
    }
    [Theory][InlineData("style.repeated_words")][InlineData("style.passive_voice")][InlineData("readability.sentence_length")]
    public async Task UnsupportedOriginalTargetFailsBeforeAnyGeneration(string rule)
    {
        int calls = 0;
        await Assert.ThrowsAsync<InvalidOperationException>(() => TargetedQualityRetry.RunAsync(true, Issue(rule), "Unchanged unsupported sentence.",
            (_, _) => { calls++; return Task.FromResult("Different sentence."); }, s => s, _ => Task.CompletedTask, default));
        Assert.Equal(0, calls);
    }
    [Fact]
    public async Task SourceChangeAtRetryNotificationIsRecheckedBeforeAdditionalGeneration()
    {
        int calls = 0; bool changed = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() => TargetedQualityRetry.RunAsync(true, Issue("style.passive_voice"), Passive,
            (_, _) => { calls++; return Task.FromResult(Passive); }, s => s,
            _ => changed ? throw new InvalidOperationException("Source changed") : Task.CompletedTask, default,
            () => { changed = true; return Task.CompletedTask; }));
        Assert.Equal(1, calls);
    }
    [Fact]
    public void ChangedButUnimprovedGoalsAreEligibleWhileSupportedImprovementRetainsNecessaryRepetition()
    {
        Assert.True(TargetedQualityRetry.Validate(Issue("style.repeated_words"), Repeated, Repeated.Replace("rang", "sounded")).RetryEligible);
        Assert.True(TargetedQualityRetry.Validate(Issue("style.passive_voice"), Passive, "The gate was opened by Anna.").RetryEligible);
        Assert.True(TargetedQualityRetry.Validate(Issue("readability.sentence_length"), Long, Long.Replace("word1 ", "term1 ")).RetryEligible);
        Assert.True(TargetedQualityRetry.Validate(Issue("style.repeated_words"), Repeated, RepeatedValid).Valid);
    }
}
