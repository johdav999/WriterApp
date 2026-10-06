using WriterApp.Controllers;

namespace WriterApp.Shared;

public sealed record PromptTransferRequest(int Version, Guid OperationId, Guid PresetId, string Action, string? ExpectedVersion, PromptDefinition? Preset);
public sealed record PromptTransferResponse(int Version, Guid OperationId, Guid PresetId, bool Deleted, string? SourceVersion, PromptPresetDto? Preset);
public sealed record PromptTransferConflict(string Code, Guid PresetId, PromptPresetDto? Current);
