namespace WriterApp.Shared.Brainstorming;

/// <summary>Explicit existing document/section/page/node maps. Conversation/proposal IDs remain stable.</summary>
public sealed record BrainstormingIdentityMap(IReadOnlyDictionary<Guid, Guid> Documents, IReadOnlyDictionary<Guid, Guid> Sections,
    IReadOnlyDictionary<Guid, Guid> Pages, IReadOnlyDictionary<Guid, Guid> Nodes, bool PreserveServerIds = false)
{
    private Guid Map(Guid id, IReadOnlyDictionary<Guid, Guid> map) => map.TryGetValue(id, out var value) && value != Guid.Empty
        ? value : PreserveServerIds && id != Guid.Empty ? id : throw new InvalidDataException("Synchronize or map every selected source/target first. No local identity was sent as a cloud identity.");
    private Guid? Map(Guid? id, IReadOnlyDictionary<Guid, Guid> map) => id is null ? null : Map(id.Value, map);
    public BrainstormingIdentityMap Reverse() => new(Documents.ToDictionary(x => x.Value, x => x.Key), Sections.ToDictionary(x => x.Value, x => x.Key),
        Pages.ToDictionary(x => x.Value, x => x.Key), Nodes.ToDictionary(x => x.Value, x => x.Key), PreserveServerIds);
    public BrainstormingSession Convert(BrainstormingSession session, BrainstormingScope destination)
    {
        if (Map(session.Scope.DocumentId, Documents) != destination.DocumentId || PreserveServerIds && session.Scope.DocumentId != destination.DocumentId) throw new InvalidDataException("Manuscript mapping changed.");
        var result = BrainstormingPersistence.ReScope(session, destination) with {
            Targets = session.Targets.Select(t => t with { DocumentId = Map(t.DocumentId, Documents), SectionId = Map(t.SectionId, Sections), PageId = Map(t.PageId, Pages),
                ChapterNodeId = Map(t.ChapterNodeId, Nodes), SceneNodeId = Map(t.SceneNodeId, Nodes), ParentNodeId = Map(t.ParentNodeId, Nodes),
                BeforeSectionId = Map(t.BeforeSectionId, Sections), BeforeNodeId = Map(t.BeforeNodeId, Nodes) }).ToArray(),
            Contexts = session.Contexts.Select(c => c with { Scope = destination, Sources = c.Sources.Select(s => s with {
                DocumentId = Map(s.DocumentId, Documents), SectionId = Map(s.SectionId, Sections), PageId = Map(s.PageId, Pages),
                Parts = s.Parts?.Select(p => p with { SectionId = Map(p.SectionId, Sections), PageId = Map(p.PageId, Pages) }).ToArray() }).ToArray() }).ToArray(),
            EnabledSources = session.EnabledSources?.Select(s => s with { DocumentId = Map(s.DocumentId, Documents), EntityKey = PlanningKey(s.EntityKey,
                s.Kind is BrainstormingSourceKind.Character or BrainstormingSourceKind.Place or BrainstormingSourceKind.Timeline) }).ToArray(),
            Planning = session.Planning.Select(p => p with { Scope = destination, EntityKey = PlanningKey(p.EntityKey, p.Kind == BrainstormingPlanningKind.CharacterNote && !p.AuthoredDocumentNotes) }).ToArray()
        };
        BrainstormingValidation.Validate(result); return result;
    }
    private string? PlanningKey(string? key, bool canon) {
        if (canon || !Guid.TryParse(key, out var id)) return key;
        foreach (var map in new[] { Documents, Nodes, Sections, Pages }) if (map.TryGetValue(id, out var value)) return value.ToString();
        if (PreserveServerIds) return key;
        throw new InvalidDataException("Planning entity has no explicit server mapping.");
    }
}
