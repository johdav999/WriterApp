using System.Text.Json;
using WriterApp.Application.AI;
using WriterApp.Application.Synopsis;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared;
using WriterApp.UI.Shared.Projects;

namespace WriterApp.Device.Shared.Services;

public static class LocalSynopsisCoaching
{
    public static AdvancedAiPrepared Prepare(LocalDocument doc,string mode,string field,string notes) {
        if(doc.Project is null || doc.Kind!="manuscript")throw new InvalidDataException("Choose a project manuscript before synopsis coaching. Standalone writing remains available offline.");
        if(doc.DeletedAtUtc is not null || doc.ServerDocumentId is null || doc.ServerVersion is null || doc.SyncState!=LocalSyncState.Synced)
            throw new InvalidDataException("Save and synchronize the synopsis before coaching. Resolve any sync conflict first.");
        var source=doc.Project.Synopsis??new();
        var synopsis=new SynopsisAiRequestDto(mode=="suggest"?field:null,notes,1,doc.ServerVersion,source,doc.ServerProjectId);
        SynopsisCoaching.Validate(mode,synopsis);
        var request=new AiActionExecuteRequestDto(doc.ServerDocumentId,null,null,null,null,null,null,null,null,doc.ServerVersion);
        return new(doc,Guid.Empty,null,mode=="suggest"?field:mode,mode=="suggest"?LocalAiTarget.SynopsisField:LocalAiTarget.Analysis,
            new(SynopsisCoaching.Action(mode),request,DeviceAiAction.Custom,Guid.Empty,"",0,0,"","analysis",synopsis));
    }
    public static LocalDocument Preview(LocalDocument current,AdvancedAiPrepared prepared,DeviceAiProposal proposal) {
        if(prepared.Target!=LocalAiTarget.SynopsisField || prepared.Request.SynopsisRequest is not { } request || proposal.Prepared!=prepared.Request
            || prepared.Field!=request.FocusFieldKey || prepared.Request.Key!="synopsis.story_coach")throw new InvalidDataException("This synopsis result is analysis only or has a different field target.");
        SynopsisCoaching.Validate("suggest",request); SynopsisCoaching.ValidateText(proposal.ProposedText,20_000);
        AdvancedAiRequests.RequireFresh(current,prepared);
        if(SynopsisCoaching.Value(current.Project!.Synopsis??new(),prepared.Field)==proposal.ProposedText)throw new InvalidDataException("The proposal makes no change to this field.");
        return Set(current,prepared.Field,proposal.ProposedText);
    }
    private static LocalDocument Set(LocalDocument doc,string field,string text) => LocalPlanning.Synopsis(doc,
        SynopsisFields.Update(doc.Project!.Synopsis??new(),new(SynopsisCoaching.Fields.Single(f=>f.Key==field).Property,text)));
    public static void ValidateEntry(LocalAiHistory entry) {
        if(entry.Version!=5 || entry.Before.Project is null || entry.PageId is not null || entry.NodeId is not null || entry.SectionId is not null
            || entry.SourceRevision!=entry.Before.LocalRevision || entry.ServerVersion!=entry.Before.ServerVersion
            || entry.SynopsisCommentary?.Length>20_000 || entry.SynopsisUserNotes?.Length>2000)
            throw new InvalidDataException("Invalid synopsis history identity.");
        string mode=entry.Action switch { "synopsis.evaluate"=>"evaluate", "synopsis.questions"=>"questions", "synopsis.story_coach"=>"suggest", _=>throw new InvalidDataException("Invalid synopsis history mode.") };
        SynopsisCoaching.ValidateText(entry.Proposed,mode=="suggest"?20_000:60_000);
        if(mode!="suggest") { if(entry.Target!="Analysis:"+mode || entry.After is not null || entry.Status!="Reviewed")throw new InvalidDataException("Synopsis feedback cannot mutate planning.");return; }
        string field=entry.Target.StartsWith("SynopsisField:",StringComparison.Ordinal)?entry.Target["SynopsisField:".Length..]:"";
        if(!SynopsisCoaching.Fields.Any(f=>f.Key==field)||entry.OriginalText!=SynopsisCoaching.Value(entry.Before.Project.Synopsis??new(),field))throw new InvalidDataException("Invalid synopsis history field.");
        if(entry.After is null) { if(entry.Status!="Reviewed")throw new InvalidDataException("Synopsis approval lacks its durable result.");return; }
        var expected=Set(entry.Before,field,entry.Proposed);
        if(entry.OriginalText==entry.Proposed || !LocalDocumentCodec.Encode(expected).SequenceEqual(LocalDocumentCodec.Encode(entry.After)))
            throw new InvalidDataException("Synopsis approval changes writing or other planning.");
    }
    public static async Task ResumeAsync(LocalDocumentRepository documents,LocalAiStore store,LocalAiHistory entry,CancellationToken ct=default) {
        ValidateEntry(entry);
        if(entry.Status!="Applying"||entry.After is null)throw new InvalidOperationException("Only an explicitly approved interrupted synopsis save can be finished.");
        var current=await documents.LoadAsync(entry.DocumentId,ct)??throw new IOException("Document unavailable.");
        if(current.DeletedAtUtc is not null || current.SyncState==LocalSyncState.Conflict || current.ServerDocumentId!=entry.Before.ServerDocumentId||current.ServerVersion!=entry.Before.ServerVersion)
            throw new InvalidOperationException("Synopsis recovery source changed. Recover the original as a copy.");
        if(Signature(current)!=Signature(entry.After)) {
            if(Signature(current)!=Signature(entry.Before))throw new InvalidOperationException("Writing or planning has later changes. Recover the original as a copy.");
            await documents.SaveAsync(entry.After,ct);
        }
        await store.SaveHistoryAsync(entry with{Status="Applied"},CancellationToken.None);
    }
    private static string Signature(LocalDocument doc)=>JsonSerializer.Serialize(doc with{LocalRevision=1,UpdatedAtUtc=doc.CreatedAtUtc,LastSyncedAtUtc=null,SyncState=LocalSyncState.Synced});
}
