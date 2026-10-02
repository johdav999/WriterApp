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
        DateTimeOffset UpdatedUtc);

    public sealed record UpsertPromptPresetRequest(
        Guid? ProjectId,
        string Name,
        string? Category,
        string Kind,
        string? BuiltinActionId,
        string? TemplateText,
        Dictionary<string, object?>? Parameters);
}
