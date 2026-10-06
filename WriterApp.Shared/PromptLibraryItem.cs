namespace WriterApp.Shared;
public sealed record PromptLibraryItem(Guid Id,PromptDefinition Preset,string? Notice=null,string? Origin=null,string? TransferStatus=null);
