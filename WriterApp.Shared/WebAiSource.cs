using System.Text.Json;

namespace WriterApp.Shared;

/// <summary>Owned source identity. Fingerprints exclude sync acknowledgements and timestamps.</summary>
public sealed record WebAiSource(int Version, string AccountKey, Guid DocumentId, Guid? ProjectId,
    string DocumentVersion, string Fingerprint, Guid? SectionId = null, Guid? PageId = null, Guid? SceneId = null);
public static class WebAiSources
{
    public const int MaxResponseBytes = 2_000_000;
    public static void Validate(WebAiSource source)
    {
        if (source.Version != 1 || source.DocumentId == Guid.Empty || source.AccountKey?.Length != 64
            || source.Fingerprint?.Length != 64 || string.IsNullOrWhiteSpace(source.DocumentVersion)
            || source.DocumentVersion.Length > 200 || source.SectionId == Guid.Empty || source.PageId == Guid.Empty || source.SceneId == Guid.Empty)
            throw new InvalidDataException("Missing checked AI source. Save and update the backend before generating.");
        if(!source.AccountKey.All(Uri.IsHexDigit) || !source.Fingerprint.All(Uri.IsHexDigit))throw new InvalidDataException("Invalid checked source fingerprint.");
    }
    public static bool Same(WebAiSource a, WebAiSource b) => a with { DocumentVersion = "ack" } == b with { DocumentVersion = "ack" };
    public static void RequireCurrent(WebAiSource current, WebAiSource original)
    {
        Validate(current); Validate(original);
        if (!Same(current, original)) throw new InvalidOperationException("Writing, planning, canon or target changed. Save and generate a new checked proposal.");
    }
    public static void RequireTime(DateTimeOffset created)
    {
        if (created > DateTimeOffset.UtcNow.AddMinutes(1) || DateTimeOffset.UtcNow - created > TimeSpan.FromHours(1))
            throw new InvalidDataException("This AI proposal is expired or has an invalid date. Generate again.");
    }
    public static async Task<T> Read<T>(HttpContent content, CancellationToken ct = default)
    {
        var input = await content.ReadAsStreamAsync(ct); using var output = new MemoryStream(); var block = new byte[16384]; int read;
        while ((read = await input.ReadAsync(block, ct)) > 0) { if (output.Length + read > MaxResponseBytes) throw new InvalidDataException("AI response exceeds the supported limit."); await output.WriteAsync(block.AsMemory(0, read), ct); }
        if(input.CanSeek)input.Position=0;
        return JsonSerializer.Deserialize<T>(output.ToArray(), new JsonSerializerOptions(JsonSerializerDefaults.Web) { MaxDepth = 32 }) ?? throw new InvalidDataException("Missing AI response.");
    }
}
