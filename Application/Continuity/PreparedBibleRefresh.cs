namespace WriterApp.Application.Continuity;

public sealed record PreparedBibleRefresh(Guid DocumentId, BibleType Type, BibleSnapshotState? Existing,
    string ContentJson, string SourceHash, BibleRefreshCursor Cursor, BibleRefreshStats Stats, bool Unchanged);
