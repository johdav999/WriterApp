namespace WriterApp.Data.AI;

public sealed class BrainstormingTranscriptionRecord {
    public Guid SessionId {get;set;}
    public Guid OperationId {get;set;}
    public string PayloadHash {get;set;}="";
    public string ResultJson {get;set;}="";
}
