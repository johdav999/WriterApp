namespace WriterApp.Data.Documents;

// Additive triggers. Touching each affected manuscript uses the existing transactional version clock.
public static class ProjectSyncSchemaV2
{
    public static IEnumerable<string> Install(bool sqlServer)
    {
        yield return "UPDATE Projects SET SyncEnabled=1 WHERE Id IN (SELECT ProjectId FROM ProjectNodes);";
        foreach (string table in new[] { "Projects", "ProjectNodes", "SceneNotes", "SceneCards" })
        {
            string Project(string row) => table == "Projects" ? $"SELECT Id FROM {row}"
                : table == "ProjectNodes" ? $"SELECT ProjectId FROM {row}"
                : $"SELECT n.ProjectId FROM ProjectNodes n JOIN {row} r ON r.SceneNodeId=n.Id";
            if (sqlServer)
            {
                string ids = Project("inserted") + " UNION " + Project("deleted");
                string body = $"CREATE TRIGGER Sync_{table} ON {table} AFTER INSERT,UPDATE,DELETE AS BEGIN SET NOCOUNT ON; "
                    + (table == "ProjectNodes" ? $"UPDATE Projects SET SyncEnabled=1 WHERE SyncEnabled=0 AND Id IN ({ids}); " : "")
                    + $"UPDATE Documents SET UpdatedAt=SYSDATETIMEOFFSET() WHERE ProjectId IN ({ids}); END;";
                yield return "EXEC(N'" + body.Replace("'", "''") + "');";
            }
            else foreach (string action in new[] { "INSERT", "UPDATE", "DELETE" })
            {
                string row = action == "DELETE" ? "OLD" : "NEW";
                string id = table == "Projects" ? $"{row}.Id" : table == "ProjectNodes" ? $"{row}.ProjectId"
                    : $"(SELECT ProjectId FROM ProjectNodes WHERE Id={row}.SceneNodeId)";
                // Parent ownership changes are not supported by the API; both sides still invalidate on raw SQL moves.
                string old = action == "UPDATE" ? table == "Projects" ? "OLD.Id" : table == "ProjectNodes" ? "OLD.ProjectId" : "(SELECT ProjectId FROM ProjectNodes WHERE Id=OLD.SceneNodeId)" : id;
                yield return $"CREATE TRIGGER Sync_{table}_{action} AFTER {action} ON {table} BEGIN "
                    + (table == "ProjectNodes" ? $"UPDATE Projects SET SyncEnabled=1 WHERE SyncEnabled=0 AND Id IN ({id},{old}); " : "")
                    + $"UPDATE Documents SET UpdatedAt=strftime('%Y-%m-%d %H:%M:%f+00:00','now') WHERE ProjectId IN ({id},{old}); END;";
            }
        }
    }
    public static IEnumerable<string> Uninstall(bool sqlServer) => new[] { "Projects", "ProjectNodes", "SceneNotes", "SceneCards" }
        .SelectMany(t => sqlServer ? new[] { $"DROP TRIGGER Sync_{t};" } : new[] { "INSERT", "UPDATE", "DELETE" }.Select(a => $"DROP TRIGGER Sync_{t}_{a};"));
}
