namespace WriterApp.Data.Documents;

// Immutable approved payload and original cover survive lost acknowledgement/reload.
public sealed class CoverEditSaveRecord
{
    public Guid Id {get;set;}
    public int Version {get;set;}=1;
    public string OwnerUserId {get;set;}="";
    public Guid ProjectId {get;set;}
    public string PayloadJson {get;set;}="";
    public string State {get;set;}="approved";
    public long ExpectedMetadataRevision {get;set;}
    public long? SavedMetadataRevision {get;set;}
    public long? RestoredMetadataRevision {get;set;}
    public string? BeforeCover {get;set;}
    public string AfterCover {get;set;}="";
}
