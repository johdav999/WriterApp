using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WriterApp.Application.AI;
using WriterApp.Data;
using WriterApp.Shared;

namespace WriterApp.Application.Collaboration;

public sealed record CollaborationStructureCommand(Guid OperationId, WebAiSource Source, string Kind, Guid? SectionId = null,
    string? Title = null, Guid[]? OrderedIds = null, Guid? ParentNodeId = null);
public sealed class CollaborationStructureCommands(AppDbContext db, ICollaborationCodec codec, CollaborationAccessService access)
{
    public async Task<Guid?> Execute(string actor, Guid document, CollaborationStructureCommand command, CancellationToken ct)
    {
        if(command.OperationId==Guid.Empty || command.Source.DocumentId!=document || command.Title?.Length>200
            || command.Kind is not ("create-chapter" or "create-section" or "duplicate-section" or "rename-section" or "reorder-sections" or "delete-section" or "create-page"))
            throw new CollaborationCommitException("collaboration_command_invalid");
        await access.Require(actor,document,CollaborationOperation.WriteText,ct);
        string hash=CollaborationMigrationService.Hash(JsonSerializer.SerializeToUtf8Bytes(new {actor,command}));
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async()=> {
            await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
            await CollaborationMigrationService.Lock(db,document,ct);
            await access.Require(actor,document,CollaborationOperation.WriteText,ct);
            var old=await db.CollaborationCommands.AsNoTracking().SingleOrDefaultAsync(c=>c.Id==command.OperationId,ct);
            if(old is not null) {
                if(old.DocumentId!=document || old.RequestSha256!=hash)throw new CollaborationCommitException("collaboration_command_identity_reused");
                await tx.CommitAsync(ct);return command.Kind is "create-chapter" or "create-section" or "duplicate-section" ? command.OperationId : command.SectionId;
            }
            await new WebAiSourceService(db,actor,access).Require(command.Source,ct);
            var snapshot=await CollaborationProjectionService.Read(db,actor,document,new(),ct,access,CollaborationOperation.WriteText);
            if(command.Kind is not ("create-chapter" or "create-section" or "reorder-sections") && !snapshot.Sections.Any(s=>s.SectionId==command.SectionId))
                throw new CollaborationCommitException("collaboration_section_missing");
            int newPages=command.Kind=="duplicate-section" ? snapshot.Pages.Count(p=>p.SectionId==command.SectionId) : command.Kind is "create-section" or "create-page" ? 1 : 0;
            int newSections=command.Kind is "create-section" or "duplicate-section" ? 1 : 0;
            if(snapshot.Pages.Length+newPages>1000 || snapshot.Sections.Length+newSections>1000)throw new CollaborationCommitException("collaboration_structure_limit");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO CollaborationStructurePermits(DocumentId,OperationId) VALUES({document},{command.OperationId})",ct);
            Guid? result=command.SectionId;var now=DateTimeOffset.UtcNow;
            async Task AddPage(Guid section, string title, string html, int order) {
                var seed=await codec.Seed(html,"Html",ct);var id=Guid.NewGuid();var generation=Guid.NewGuid();
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Pages(Id,DocumentId,SectionId,Title,Content,OrderIndex,CreatedAt,UpdatedAt) VALUES({id},{document},{section},{title},{""},{order},{now},{now})",ct);
                db.CollaborationPages.Add(new(){PageId=id,DocumentId=document,Generation=generation,ContentContractVersion=seed.Support.ContentContractVersion,ProtocolVersion=seed.Support.ProtocolVersion,SchemaFingerprint=seed.Support.SchemaFingerprint});
                db.CollaborationSnapshots.Add(new(){PageId=id,Generation=generation,State=seed.State,Sha256=seed.Sha256,CreatedAtUnixMilliseconds=now.ToUnixTimeMilliseconds()});
                db.CollaborationProjections.Add(new(){PageId=id,Generation=generation,Html=seed.Html,Json=seed.Json.GetRawText(),Text=seed.Text,StateSha256=seed.Sha256});
            }
            switch(command.Kind) {
                case "create-chapter":
                    var project=await db.Documents.Where(d=>d.Id==document).Select(d=>d.ProjectId).SingleAsync(ct);
                    if(command.Source.ProjectId!=project || string.IsNullOrWhiteSpace(command.Title)
                        || await db.ProjectNodes.CountAsync(n=>n.DocumentId==document,ct)>=1000
                        || command.ParentNodeId is { } parent && !await db.ProjectNodes.AnyAsync(n=>n.Id==parent && n.DocumentId==document && n.ProjectId==project && n.SyncDeletionId==null && n.NodeType==WriterApp.Data.Documents.ProjectNodeType.Part,ct))
                        throw new CollaborationCommitException("collaboration_chapter_destination_invalid");
                    int chapterOrder=await db.ProjectNodes.Where(n=>n.DocumentId==document && n.ParentId==command.ParentNodeId && n.SyncDeletionId==null).Select(n=>(int?)n.OrderIndex).MaxAsync(ct) is { } last ? last+1 : 0;
                    // Only this checked append is permitted; ordinary outline snapshots remain fenced.
                    await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO CollaborationPlanningPermits(DocumentId,OperationId) VALUES({document},{command.OperationId})",ct);
                    result=command.OperationId;
                    await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO ProjectNodes(Id,DocumentId,ProjectId,ParentId,NodeType,Title,OrderIndex,WordCountCache,UpdatedUtc) VALUES({result.Value},{document},{project},{command.ParentNodeId},{(int)WriterApp.Data.Documents.ProjectNodeType.Chapter},{command.Title.Trim()},{chapterOrder},{0},{now})",ct);
                    await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM CollaborationPlanningPermits WHERE DocumentId={document} AND OperationId={command.OperationId}",ct);
                    break;
                case "create-section": case "duplicate-section":
                    result=command.OperationId;
                    var title=command.Title?.Trim() ?? (command.Kind=="duplicate-section" ? snapshot.Sections.Single(s=>s.SectionId==command.SectionId).Title+" (copy)" : "New section");
                    if(string.IsNullOrWhiteSpace(title))throw new CollaborationCommitException("collaboration_title_required");
                    if(title.Length>200)title=title[..193]+" (copy)";
                    int order=snapshot.Sections.Select(s=>s.OrderIndex).DefaultIfEmpty(-1).Max()+1;
                    await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Sections(Id,DocumentId,Title,NarrativePurpose,OrderIndex,CreatedAt,UpdatedAt) VALUES({result.Value},{document},{title},{(string?)null},{order},{now},{now})",ct);
                    if(command.Kind=="duplicate-section") {
                        foreach(var page in snapshot.Pages.Where(p=>p.SectionId==command.SectionId))await AddPage(result.Value,page.Title,page.Html,page.OrderIndex);
                        var notes=await db.SectionNotes.AsNoTracking().SingleOrDefaultAsync(n=>n.SectionId==command.SectionId,ct);
                        if(notes is not null)await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO SectionNotes(SectionId,NotesText,UpdatedAtUtc) VALUES({result.Value},{notes.NotesText},{now})",ct);
                    }
                    else await AddPage(result.Value,"Page 1","<p></p>",0);
                    break;
                case "rename-section":
                    if(string.IsNullOrWhiteSpace(command.Title))throw new CollaborationCommitException("collaboration_title_required");
                    await db.Sections.Where(s=>s.Id==command.SectionId && s.DocumentId==document).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.Title,command.Title.Trim()),ct);break;
                case "reorder-sections":
                    var ids=command.OrderedIds ?? [];
                    if(ids.Length!=snapshot.Sections.Length || ids.Distinct().Count()!=ids.Length || ids.Any(id=>!snapshot.Sections.Any(s=>s.SectionId==id)))throw new CollaborationCommitException("collaboration_structure_changed");
                    for(int i=0;i<ids.Length;i++){var id=ids[i];await db.Sections.Where(s=>s.Id==id && s.DocumentId==document).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.OrderIndex,i),ct);}break;
                case "create-page":
                    await AddPage(command.SectionId!.Value,command.Title ?? "New page","<p></p>",snapshot.Pages.Where(p=>p.SectionId==command.SectionId).Select(p=>p.OrderIndex).DefaultIfEmpty(-1).Max()+1);break;
                case "delete-section":
                    if(snapshot.Sections.Length<2)throw new CollaborationCommitException("collaboration_keep_one_section");
                    var removed=snapshot.Pages.Where(p=>p.SectionId==command.SectionId).Select(p=>p.PageId).ToArray();
                    await db.CollaborationPages.Where(p=>p.DocumentId==document && removed.Contains(p.PageId) && !p.IsDeleted).ExecuteUpdateAsync(p=>p.SetProperty(x=>x.IsDeleted,true).SetProperty(x=>x.LeaseEpoch,x=>x.LeaseEpoch+1).SetProperty(x=>x.LeaseOwner,(string?)null).SetProperty(x=>x.LeaseExpiresAtUnixMilliseconds,0),ct);
                    await db.CollaborationSessions.Where(s=>removed.Contains(s.PageId)).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.Revoked,true).SetProperty(x=>x.Version,x=>x.Version+1),ct);
                    await db.CollaborationOutboxEvents.Where(e=>removed.Contains(e.PageId) && e.CompletedAtUnixMilliseconds==null).ExecuteUpdateAsync(e=>e.SetProperty(x=>x.CompletedAtUnixMilliseconds,now.ToUnixTimeMilliseconds()),ct);break;
            }
            var manifest=await db.CollaborationManifests.SingleAsync(m=>m.DocumentId==document,ct);manifest.StructureRevision++;
            db.CollaborationCommands.Add(new(){Id=command.OperationId,DocumentId=document,RequestSha256=hash,StructureRevision=manifest.StructureRevision});
            await db.SaveChangesAsync(ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM CollaborationStructurePermits WHERE DocumentId={document} AND OperationId={command.OperationId}",ct);
            await tx.CommitAsync(ct);return result;
        });
    }
}
