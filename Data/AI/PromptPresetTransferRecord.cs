namespace WriterApp.Data.AI;

/// <summary>Durable replay receipt. Retained independently of later preset edits/deletion.</summary>
public sealed class PromptPresetTransferRecord
{
    public string OwnerUserId {get;set;}="";
    public Guid OperationId {get;set;}
    public string RequestHash {get;set;}="";
    public string ResultJson {get;set;}="";
    public DateTimeOffset CreatedUtc {get;set;}
}
