using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace WriterApp.Shared;

public sealed record WritingOutlineSection(Guid Id, int Order, string Title);
public sealed record WritingOutlineNode(Guid Id, Guid? ParentId, int Order, string Type, string Title, Guid? SectionId);
public sealed record WritingOutlineSnapshot(int Version, Guid DocumentId, Guid? ProjectId, string DocumentVersion,
    IReadOnlyList<WritingOutlineSection> Sections, IReadOnlyList<WritingOutlineNode> Nodes, string Fingerprint);

public static class WritingOutline
{
    public const string Option = "saved_writing_outline";
    public const int MaxEntities = 1000, MaxTitleCharacters = 20000, MaxJsonCharacters = 262144;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static bool Consumes(string key) => WritingActions.SelectionKeys.Contains(key) || WritingActions.SectionKeys.Contains(key)
        || key is "custom_transform" or "propose.next-paragraph";
    private static string Payload(WritingOutlineSnapshot value) => JsonSerializer.Serialize(new {
        version=value.Version, documentId=value.DocumentId, projectId=value.ProjectId, sections=value.Sections, nodes=value.Nodes
    }, Json);
    public static WritingOutlineSnapshot Create(Guid document, Guid? project, string documentVersion,
        IEnumerable<WritingOutlineSection> sections, IEnumerable<WritingOutlineNode> nodes)
    {
        var value = new WritingOutlineSnapshot(1,document,project,documentVersion,
            sections.OrderBy(s=>s.Order).ThenBy(s=>s.Id).ToArray(),
            nodes.OrderBy(n=>n.ParentId).ThenBy(n=>n.Order).ThenBy(n=>n.Id).ToArray(), "");
        ValidateShape(value);
        return value with { Fingerprint=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Payload(value)))) };
    }
    private static void ValidateShape(WritingOutlineSnapshot value)
    {
        if(value is null || value.Version!=1 || value.DocumentId==Guid.Empty || value.ProjectId==Guid.Empty
            || value.DocumentVersion is not {Length:>0 and <=64} || value.Sections is null || value.Nodes is null
            || value.Sections.Count>MaxEntities || value.Nodes.Count>MaxEntities || value.ProjectId is null && value.Nodes.Count!=0)
            throw new InvalidDataException("Unsupported saved writing outline. Save and synchronize this document or update the backend.");
        if(value.Sections.Any(s=>s is null || s.Id==Guid.Empty || s.Order<0 || s.Title is null || s.Title.Length>200)
            || value.Nodes.Any(n=>n is null || n.Id==Guid.Empty || n.Order<0 || n.Title is null || n.Title.Length>200
                || n.Type is not ("part" or "chapter" or "scene" or "frontmatteritem"))
            || value.Sections.Select(s=>s.Id).Distinct().Count()!=value.Sections.Count
            || value.Nodes.Select(n=>n.Id).Distinct().Count()!=value.Nodes.Count)
            throw new InvalidDataException("Invalid saved outline identities or titles. Correct the saved structure before generating.");
        var sections=value.Sections.Select(s=>s.Id).ToHashSet(); var nodes=value.Nodes.ToDictionary(n=>n.Id);
        foreach(var node in value.Nodes) {
            if(node.SectionId is { } section && !sections.Contains(section))throw new InvalidDataException("Outline links another document's section.");
            var visited=new HashSet<Guid>{node.Id}; var parent=node.ParentId;
            while(parent is { } id) {
                if(!visited.Add(id) || !nodes.TryGetValue(id,out var ancestor))throw new InvalidDataException("Outline contains a cycle or missing parent.");
                parent=ancestor.ParentId;
            }
        }
        if(value.Sections.Sum(s=>(long)s.Title.Length)+value.Nodes.Sum(n=>(long)n.Title.Length)>MaxTitleCharacters
            || Payload(value).Length>MaxJsonCharacters)
            throw new InvalidDataException("Saved outline exceeds the supported 1,000 sections/nodes or 20,000 title characters. No outline is silently truncated.");
    }
    public static void Validate(WritingOutlineSnapshot value)
    {
        ValidateShape(value);
        var canonical=Create(value.DocumentId,value.ProjectId,value.DocumentVersion,value.Sections,value.Nodes);
        if(value.Fingerprint!=canonical.Fingerprint || JsonSerializer.Serialize(value,Json)!=JsonSerializer.Serialize(canonical,Json))
            throw new InvalidDataException("Saved outline fingerprint or ordering is invalid. Capture it again.");
    }
    public static string ProviderText(WritingOutlineSnapshot value) { Validate(value);return Payload(value); }
    public static string? FromOptions(Dictionary<string,object?>? options) => options?.GetValueOrDefault(Option)?.ToString();
}
