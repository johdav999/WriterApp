namespace WriterApp.Client.Components.Projects
{
    public sealed record StoryboardNextSceneSuggestion(
        string Title,
        string? Summary,
        string Status,
        string? PovCharacterId,
        IReadOnlyList<string> SubplotTags,
        string? NarrativePurpose,
        string? Rationale,
        string? PreferredChapterTitle = null);
}
