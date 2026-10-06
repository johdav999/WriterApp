using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Serialization;
using WriterApp.Device.Shared.Services;
namespace WriterApp.Device.Shared.Storage;

public sealed record LocalCover(Guid Id, Guid DocumentId, Guid? ProjectId, string FileName, byte[] Png,
    string? RemoteReference = null, string? ContentHash = null, string? AccountScope = null)
{
    [JsonIgnore] public string DataUri => "data:image/png;base64," + Convert.ToBase64String(Png);
    public static LocalCover Create(LocalDocument doc, DeviceImportFile file)
    {
        Validate(file.Content);
        return new(Guid.NewGuid(),doc.DocumentId,doc.Project?.ProjectId,Path.GetFileName(file.FileName),file.Content.ToArray());
    }
    public static LocalCover? FromProject(LocalDocument doc)
    {
        if(doc.Project?.CoverImageUrl is not { } url || !url.StartsWith("data:",StringComparison.OrdinalIgnoreCase)) return null;
        byte[] png=WriterApp.Shared.CoverStudioContract.ReadPng(url);
        return new(doc.Project.CoverChangeId ?? doc.Project.ProjectId, doc.DocumentId,doc.Project.ProjectId,"project-cover.png",png);
    }
    public static void Validate(byte[] bytes) => WriterApp.Shared.CoverStudioContract.ValidatePng(bytes);

}
public sealed record LocalExportPreset(Guid Id, string Name, LocalPublishingOptions Options);
public sealed record LocalPublishingState(int Version, Guid DocumentId, Guid? ProjectId, long Revision,
    LocalPublishingOptions Options, IReadOnlyList<LocalExportPreset> Presets, LocalCover? Cover);

public sealed class LocalPublishingStore(string directory)
{
    private readonly SemaphoreSlim _gate = new(1,1);
    private static readonly JsonSerializerOptions Json = new() { UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow };
    public async Task<LocalPublishingState> LoadAsync(LocalDocument doc, CancellationToken ct=default)
    {
        string path=Path.Combine(directory,doc.DocumentId.ToString("N")+".json");
        if(!File.Exists(path)) return new(1,doc.DocumentId,doc.Project?.ProjectId,0,new(),[],null);
        if(new FileInfo(path).Length>4*1024*1024) throw new InvalidDataException("Publishing settings exceed the safe size limit.");
        var state=JsonSerializer.Deserialize<LocalPublishingState>(await File.ReadAllBytesAsync(path,ct),Json) ?? throw new InvalidDataException("Invalid publishing settings.");
        if(state.Version!=1 || state.DocumentId!=doc.DocumentId || state.ProjectId!=doc.Project?.ProjectId || state.Options is null || state.Presets is null)
            throw new InvalidDataException("Publishing settings belong to another project or a newer app. The original file is preserved.");
        ValidatePresets(state);
        if(state.Cover is { } cover) { if(cover.DocumentId!=doc.DocumentId || cover.ProjectId!=doc.Project?.ProjectId) throw new InvalidDataException("Cover association mismatch."); LocalCover.Validate(cover.Png); }
        return state;
    }
    public async Task<LocalPublishingState> SaveAsync(LocalDocument doc, LocalPublishingState state, CancellationToken ct=default)
    {
        await _gate.WaitAsync(ct);
        try {
            var current=await LoadAsync(doc,ct);
            if(current.Revision!=state.Revision || state.DocumentId!=doc.DocumentId || state.ProjectId!=doc.Project?.ProjectId) throw new IOException("Publishing settings changed. Reopen before saving.");
            ValidatePresets(state);
            if(state.Cover is { } cover) {
                if(cover.DocumentId!=doc.DocumentId || cover.ProjectId!=doc.Project?.ProjectId) throw new InvalidDataException("Cover association mismatch.");
                LocalCover.Validate(cover.Png);
            }
            state=state with { Revision=state.Revision+1 };
            Directory.CreateDirectory(directory);
            byte[] bytes=JsonSerializer.SerializeToUtf8Bytes(state,Json);
            if(bytes.Length>4*1024*1024) throw new InvalidDataException("Publishing settings exceed the safe size limit.");
            await new AtomicDocumentWriter().WriteAsync(Path.Combine(directory,doc.DocumentId.ToString("N")+".json"),bytes,ct);
            return state;
        } finally { _gate.Release(); }
    }
    private static void ValidatePresets(LocalPublishingState state)
    {
        if(state.Version!=1 || state.Options is null || state.Presets is null || state.Presets.Count>50
            || state.Presets.Any(p=>p is null || string.IsNullOrWhiteSpace(p.Name) || p.Name.Length>100 || p.Options is null))
            throw new InvalidDataException("Use up to 50 valid presets with names of 1–100 characters.");
    }
}
