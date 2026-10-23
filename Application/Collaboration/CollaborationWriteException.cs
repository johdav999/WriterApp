using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using WriterApp.Shared.Collaboration;

namespace WriterApp.Application.Collaboration;

public sealed class CollaborationWriteException(CollaborationError error, Exception? inner = null) : Exception(error.Message, inner)
{
    public CollaborationError Error { get; } = error;

    public static CollaborationError ForMode(CollaborationMode mode) => mode == CollaborationMode.Migrating
        ? new(CollaborationErrors.MigrationInProgress, "This manuscript is being prepared for collaboration. Keep your local draft and try again after migration completes.")
        : new(CollaborationErrors.LegacyWriteBlocked, "This manuscript requires a compatible collaboration editor. Your local draft must be preserved; snapshot saves cannot replace shared writing.");

    public static CollaborationWriteException? FromStorage(Exception error)
    {
        for (Exception? current = error; current is not null; current = current.InnerException)
        {
            if (current is CollaborationWriteException known) return known;
            bool recognized = current is SqlException sql && sql.Number is 51070 or 51071
                || current is SqliteException sqlite && sqlite.SqliteErrorCode == 19;
            if (!recognized) continue;
            if (current.Message.Contains(CollaborationErrors.MigrationInProgress, StringComparison.Ordinal))
                return new(ForMode(CollaborationMode.Migrating), error);
            if (current.Message.Contains(CollaborationErrors.LegacyWriteBlocked, StringComparison.Ordinal))
                return new(ForMode(CollaborationMode.Collaborative), error);
        }
        return null;
    }
}
