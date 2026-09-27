using System.Net.Http;
using System.Text.Json;

namespace WriterApp.Device.Shared.Services;

public enum DeviceDiagnosticEvent { AppStarted, SyncChanged, SaveFailed, UpdateChecked, DiagnosticExported }
public enum DeviceDiagnosticError { None, Network, Storage, Authentication, Other }
public sealed record DeviceSyncDiagnosticSnapshot(bool Running, int LinkedDocuments, int Pending, int Errors,
    int Conflicts, DateTimeOffset? LastSyncedAtUtc);

/// <summary>Writes only allowlisted event names, counts and error classes; never exception messages or document text.</summary>
public sealed class DeviceDiagnostics(string directory, string appVersion, DeviceEnvironment environment,
    TimeProvider? time = null, long maxFileBytes = 512 * 1024)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private DeviceSyncDiagnosticSnapshot? _lastSync;
    private string Active => Path.Combine(directory, "diagnostics.jsonl");
    private string Archive(int index) => Path.Combine(directory, $"diagnostics.{index}.jsonl");

    public async Task RecordAsync(DeviceDiagnosticEvent eventType,
        DeviceSyncDiagnosticSnapshot? sync = null, Exception? error = null, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(eventType)) throw new ArgumentOutOfRangeException(nameof(eventType));
        await _gate.WaitAsync(ct);
        try
        {
            if (eventType == DeviceDiagnosticEvent.SyncChanged && sync == _lastSync) return;
            if (eventType == DeviceDiagnosticEvent.SyncChanged) _lastSync = sync;
            // The record has no free-text field. Exception.Message, token headers, and writing never cross this boundary.
            var entry = new DiagnosticRecord(_time.GetUtcNow(), eventType.ToString(), appVersion,
                Environment.Version.ToString(), environment.ToString(), Classify(error).ToString(), sync);
            byte[] line = JsonSerializer.SerializeToUtf8Bytes(entry, Json);
            Directory.CreateDirectory(directory);
            if (File.Exists(Active) && new FileInfo(Active).Length + line.Length + 1 > maxFileBytes)
                Rotate();
            await using FileStream stream = new(Active, FileMode.Append, FileAccess.Write, FileShare.Read);
            await stream.WriteAsync(line, ct);
            await stream.WriteAsync(new byte[] { (byte)'\n' }, ct);
        }
        finally { _gate.Release(); }
    }

    public async Task<byte[]> ExportAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            using MemoryStream export = new();
            for (int index = 3; index >= 1; index--)
                await AppendIfPresentAsync(Archive(index), export, ct);
            await AppendIfPresentAsync(Active, export, ct);
            return export.ToArray();
        }
        finally { _gate.Release(); }
    }

    private static async Task AppendIfPresentAsync(string path, Stream destination, CancellationToken ct)
    {
        if (!File.Exists(path)) return;
        await using FileStream file = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        await file.CopyToAsync(destination, ct);
    }
    private void Rotate()
    {
        if (File.Exists(Archive(3))) File.Delete(Archive(3));
        for (int index = 2; index >= 1; index--)
            if (File.Exists(Archive(index))) File.Move(Archive(index), Archive(index + 1));
        File.Move(Active, Archive(1));
    }
    private static DeviceDiagnosticError Classify(Exception? error) => error switch
    {
        null => DeviceDiagnosticError.None,
        UnauthorizedAccessException => DeviceDiagnosticError.Storage,
        IOException => DeviceDiagnosticError.Storage,
        HttpRequestException or TimeoutException => DeviceDiagnosticError.Network,
        DeviceSignInRequiredException => DeviceDiagnosticError.Authentication,
        _ => DeviceDiagnosticError.Other
    };
    private sealed record DiagnosticRecord(DateTimeOffset TimestampUtc, string Event, string AppVersion,
        string RuntimeVersion, string Environment, string ErrorClass, DeviceSyncDiagnosticSnapshot? Sync);
}
