namespace WriterApp.Application.Documents;

public static class QualityRuleCatalog
{
    public const string CacheVersion = "2";
    public static IReadOnlyList<IQualityRule> Create() => [new SentenceLengthRule(), new ParagraphLengthRule(),
        new ReadabilityScoreRule(), new RepeatedWordRule(), new PassiveVoiceRule(), new ProperNameConsistencyRule(),
        new TimelineHintRule(), new GlossaryRule()];
}

public sealed record QualityHighlight(string IssueKey, int From, int To, string ExpectedText, string Severity);
