namespace WriterApp.Application.Documents
{
    public sealed record SearchResultDto(
        Guid DocumentId,
        Guid? SectionId,
        Guid? PageId,
        string EntityType,
        string EntityId,
        string Title,
        string Snippet,
        double Score,
        string DocumentTitle,
        string MatchKind);
}
