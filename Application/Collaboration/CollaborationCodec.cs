using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Options;
using WriterApp.Shared.Collaboration;

namespace WriterApp.Application.Collaboration;

public sealed class CollaborationConversionOptions
{
    public bool Enabled { get; set; }
    public string NodeExecutable { get; set; } = "node";
    public string CodecPath { get; set; } = "WriterApp.Collaboration/src/codec.mjs";
}
public sealed record CollaborationCodecResult(string Html, JsonElement Json, string Text,
    CollaborationJournalSupport Support, byte[] State, string Sha256);
public interface ICollaborationCodec
{
    Task<CollaborationCodecResult> Seed(string content, string format, CancellationToken ct);
    Task<CollaborationCodecResult> Project(byte[][] updates, CancellationToken ct);
    Task<CollaborationAnchorState[]> Anchors(byte[][] updates, CollaborationAnchor[] anchors, CancellationToken ct, CollaborationJournalSupport? expectedSupport = null) => throw new NotSupportedException();
}

// Process arguments never contain prose or credentials. The child accepts one bounded stdin record.
public sealed class CollaborationCodec(IOptions<CollaborationConversionOptions> options, IWebHostEnvironment host) : ICollaborationCodec
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public Task<CollaborationCodecResult> Seed(string content, string format, CancellationToken ct) => ProjectRequest(new { operation = "seed", content, format }, ct);
    public Task<CollaborationCodecResult> Project(byte[][] updates, CancellationToken ct) => ProjectRequest(new { operation = "project", updates }, ct);
    public Task<CollaborationAnchorState[]> Anchors(byte[][] updates, CollaborationAnchor[] anchors, CancellationToken ct, CollaborationJournalSupport? expectedSupport = null) => Run<CollaborationAnchorState[]>(new { operation = "anchors", updates, anchors, expectedSupport }, ct);
    private async Task<CollaborationCodecResult> ProjectRequest(object request, CancellationToken ct)
    {
        var result = await Run<CollaborationCodecResult>(request, ct);
        if (result.State.Length is 0 or > 16 * 1024 * 1024 || result.Support.ContentContractVersion != 2 || result.Support.ProtocolVersion != 1
            || result.Support.SchemaFingerprint.Length != 64 || !result.Support.SchemaFingerprint.All(Uri.IsHexDigit)
            || !Convert.ToHexString(SHA256.HashData(result.State)).Equals(result.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new CollaborationCommitException("collaboration_codec_invalid");
        return result;
    }
    private async Task<T> Run<T>(object request, CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(request, Json);
        if (payload.Length > 24 * 1024 * 1024) throw new CollaborationCommitException("collaboration_codec_limit");
        var start = new ProcessStartInfo(options.Value.NodeExecutable) { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = host.ContentRootPath,
            StandardInputEncoding = new System.Text.UTF8Encoding(false), StandardOutputEncoding = new System.Text.UTF8Encoding(false), StandardErrorEncoding = new System.Text.UTF8Encoding(false) };
        start.ArgumentList.Add(Path.GetFullPath(options.Value.CodecPath, host.ContentRootPath));
        using var process = Process.Start(start) ?? throw new InvalidOperationException("collaboration_codec_unavailable");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            var output = ReadBounded(process.StandardOutput, timeout.Token);
            var error = ReadBounded(process.StandardError, timeout.Token);
            await process.StandardInput.WriteAsync(payload.AsMemory(), timeout.Token); process.StandardInput.Close();
            await process.WaitForExitAsync(timeout.Token);
            string value = await output; await error;
            if (process.ExitCode != 0) throw new CollaborationCommitException("collaboration_codec_rejected");
            return JsonSerializer.Deserialize<T>(value, Json) ?? throw new JsonException();
        }
        finally { if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(CancellationToken.None); } }
    }
    private static async Task<string> ReadBounded(StreamReader reader, CancellationToken ct)
    {
        var result = new System.Text.StringBuilder(); var buffer = new char[8192]; int read;
        while ((read = await reader.ReadAsync(buffer, ct)) > 0)
        { if (result.Length + read > 48 * 1024 * 1024) throw new InvalidOperationException("collaboration_codec_limit"); result.Append(buffer, 0, read); }
        return result.ToString();
    }
}
