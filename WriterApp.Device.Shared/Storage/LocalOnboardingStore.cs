using System.Text.Json;

namespace WriterApp.Device.Shared.Storage;

public sealed record LocalOnboardingProgress(int Version, Guid Id, string Scope, long Revision, int Step,
    string Status, Guid? PracticeDocumentId = null, bool PracticeCreated = false,
    Guid? DemoBootstrapId = null, Guid? DemoDocumentId = null, WriterApp.Shared.OnboardingDemoStatus? Demo = null,
    WriterApp.Shared.OnboardingDemoProgressRequest? PendingProgress = null);

/// <summary>Scoped guidance, demo identities and cached server status; no provider results, tokens or locally granted entitlements.</summary>
public sealed class LocalOnboardingStore(string root)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly AtomicDocumentWriter _writer = new();
    public Task<LocalOnboardingProgress> ReadAsync(string scope, CancellationToken ct = default) =>
        Locked(scope, () => Read(scope, ct), ct);
    public Task<LocalOnboardingProgress> UpdateAsync(LocalOnboardingProgress source,
        Func<LocalOnboardingProgress, LocalOnboardingProgress> update, CancellationToken ct = default) => Locked(source.Scope, async () => {
            var current = await Read(source.Scope, ct);
            if (current.Id != source.Id || current.Revision != source.Revision)
                throw new InvalidOperationException("The practice guide changed in another window. Reopen it and retry.");
            var next = update(current) with { Version = 3, Revision = checked(current.Revision + 1) };
            if (next.Id != current.Id || next.Scope != current.Scope || next.PracticeDocumentId != current.PracticeDocumentId && current.PracticeDocumentId is not null
                || current.PracticeCreated && !next.PracticeCreated
                || current.DemoBootstrapId is not null && next.DemoBootstrapId != current.DemoBootstrapId
                || current.DemoDocumentId is not null && next.DemoDocumentId != current.DemoDocumentId
                || current.Demo?.Workspace is { } cloud && next.Demo?.Workspace!=cloud
                || current.PendingProgress is not null && next.PendingProgress is not null && current.PendingProgress!=next.PendingProgress)
                throw new InvalidDataException("Practice/demo identity or pending operation cannot be replaced.");
            Validate(next);
            await _writer.WriteAsync(PathFor(source.Scope), JsonSerializer.SerializeToUtf8Bytes(next), ct);
            return next;
        }, ct);
    private string PathFor(string scope) => Path.Combine(root, scope + ".json");
    private async Task<LocalOnboardingProgress> Read(string scope, CancellationToken ct) {
        var path = PathFor(scope);
        if (!File.Exists(path)) {
            var initial = new LocalOnboardingProgress(3, Guid.NewGuid(), scope, 0, 0, "Active");
            await _writer.WriteAsync(path, JsonSerializer.SerializeToUtf8Bytes(initial), ct);
            return initial;
        }
        if (new FileInfo(path).Length > 16_384) throw new InvalidDataException("Practice guide state is too large. Its file was preserved.");
        var state = JsonSerializer.Deserialize<LocalOnboardingProgress>(await File.ReadAllBytesAsync(path, ct))
            ?? throw new InvalidDataException("Practice guide state is unreadable. Its file was preserved.");
        Validate(state);
        if (state.Scope != scope) throw new InvalidDataException("Practice guide belongs to another account or backend.");
        if (state.Version == 1) {
            // Version 1 used the same identity/steps. Migration never loads or writes a manuscript.
            var original = await File.ReadAllBytesAsync(path, ct);
            if (!File.Exists(path + ".v1.bak")) await _writer.WriteAsync(path + ".v1.bak", original, ct);
            else if (!(await File.ReadAllBytesAsync(path + ".v1.bak", ct)).SequenceEqual(original))
                throw new IOException("The practice guide migration backup differs. Both files were preserved.");
            state = state with { Version = 2, Revision = checked(state.Revision + 1), PracticeCreated = state.PracticeDocumentId is not null };
            await _writer.WriteAsync(path, JsonSerializer.SerializeToUtf8Bytes(state), ct);
        }
        return state;
    }
    private static void Validate(LocalOnboardingProgress state) {
        if (state.Version is not (1 or 2 or 3) || state.Id == Guid.Empty || state.Revision < 0 || state.Step is < 0 or > 4
            || state.Status is not ("Active" or "Skipped" or "Completed") || state.PracticeDocumentId == Guid.Empty
            || state.PracticeCreated && state.PracticeDocumentId is null || state.DemoDocumentId==Guid.Empty || state.DemoBootstrapId==Guid.Empty
            || state.Demo is { } demo && (demo.Version!=1 || demo.OnboardingStep is <0 or >10 || demo.Revision is not {Length:64}
                || demo.Reason is not {Length:<=200} || demo.ActionKey!=WriterApp.Shared.OnboardingAiDemoRequest.ActionKey || demo.Scope!="section"
                || demo.Workspace is { } ids && (ids.ProjectId==Guid.Empty || ids.DocumentId==Guid.Empty || ids.SectionId==Guid.Empty || ids.SceneNodeId==Guid.Empty))
            || state.PendingProgress is { } pending && (pending.Version!=1 || pending.OperationId==Guid.Empty || pending.Step is <0 or >10 || pending.ExpectedRevision is not {Length:64}))
            throw new InvalidDataException("Unsupported practice guide state. Its file was preserved.");
    }
    private async Task<T> Locked<T>(string scope, Func<Task<T>> action, CancellationToken ct) {
        if (scope.Length != 64 || scope.Any(c => !Uri.IsHexDigit(c))) throw new InvalidDataException("Invalid practice scope.");
        await _gate.WaitAsync(ct);
        try {
            Directory.CreateDirectory(root);
            using var lease = new FileStream(Path.Combine(root, scope + ".lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            return await action();
        } finally { _gate.Release(); }
    }
}
