namespace WriterApp.Controllers
{
    public sealed record PromptPresetDto(
        Guid Id,
        Guid? ProjectId,
        string Name,
        string? Category,
        string Kind,
        string? BuiltinActionId,
        string? TemplateText,
        Dictionary<string, object?> Parameters,
        DateTimeOffset CreatedUtc,
        DateTimeOffset UpdatedUtc,
        WriterApp.Shared.WritingScope? Scope = null, bool? Pinned = null, string? Version = null);

    public sealed record UpsertPromptPresetRequest(
        Guid? ProjectId,
        string Name,
        string? Category,
        string Kind,
        string? BuiltinActionId,
        string? TemplateText,
        Dictionary<string, object?>? Parameters);
}
