namespace WriterApp.Data.Documents;

public sealed class CoverEditProposalRecord
{
    public Guid Id {get;set;}
    public int Version {get;set;}=1;
    public string OwnerUserId {get;set;}="";
    public Guid ProjectId {get;set;}
    public Guid ProposedAssetId {get;set;}
    public string RequestJson {get;set;}="";
}
