using System.Text.Json;
using System.Text.Json.Nodes;
using WriterApp.Application.AI;
using WriterApp.Application.Documents;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared.Sync;
using WriterApp.UI.Shared.Projects;

namespace WriterApp.Device.Shared.Services;

public sealed class LocalStoryboardData(LocalDocumentRepository documents, DeviceAiService ai, LocalAiStore? history = null) : IStoryboardData
{
    public LocalDocument? Document { get; private set; }
    public Func<Task<bool>>? BeforeMutation { get; set; }
    public Func<Task>? AfterMutation { get; set; }
    public Func<Task<bool>>? BeforeAi { get; set; }
    private readonly SemaphoreSlim _gate = new(1, 1);
    private LocalDocument? _aiSource;
    private DeviceAiProposal? _aiProposal;

    public async Task LoadAsync(Guid projectId, Guid? documentId = null)
    {
        var candidates = (await documents.ListAsync()).Documents.Where(d => d.Project?.ProjectId == projectId && d.Kind == "manuscript").ToArray();
        var found = documentId is { } requested ? candidates.SingleOrDefault(d => d.DocumentId == requested)
            : candidates.FirstOrDefault(d => d.DocumentId == d.Project!.PrimaryDocumentId) ?? candidates.OrderBy(d => d.CreatedAtUtc).FirstOrDefault();
        found = found
            ?? throw new InvalidOperationException("Project unavailable. Restore a trashed manuscript first.");
        Document = await documents.LoadAsync(found.DocumentId) ?? throw new IOException("Manuscript unavailable.");
    }
    private LocalDocument Current(Guid projectId, Guid? documentId = null) => Document is { DeletedAtUtc: null, Project: { } p } d && p.ProjectId == projectId && (documentId == null || d.DocumentId == documentId)
        ? d : throw new InvalidOperationException("Reload the storyboard before continuing.");
    private static LocalProjectNode Node(LocalDocument d, Guid id) => d.Project!.Nodes.SingleOrDefault(n => n.NodeId == id && n.DeletionId is null)
        ?? throw new InvalidOperationException("The node is unavailable. Reload the storyboard.");

    public async Task<LocalDocument?> CommitAsync(Func<LocalDocument, LocalDocument> mutation)
    {
        await _gate.WaitAsync();
        try
        {
            for (int attempt = 0; ; attempt++)
            {
                var source = Document ?? throw new InvalidOperationException("Load a project first.");
                var latest = await documents.LoadAsync(source.DocumentId)
                    ?? throw new LocalDocumentConflictException(source.DocumentId);
                if (latest.LocalRevision != source.LocalRevision)
                {
                    // Background sync advances the revision even when writing and planning
                    // are unchanged. Adopt its acknowledgment before applying the edit.
                    // Actual content/structure changes must still require a reload.
                    if (DeviceSyncMapping.Fingerprint(latest) != DeviceSyncMapping.Fingerprint(source))
                        throw new LocalDocumentConflictException(source.DocumentId);
                    Document = source = latest;
                }
                var next = mutation(source);
                LocalProjectStructure.Validate(next);
                try
                {
                    Document = await documents.SaveAsync(next);
                    break;
                }
                catch (LocalDocumentConflictException) when (attempt < 2)
                {
                    // Sync may acknowledge between the read and the compare-and-swap.
                    // Recheck against the unchanged source before retrying the mutation.
                }
            }
            if (AfterMutation is not null) await AfterMutation();
            return Document;
        }
        finally { _gate.Release(); }
    }
    private async Task<LocalDocument> MutateAsync(Guid projectId, Guid? documentId, Func<LocalDocument, LocalDocument> mutation)
    {
        if (BeforeMutation is not null && !await BeforeMutation()) throw new InvalidOperationException("Save the scene detail draft before changing the storyboard.");
        Current(projectId, documentId);
        return (await CommitAsync(mutation))!;
    }
    public ProjectTreeDto Tree(Guid projectId, Guid? documentId = null)
    {
        var d = Current(projectId, documentId); var p = d.Project!;
        var nodes = p.Nodes.Where(n => n.DeletionId is null).Select(n => ToDto(d, n)).ToArray();
        return new(new(p.ProjectId, p.Title, p.Subtitle, p.AuthorName, p.Language, p.Genre, p.CoverImageUrl,
            d.CreatedAtUtc, d.UpdatedAtUtc, nodes.Where(n => n.NodeType == "scene").Sum(n => n.WordCountCache), p.PrimaryDocumentId, d.DocumentId), nodes, d.DocumentId);
    }
    private static ProjectNodeDto ToDto(LocalDocument d, LocalProjectNode n)
    {
        string? metadata = n.MetadataJson;
        if (n.NodeType == "scene")
        {
            JsonObject json;
            try { json = JsonNode.Parse(metadata ?? "{}") as JsonObject ?? new(); }
            catch (JsonException) { json = new(); }
            if (n.Card is { } c)
            {
                json["summary"] = c.Summary; json["status"] = c.Status; json["pov"] = c.PovCharacterId;
                json["narrativePurpose"] = c.NarrativePurpose; json["narrativeRole"] = c.NarrativeRole;
                json["narrativeIntent"] = c.NarrativeIntent; json["subplotTags"] = JsonSerializer.SerializeToNode(Tags(c.SubplotTagsJson));
            }
            json["notesText"] = n.Notes;
            metadata = json.ToJsonString();
        }
        int words = n.SectionId is { } id ? DeviceWordMetrics.CountPages(d.Sections.Single(s => s.SectionId == id).Pages) ?? 0 : 0;
        return new(n.NodeId, d.Project!.ProjectId, n.ParentId, n.NodeType, n.Title, n.OrderIndex, n.SectionId, metadata, words, d.UpdatedAtUtc);
    }
    private static IReadOnlyList<string> Tags(string? json) => JsonSerializer.Deserialize<string[]>(json ?? "[]") ?? [];
    public Task<SceneCardDto?> GetSceneCardAsync(Guid projectId, Guid sceneId, Guid? documentId = null)
    {
        var d = Current(projectId, documentId); var n = Node(d, sceneId);
        if (n.NodeType != "scene") throw new InvalidOperationException("Select a scene.");
        var c = n.Card ?? LocalPlanning.EmptyCard;
        return Task.FromResult<SceneCardDto?>(new(n.NodeId, c.NarrativePurpose, c.EmotionalBeat, c.KeyEvents, c.OpenQuestions,
            d.UpdatedAtUtc, c.PovCharacterId, c.PlaceId, c.TimelineEventId, c.TimeRef, Tags(c.TagsJson),
            JsonSerializer.Deserialize<SceneCardReferenceDto[]>(c.ReferencesJson ?? "[]"), c.Summary, c.Status,
            Tags(c.SubplotTagsJson), c.NarrativeRole, c.NarrativeIntent));
    }
    public async Task SaveSceneCardAsync(Guid projectId, Guid sceneId, SceneCardUpdateRequest r, Guid? documentId = null) =>
        await MutateAsync(projectId, documentId, d => LocalPlanning.Scene(d, sceneId, new(r.NarrativePurpose, r.NarrativeRole, r.NarrativeIntent,
            r.EmotionalBeat, r.KeyEvents, r.OpenQuestions, r.Summary, r.Status, r.PovCharacterId, r.PlaceId,
            r.TimelineEventId, r.TimeRef, JsonSerializer.Serialize(r.Tags ?? []), JsonSerializer.Serialize(r.SubplotTags ?? []),
            JsonSerializer.Serialize(r.References ?? [])), Node(d, sceneId).Notes ?? ""));

    public async Task PatchNodeAsync(Guid projectId, Guid nodeId, ProjectNodePatchRequest r, Guid? documentId = null) => await MutateAsync(projectId, documentId, d =>
    {
        var n = Node(d, nodeId);
        if (r.NodeType != n.NodeType || r.LinkedSectionId != n.SectionId) throw new InvalidOperationException("Storyboard edits must preserve the scene's writing link and node type.");
        if (r.Title != n.Title) d = LocalProjectStructure.Apply(d, new(LocalProjectAction.Rename, nodeId, Title: r.Title), DateTimeOffset.UtcNow);
        if (r.ParentId != n.ParentId) d = LocalProjectStructure.Apply(d, new(LocalProjectAction.Move, nodeId, r.ParentId), DateTimeOffset.UtcNow);
        return d with { Project = d.Project! with { Nodes = d.Project!.Nodes.Select(x => x.NodeId == nodeId ? x with { MetadataJson = r.MetadataJson } : x).ToArray() } };
    });

    public async Task ReorderAsync(Guid projectId, Guid parentId, ProjectNodeReorderRequest r, Guid? documentId = null) => await MutateAsync(projectId, documentId, d =>
        Reorder(d, parentId, r));
    public async Task MoveSceneAsync(Guid projectId, Guid sceneId, ProjectNodePatchRequest patch, ProjectNodeReorderRequest order, Guid? documentId = null) => await MutateAsync(projectId, documentId, d =>
    {
        var node = Node(d, sceneId);
        if (node.NodeType != "scene" || patch.ParentId is not { } chapterId || Node(d, chapterId).NodeType != "chapter")
            throw new InvalidOperationException("Choose an active scene and destination chapter.");
        var moved = LocalProjectStructure.Apply(d, new(LocalProjectAction.Move, sceneId, chapterId), DateTimeOffset.UtcNow);
        return Reorder(moved, chapterId, order);
    });
    private static LocalDocument Reorder(LocalDocument d, Guid parentId, ProjectNodeReorderRequest r)
    {
        Node(d, parentId);
        var siblings = d.Project!.Nodes.Where(n => n.ParentId == parentId && n.DeletionId is null).Select(n => n.NodeId).ToHashSet();
        if (r.OrderedChildIds.Count != siblings.Count || r.OrderedChildIds.Distinct().Count() != siblings.Count || !siblings.SetEquals(r.OrderedChildIds))
            throw new InvalidOperationException("The chapter changed. Reload the storyboard before reordering.");
        return OrderManuscript(d with { Project = d.Project with { Nodes = d.Project.Nodes.Select(n => siblings.Contains(n.NodeId)
            ? n with { OrderIndex = r.OrderedChildIds.ToList().IndexOf(n.NodeId) } : n).ToArray() } });
    }
    private static LocalDocument OrderManuscript(LocalDocument d)
    {
        IEnumerable<Guid> Sections(Guid? parent)
        {
            foreach (var n in d.Project!.Nodes.Where(n => n.ParentId == parent && n.DeletionId is null).OrderBy(n => n.OrderIndex))
            { if (n.SectionId is { } id) yield return id; foreach (var child in Sections(n.NodeId)) yield return child; }
        }
        var order = Sections(null).ToArray();
        return d with { Sections = order.Select(id => d.Sections.Single(s => s.SectionId == id))
            .Concat(d.Sections.OrderBy(s => s.OrderIndex).Where(s => !order.Contains(s.SectionId))).Select((s,i) => s with { OrderIndex = i }).ToArray() };
    }
    public async Task<ProjectNodeDto> CreateNodeAsync(Guid projectId, ProjectNodeCreateRequest r, Guid? documentId = null)
    {
        Guid id = Guid.Empty;
        var saved = await MutateAsync(projectId, documentId, d =>
        {
            var before = d.Project!.Nodes.Select(n => n.NodeId).ToHashSet();
            var next = LocalProjectStructure.Apply(d, new(LocalProjectAction.Create, ParentId: r.ParentId, NodeType: r.NodeType, Title: r.Title), DateTimeOffset.UtcNow);
            id = next.Project!.Nodes.Single(n => !before.Contains(n.NodeId)).NodeId;
            return next with { Project = next.Project with { Nodes = next.Project.Nodes.Select(n => n.NodeId == id ? n with { MetadataJson = r.MetadataJson } : n).ToArray() } };
        });
        return ToDto(saved, Node(saved, id));
    }
    public async Task<ProjectNodeDuplicateResponse> DuplicateAsync(Guid projectId, Guid nodeId, ProjectNodeDuplicateRequest request, Guid? documentId = null)
    {
        Guid id = Guid.NewGuid();
        var saved = await MutateAsync(projectId, documentId, d =>
        {
            var source = Node(d, nodeId);
            if (source.NodeType != "scene" || request.Deep == true) throw new InvalidOperationException("Select a single scene to duplicate.");
            var now = DateTimeOffset.UtcNow; var section = d.Sections.Single(s => s.SectionId == source.SectionId);
            var copy = section with { SectionId = Guid.NewGuid(), ServerSectionId = null, CreatedAtUtc = now, UpdatedAtUtc = now,
                Pages = section.Pages.Select(p => p with { PageId = Guid.NewGuid(), ServerPageId = null, CreatedAtUtc = now, UpdatedAtUtc = now }).ToArray() };
            if (d.Sections.Count >= 100 || d.Sections.Sum(s => s.Pages.Count) + copy.Pages.Count > 1000)
                throw new InvalidOperationException("A document supports up to 100 sections and 1,000 pages.");
            var cloned = source with { NodeId = id, ServerNodeId = null, SectionId = copy.SectionId, OrderIndex = source.OrderIndex + 1, Annotations = [] };
            var nodes = d.Project!.Nodes.Select(n => n.ParentId == source.ParentId && n.DeletionId is null && n.OrderIndex > source.OrderIndex
                ? n with { OrderIndex = checked(n.OrderIndex + 1) } : n).Append(cloned).ToArray();
            return OrderManuscript(d with { Project = d.Project with { Nodes = nodes }, Sections = d.Sections.Append(copy).ToArray() });
        });
        return new(id, [ToDto(saved, Node(saved, id))], saved.Project!.Nodes.Where(n => n.DeletionId is null && n.NodeId != id).Select(n => ToDto(saved,n)).ToArray());
    }
    public async Task DeleteNodeAsync(Guid projectId, Guid nodeId, Guid? documentId = null) => await MutateAsync(projectId, documentId,
        d => LocalProjectStructure.Apply(d, new(LocalProjectAction.Delete, nodeId), DateTimeOffset.UtcNow));
    public async Task<ProjectSceneOpenTargetDto?> ResolveTargetAsync(Guid projectId, Guid sceneId, Guid? documentId = null)
    {
        if (BeforeAi is not null && !await BeforeAi()) throw new InvalidOperationException("Save scene detail before running AI.");
        var d = Current(projectId, documentId); var n = Node(d, sceneId);
        return new(projectId, sceneId, d.DocumentId, n.SectionId, n.Title);
    }
    public async Task<AiActionExecuteResponseDto> ExecuteAiAsync(Guid projectId, string key, AiActionExecuteRequestDto request, Guid? documentId = null)
    {
        if (key is not ("storyboard.suggest-next-scene" or "storyboard.detect-missing-scenes" or "storyboard.check-subplot-continuity" or "storyboard.analyze-pov-balance"))
            throw new InvalidOperationException("Unknown storyboard AI action.");
        if (BeforeMutation is not null && !await BeforeMutation()) throw new InvalidOperationException("Save scene detail before running AI.");
        var d = Current(projectId, documentId);
        if (request.DocumentId != d.DocumentId || request.SectionId is not { } section) throw new InvalidOperationException("Select a scene from this project.");
        var prepared = AdvancedAiRequests.Build(d, section, AdvancedAiAction.Storyboard).Request;
        var parameters = request.Parameters is null ? new Dictionary<string, object?>() : new(request.Parameters);
        parameters["storyboard_context"] = AdvancedAiRequests.StoryboardContext(d);
        if (key == "storyboard.suggest-next-scene")
        {
            var anchor = d.Project!.Nodes.Single(n => n.NodeType == "scene" && n.SectionId == section && n.DeletionId is null);
            parameters["selected_scene_title"] = anchor.Title;
            parameters["preferred_chapter_title"] = d.Project.Nodes.SingleOrDefault(n => n.NodeId == anchor.ParentId && n.DeletionId is null)?.Title ?? "";
        }
        prepared = prepared with { Key = key, Request = prepared.Request with { Parameters = parameters } };
        var proposal = await ai.ProposeAsync(prepared, CancellationToken.None);
        var latest = await documents.LoadAsync(d.DocumentId) ?? throw new IOException("Manuscript unavailable.");
        if (latest.LocalRevision != d.LocalRevision) throw new InvalidOperationException("The storyboard changed. Reload and run AI again.");
        _aiSource = d; _aiProposal = proposal;
        if (history is not null) await history.SaveHistoryAsync(new(1, Guid.NewGuid(), d.DocumentId, key, "Analysis:Storyboard",
            d.LocalRevision, d.ServerVersion, DateTimeOffset.UtcNow, "Reviewed", d, proposal.ProposedText,
            OriginalText: proposal.SourceText, CloudOrigin:proposal.HistoryOrigin));
        return new(proposal.ProposalId, null, proposal.ProposedText, proposal.Summary, proposal.PreparedAt, key, SourceDocumentVersion: d.ServerVersion);
    }
    public async Task ValidateSuggestionAsync(Guid projectId, Guid? documentId = null)
    {
        var d = Current(projectId, documentId);
        if (_aiProposal is null || _aiSource is null) throw new InvalidOperationException("Run AI again before creating a suggested scene.");
        ai.RequireCurrentAccount(_aiProposal);
        var latest = await documents.LoadAsync(d.DocumentId);
        if (latest?.LocalRevision != _aiSource.LocalRevision || d.LocalRevision != _aiSource.LocalRevision)
            throw new InvalidOperationException("The storyboard changed. Run AI again before creating a suggested scene.");
    }
    public async Task<LocalAiHistory?> BeginSuggestedSceneAsync()
    {
        if (_aiSource is null || _aiProposal is null || history is null) return null;
        await ValidateSuggestionAsync(_aiSource.Project!.ProjectId, _aiSource.DocumentId);
        var intent = new LocalAiHistory(1, Guid.NewGuid(), _aiSource.DocumentId, _aiProposal.Prepared.Key, "Storyboard:created-scene",
            _aiSource.LocalRevision, _aiSource.ServerVersion, DateTimeOffset.UtcNow, "Applying", _aiSource, _aiProposal.ProposedText,
            OriginalText:_aiProposal.SourceText, CloudOrigin:_aiProposal.HistoryOrigin);
        await history.SaveHistoryAsync(intent); return intent;
    }
    public async Task CompleteSuggestedSceneAsync(LocalAiHistory? intent)
    {
        if (intent is null || history is null) return;
        var saved = await documents.LoadAsync(intent.DocumentId) ?? throw new IOException("Created scene document unavailable.");
        var created = saved.Project?.Nodes.Where(n => n.NodeType == "scene" && n.DeletionId is null
            && intent.Before.Project!.Nodes.All(old => old.NodeId != n.NodeId)).ToArray();
        if (created?.Length != 1) throw new InvalidOperationException("Scene creation cannot be confirmed uniquely. Keep the saved storyboard and inspect its local recovery record.");
        await history.SaveHistoryAsync(intent with { Status = "Applied", After = saved });
    }
}
