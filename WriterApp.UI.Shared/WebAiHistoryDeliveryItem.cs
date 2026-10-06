namespace WriterApp.UI.Shared;
public sealed record WebAiHistoryDeliveryItem(Guid OperationId,string Outcome,string TargetKind,int Sequence,string Status,string? Message,string? Before=null,string? After=null,bool Saved=false);
