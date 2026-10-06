using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WriterApp.Application.Documents;
using WriterApp.Data;
using WriterApp.Data.Documents;
using WriterApp.Shared;

namespace WriterApp.Application.AI;

public sealed class WebPlanningRecovery(AppDbContext db)
{
    private static readonly string[] CardFields = Enum.GetValues<SceneCoachingField>().Select(SceneCardApprovals.Property).ToArray();
    private static readonly string[] SynopsisFields = ["Logline","Premise","Theme","ProtagonistArc","CentralConflict","Stakes","Setting","EndingIntent","OpenQuestions","Notes"];
    private static string[] Fields(string kind) => kind == "Synopsis" ? SynopsisFields : kind is "SceneCard" or "SectionCard" ? CardFields : throw new InvalidDataException("Unsupported planning recovery target.");
    public static bool Supports(string kind) => kind is "SceneCard" or "SectionCard" or "Synopsis";
    private static Dictionary<string,string?> Values(object record,string kind) => Fields(kind).ToDictionary(f=>f,f=>(string?)record.GetType().GetProperty(f)!.GetValue(record));
    public async Task<WebRecoverySnapshot> Capture(WebAiHistoryIntent intent,CancellationToken ct)
    {
        var targets = new List<WebRecoveryTarget>();
        async Task Add(string kind,Guid id,Guid? section) {
            var record=await Record(kind,id,ct,false);
            var values=Values(record,kind);targets.Add(new(kind,id,section,null,values,values));
        }
        if(intent.TargetKind=="Synopsis") await Add("Synopsis",intent.Source.DocumentId,null);
        else if(intent.TargetKind=="SceneCard") {
            var linked=await db.ProjectNodes.Where(n=>n.Id==intent.Source.SceneId && n.DocumentId==intent.Source.DocumentId).Select(n=>n.LinkedSectionId).SingleAsync(ct);
            await Add("SceneCard",intent.Source.SceneId!.Value,linked);
        }
        else if(intent.TargetKind=="SectionCard") {
            await Add("SectionCard",intent.Source.SectionId!.Value,intent.Source.SectionId);
            var mirrors=await db.ProjectNodes.AsNoTracking().Where(n=>n.DocumentId==intent.Source.DocumentId && n.LinkedSectionId==intent.Source.SectionId && n.NodeType==ProjectNodeType.Scene && n.SyncDeletionId==null).Take(1001).ToArrayAsync(ct);
            if(mirrors.Length>1000)throw new InvalidDataException("Too many linked planning mirrors for checked recovery.");
            foreach(var mirror in mirrors)await Add("SceneCard",mirror.Id,mirror.LinkedSectionId);
        }
        else throw new InvalidDataException("This operation has no planning snapshots.");
        return new(1,intent.Source.DocumentId,intent.Source.ProjectId,targets);
    }
    public async Task<WebRecoverySnapshot?> Complete(WebRecoverySnapshot before,CancellationToken ct)
    {
        var targets=new List<WebRecoveryTarget>();
        foreach(var target in before.Targets) {
            var after=Values(await Record(target.Kind,target.Id,ct,true),target.Kind);
            var keys=target.Before.Keys.Where(k=>target.Before[k]!=after[k]).ToArray();
            if(keys.Length>0)targets.Add(target with { Before=keys.ToDictionary(k=>k,k=>target.Before[k]),After=keys.ToDictionary(k=>k,k=>after[k]) });
        }
        return targets.Count==0 ? null : before with {Targets=targets};
    }
    public async Task Restore(WebRecoverySnapshot snapshot,bool undo,CancellationToken ct)
    {
        if(snapshot.Version!=1 || snapshot.Targets.Count is <1 or >1001)throw new InvalidDataException("Retained recovery snapshots are unsupported.");
        var records=new List<(WebRecoveryTarget Target,object Record)>();
        foreach(var target in snapshot.Targets) {
            if(target.Kind=="Synopsis" && target.Id!=snapshot.DocumentId)throw new InvalidDataException("Wrong synopsis recovery identity.");
            if(target.Kind=="SectionCard" && !await db.Sections.AnyAsync(s=>s.Id==target.Id && s.DocumentId==snapshot.DocumentId,ct)
                || target.Kind=="SceneCard" && !await db.ProjectNodes.AnyAsync(n=>n.Id==target.Id && n.DocumentId==snapshot.DocumentId && n.ProjectId==snapshot.ProjectId && n.LinkedSectionId==target.SectionId && n.SyncDeletionId==null,ct))
                throw new InvalidOperationException("The affected planning target moved or was deleted. Nothing was restored.");
            var record=await Record(target.Kind,target.Id,ct,true);var actual=Values(record,target.Kind);var expected=undo?target.After:target.Before;
            if(expected.Count==0 || !expected.Keys.Order().SequenceEqual((undo?target.Before:target.After).Keys.Order()) || expected.Keys.Any(k=>!Fields(target.Kind).Contains(k) || actual[k]!=expected[k]))
                throw new InvalidOperationException("An affected planning field changed. Keep current planning or recover from the retained comparison.");
            records.Add((target,record));
        }
        // Validate every mirror before the first mutation. The caller commits fields and receipt together.
        foreach(var (target,record) in records) {
            foreach(var value in undo?target.Before:target.After)record.GetType().GetProperty(value.Key)!.SetValue(record,value.Value);
            record.GetType().GetProperty(target.Kind=="SectionCard"?"UpdatedUtc":target.Kind=="Synopsis"?"UpdatedAt":"UpdatedAtUtc")!.SetValue(record,DateTimeOffset.UtcNow);
        }
    }
    private async Task<object> Record(string kind,Guid id,CancellationToken ct,bool require)
    {
        object? value=kind switch {
            "Synopsis"=>await db.DocumentSynopses.SingleOrDefaultAsync(s=>s.DocumentId==id,ct),
            "SceneCard"=>await db.SceneCards.SingleOrDefaultAsync(s=>s.SceneNodeId==id,ct),
            "SectionCard"=>await db.SectionSceneCards.SingleOrDefaultAsync(s=>s.SectionId==id,ct), _=>null };
        if(value is not null)return value;
        if(require)throw new InvalidOperationException("The saved planning record was deleted. Recovery refused.");
        return kind switch {"Synopsis"=>new DocumentSynopsisRecord(),"SceneCard"=>new SceneCardRecord(),"SectionCard"=>new SectionSceneCardRecord(),_=>throw new InvalidDataException("Unsupported planning target.")};
    }
    public static string Serialize(WebRecoverySnapshot snapshot) {
        var json=JsonSerializer.Serialize(snapshot,WebAiHistoryContracts.Json);
        return json.Length<=16_000_000?json:throw new InvalidDataException("Recovery snapshot exceeds its supported bound. No manuscript change was committed.");
    }
    public static WebRecoverySnapshot Parse(string json) => json.Length<=16_000_000 ? JsonSerializer.Deserialize<WebRecoverySnapshot>(json,WebAiHistoryContracts.Json) ?? throw new InvalidDataException("Missing recovery snapshot.") : throw new InvalidDataException("Recovery snapshot exceeds its bound.");
}
