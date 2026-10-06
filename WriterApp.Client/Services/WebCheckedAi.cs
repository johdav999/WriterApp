using System.Net.Http.Json;
using System.Text.Json;
using WriterApp.Application.AI;
using WriterApp.Shared;

namespace WriterApp.Client.Services;

public sealed class WebCheckedAi(HttpClient http, AuthStateService? auth = null)
{
    public sealed record Lease(WebAiSource Source, string Backend, string? Account);
    public async Task<Lease> Capture(Guid document, Guid? section = null, Guid? page = null, Guid? scene = null, CancellationToken ct = default)
    {
        if(auth is not null) await auth.GetAsync(ct:ct);
        string backend=http.BaseAddress?.AbsoluteUri ?? ""; string? account=auth?.UserId;
        using var response=await http.GetAsync($"api/ai/actions/web-source/{document}?sectionId={section}&pageId={page}&sceneId={scene}",ct);
        if(!response.IsSuccessStatusCode) throw new InvalidOperationException("Checked AI source is unavailable. Save/synchronize and update the backend; this preview cannot use an unchecked fallback.");
        var source=await WebAiSources.Read<WebAiSource>(response.Content,ct); WebAiSources.Validate(source);
        if(source.DocumentId!=document || source.SectionId!=section || source.PageId!=page || source.SceneId!=scene) throw new InvalidDataException("Backend returned another AI target.");
        var lease=new Lease(source,backend,account); RequireLease(lease); return lease;
    }
    public void RequireLease(Lease lease)
    {
        if(lease.Backend!=(http.BaseAddress?.AbsoluteUri ?? "") || auth is not null && (lease.Account is null || lease.Account!=auth.UserId || !auth.IsAuthenticated))
            throw new InvalidOperationException("The account or backend changed. Generate a new proposal.");
    }
    public async Task Confirm(Lease lease, CancellationToken ct=default)
    {
        RequireLease(lease); var current=await Capture(lease.Source.DocumentId,lease.Source.SectionId,lease.Source.PageId,lease.Source.SceneId,ct);
        RequireLease(lease); WebAiSources.RequireCurrent(current.Source,lease.Source); ct.ThrowIfCancellationRequested();
    }
    public async Task<HttpResponseMessage> Execute(string key, AiActionExecuteRequestDto request, Lease lease, CancellationToken ct=default)
    {
        await Confirm(lease,ct);
        using var body=new HttpRequestMessage(HttpMethod.Post,$"api/ai/actions/{key}/execute"){Content=JsonContent.Create(request with { WebSource=lease.Source })};
        var response=await http.SendAsync(body,ct);
        try {
            if(response.IsSuccessStatusCode) {
                var result=await WebAiSources.Read<AiActionExecuteResponseDto>(response.Content,ct);
                if(result.ProposalId==Guid.Empty || result.ActionKey!=key || result.WebSource!=lease.Source || result.ProposedText?.Length>100_000)
                    throw new InvalidDataException("Unchecked or malformed AI response. Update the backend and regenerate.");
                WebAiSources.RequireTime(result.CreatedUtc); await Confirm(lease,ct);
                response.Content.Dispose(); response.Content=JsonContent.Create(result);
                await response.Content.LoadIntoBufferAsync(ct);
            }
            RequireLease(lease);ct.ThrowIfCancellationRequested();return response;
        } catch { response.Dispose();throw; }
    }
    public HttpRequestMessage Mutation(HttpMethod method,string url,object payload,Lease lease)
    {
        RequireLease(lease);
        var message=new HttpRequestMessage(method,url){Content=JsonContent.Create(payload)};
        message.Headers.Add("X-WriterApp-AI-Source",JsonSerializer.Serialize(lease.Source));return message;
    }
    public static void RequireMutationReceipt(HttpResponseMessage response)
    {
        if(!response.IsSuccessStatusCode || !response.Headers.TryGetValues("X-WriterApp-Checked-Save",out var values) || !values.Contains("1"))
            throw new InvalidDataException("Checked Save was not confirmed by the backend. Reload current content before retrying; your draft is retained.");
    }
    public async Task<AiActionExecuteRequestDto> BindCanon(string key,AiActionExecuteRequestDto request,Lease lease,CancellationToken ct=default)
    {
        if(key!="continuity.check_section" && !key.StartsWith("scene.",StringComparison.Ordinal))return request;
        var fresh=await Capture(lease.Source.DocumentId,lease.Source.SectionId,lease.Source.PageId,lease.Source.SceneId,ct);
        WebAiSources.RequireCurrent(fresh.Source,lease.Source);
        var parameters=request.Parameters is null ? new Dictionary<string,object?>() : new(request.Parameters);
        var versions=new Dictionary<WriterApp.Shared.Canon.CanonKind,string>();var entities=new List<WriterApp.Application.Documents.SceneEntity>();
        foreach(var kind in Enum.GetValues<WriterApp.Shared.Canon.CanonKind>()) {
            string type=kind.ToString().ToLowerInvariant();
            using var response=await http.GetAsync($"api/documents/{request.DocumentId}/bibles/{type}/device?expectedDocumentVersion={Uri.EscapeDataString(fresh.Source.DocumentVersion)}",ct);
            if(!response.IsSuccessStatusCode) {
                if (key == "continuity.check_section" && (int)response.StatusCode is 402 or 403 or 422 or >= 500) {
                    parameters[type+"_bible_json"]="{}"; continue;
                }
                throw new InvalidOperationException("Checked canon is unavailable. Refresh canon or update the backend before coaching.");
            }
            var snapshot=await WebAiSources.Read<WriterApp.Shared.Canon.DeviceBibleSnapshot>(response.Content,ct);
            if(snapshot.ContractVersion!=1 || snapshot.DocumentId!=request.DocumentId || snapshot.Kind!=kind || snapshot.CheckedDocumentVersion!=fresh.Source.DocumentVersion)
                throw new InvalidDataException("Story reference response belongs to another source.");
            if(key == "continuity.check_section" && (!snapshot.Exists || snapshot.SourceDocumentVersion != fresh.Source.DocumentVersion || snapshot.ContentJson.Length > 100_000)) {
                parameters[type+"_bible_json"]="{}";
                continue;
            }
            if(snapshot.ContentJson.Length>100_000 || snapshot.Exists && snapshot.SourceDocumentVersion!=fresh.Source.DocumentVersion)
                throw new InvalidOperationException("Canon is stale or oversized. Refresh canon before coaching.");
            var canon=WriterApp.Shared.Canon.CanonContent.Parse(kind,snapshot.ContentJson);
            entities.AddRange(canon.Entries.Where(e=>e.Id is not null).Select(e=>new WriterApp.Application.Documents.SceneEntity(
                kind switch { WriterApp.Shared.Canon.CanonKind.Character=>WriterApp.Application.Documents.SceneEntityKind.Character,WriterApp.Shared.Canon.CanonKind.Place=>WriterApp.Application.Documents.SceneEntityKind.Place,_=>WriterApp.Application.Documents.SceneEntityKind.Timeline },e.Id!,e.Name)));
            if(snapshot.Exists)versions[kind]=snapshot.SnapshotVersion;
            parameters[type+"_bible_json"]=snapshot.Exists ? snapshot.ContentJson : "{}";
        }
        if(key.StartsWith("scene.",StringComparison.Ordinal)) {
            using var response=await http.GetAsync($"api/ai/actions/writing-outline/{lease.Source.DocumentId}",ct);
            if(!response.IsSuccessStatusCode)throw new InvalidOperationException("Checked planning identities are unavailable. Update the backend.");
            var outline=await WebAiSources.Read<WritingOutlineSnapshot>(response.Content,ct);WritingOutline.Validate(outline);
            if(outline.DocumentId!=lease.Source.DocumentId || outline.ProjectId!=lease.Source.ProjectId)throw new InvalidDataException("Scene identities belong to another manuscript.");
            entities.AddRange(outline.Sections.Select(s=>new WriterApp.Application.Documents.SceneEntity(WriterApp.Application.Documents.SceneEntityKind.Section,s.Id.ToString(),s.Title)));
            entities.AddRange(outline.Nodes.Select(n=>new WriterApp.Application.Documents.SceneEntity(n.Type switch { "scene"=>WriterApp.Application.Documents.SceneEntityKind.Scene,"chapter"=>WriterApp.Application.Documents.SceneEntityKind.Chapter,_=>WriterApp.Application.Documents.SceneEntityKind.Part },n.Id.ToString(),n.Title)));
            parameters["scene_coaching_version"]="1";parameters["current_scene_card"]=request.OriginalText ?? "{}";
            parameters["scene_entities_json"]=JsonSerializer.Serialize(entities,new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
        await Confirm(lease,ct);return request with { Parameters=parameters,ExpectedCanonVersions=versions,ExpectedDocumentVersion=fresh.Source.DocumentVersion };
    }
}
