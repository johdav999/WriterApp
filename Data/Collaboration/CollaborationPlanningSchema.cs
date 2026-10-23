namespace WriterApp.Data.Collaboration;

// A permit is inserted and removed in the same locked, serializable reviewed
// transaction. It does not authorize prose, document or cross-document writes.
public static class CollaborationPlanningSchema
{
    public static IEnumerable<string> Install(bool sqlServer)
    {
        yield return sqlServer
            ? "CREATE TABLE CollaborationPlanningPermits (DocumentId uniqueidentifier NOT NULL PRIMARY KEY, OperationId uniqueidentifier NOT NULL);"
            : "CREATE TABLE CollaborationPlanningPermits (DocumentId TEXT NOT NULL PRIMARY KEY, OperationId TEXT NOT NULL);";
        foreach (var table in new[] { "DocumentSynopses", "ProjectNodes", "SceneCards", "SceneNotes" })
        {
            bool scene=table is "SceneCards" or "SceneNotes";
            foreach (var suffix in scene ? Array.Empty<string>() : sqlServer ? new[] { "" } : new[] { "_INSERT", "_UPDATE", "_DELETE" })
                yield return $"DROP TRIGGER CollaborationFence_{table}{suffix};";
            if (sqlServer)
            {
                string affected = scene ? "SELECT d.Id FROM Documents d JOIN ProjectNodes n ON n.ProjectId=d.ProjectId JOIN (SELECT SceneNodeId FROM inserted UNION SELECT SceneNodeId FROM deleted) r ON r.SceneNodeId=n.Id" : table == "ProjectNodes"
                    ? "SELECT d.Id FROM Documents d JOIN (SELECT ProjectId FROM inserted UNION SELECT ProjectId FROM deleted) r ON r.ProjectId=d.ProjectId"
                    : "SELECT DocumentId FROM inserted UNION SELECT DocumentId FROM deleted";
                string permitted = scene ? "NOT EXISTS(SELECT 1 FROM ProjectNodes n JOIN (SELECT SceneNodeId FROM inserted UNION SELECT SceneNodeId FROM deleted) r ON r.SceneNodeId=n.Id WHERE n.ProjectId=d.ProjectId AND (n.DocumentId IS NULL OR NOT EXISTS(SELECT 1 FROM CollaborationPlanningPermits p JOIN Documents target ON target.Id=p.DocumentId WHERE p.DocumentId=n.DocumentId AND target.ProjectId=n.ProjectId AND target.CollaborationMode=2)))" : table == "ProjectNodes"
                    ? "NOT EXISTS(SELECT 1 FROM (SELECT DocumentId,ProjectId FROM inserted UNION SELECT DocumentId,ProjectId FROM deleted) r WHERE r.ProjectId=d.ProjectId AND (r.DocumentId IS NULL OR NOT EXISTS(SELECT 1 FROM CollaborationPlanningPermits p WHERE p.DocumentId=r.DocumentId) OR NOT EXISTS(SELECT 1 FROM Documents target WHERE target.Id=r.DocumentId AND target.ProjectId=r.ProjectId AND target.CollaborationMode=2)))"
                    : "EXISTS(SELECT 1 FROM CollaborationPlanningPermits p WHERE p.DocumentId=d.Id)";
                string identity = table == "ProjectNodes" ? "i.DocumentId<>o.DocumentId OR i.ProjectId<>o.ProjectId OR i.DocumentId IS NULL OR o.DocumentId IS NULL" : "i.DocumentId<>o.DocumentId";
                var body = $"CREATE TRIGGER CollaborationFence_{table} ON {table} AFTER INSERT,UPDATE,DELETE AS BEGIN SET NOCOUNT ON; "
                    + $"DECLARE @mode int; SELECT @mode=MAX(CONVERT(int,d.CollaborationMode)) FROM Documents d WITH(UPDLOCK,HOLDLOCK) WHERE d.Id IN({affected}); "
                    + "IF @mode=1 THROW 51071,'collaboration_migration_in_progress',1; "
                    + $"IF EXISTS(SELECT 1 FROM Documents d WHERE d.CollaborationMode<>0 AND d.Id IN({affected}) AND NOT ({permitted})) THROW 51070,'collaboration_legacy_write_blocked',1; "
                    + (table == "ProjectNodes" ? $"IF EXISTS(SELECT 1 FROM inserted i JOIN deleted o ON i.Id=o.Id WHERE ({identity}) AND EXISTS(SELECT 1 FROM Documents d WHERE d.ProjectId IN(i.ProjectId,o.ProjectId) AND d.CollaborationMode<>0)) THROW 51070,'collaboration_legacy_write_blocked',1; " : "") + "END;";
                yield return "EXEC(N'" + body.Replace("'", "''") + "');";
            }
            else foreach (var action in new[] { "INSERT", "UPDATE", "DELETE" })
            {
                var rows = action == "UPDATE" ? new[] { "OLD", "NEW" } : new[] { action == "DELETE" ? "OLD" : "NEW" };
                string affected = string.Join(" UNION ", rows.Select(r => scene ? $"SELECT d.Id FROM Documents d JOIN ProjectNodes n ON n.ProjectId=d.ProjectId WHERE n.Id={r}.SceneNodeId" : table == "ProjectNodes" ? $"SELECT Id FROM Documents WHERE ProjectId={r}.ProjectId" : $"SELECT {r}.DocumentId"));
                string allowed = string.Join(" AND ", rows.Select(r => scene ? $"EXISTS(SELECT 1 FROM CollaborationPlanningPermits p JOIN Documents target ON target.Id=p.DocumentId JOIN ProjectNodes n ON n.DocumentId=target.Id AND n.ProjectId=target.ProjectId WHERE n.Id={r}.SceneNodeId AND target.CollaborationMode=2)" : $"EXISTS(SELECT 1 FROM CollaborationPlanningPermits p JOIN Documents target ON target.Id=p.DocumentId WHERE p.DocumentId={r}.DocumentId AND target.CollaborationMode=2" + (table == "ProjectNodes" ? $" AND target.ProjectId={r}.ProjectId" : "") + ")"));
                string identity = table == "ProjectNodes" && action == "UPDATE" ? " OR OLD.Id IS NOT NEW.Id OR OLD.DocumentId IS NOT NEW.DocumentId OR OLD.ProjectId IS NOT NEW.ProjectId" : "";
                yield return $"CREATE TRIGGER CollaborationFence_{table}_{action} BEFORE {action} ON {table} BEGIN "
                    + $"SELECT RAISE(ABORT,'collaboration_migration_in_progress') WHERE EXISTS(SELECT 1 FROM Documents WHERE Id IN({affected}) AND CollaborationMode=1); "
                    + $"SELECT RAISE(ABORT,'collaboration_legacy_write_blocked') WHERE EXISTS(SELECT 1 FROM Documents d WHERE d.Id IN({affected}) AND d.CollaborationMode<>0 AND (NOT ({allowed}){identity})); END;";
            }
        }
        // Legacy sync triggers touch Documents/Projects even for a planning-only
        // change. Shared revisions are derived from committed content and planning,
        // so keep those immutable legacy timestamps/flags outside shared writes.
        foreach(var sql in SyncTriggers(sqlServer,true)) yield return sql;
    }
    public static IEnumerable<string> Uninstall(bool sqlServer)
    {
        foreach (var table in new[] { "DocumentSynopses", "ProjectNodes", "SceneCards", "SceneNotes" })
            foreach (var suffix in sqlServer ? new[] { "" } : new[] { "_INSERT", "_UPDATE", "_DELETE" }) yield return $"DROP TRIGGER CollaborationFence_{table}{suffix};";
        foreach (var sql in CollaborationWriteFenceSchema.Install(sqlServer).Where(s => s.Contains("CREATE TRIGGER CollaborationFence_DocumentSynopses") || s.Contains("CREATE TRIGGER CollaborationFence_ProjectNodes"))) yield return sql;
        yield return "DROP TABLE CollaborationPlanningPermits;";
        foreach(var sql in SyncTriggers(sqlServer,false)) yield return sql;
    }
    private static IEnumerable<string> SyncTriggers(bool sqlServer,bool sharedAware)
    {
        foreach(var sql in WriterApp.Data.Documents.MultiDocumentSyncSchemaV4.Install(sqlServer).Where(s=>new[]{"ProjectNodes","DocumentSynopses","SceneCards","SceneNotes"}.Any(t=>s.Contains("Sync_"+t))))
            yield return !sharedAware ? sql : sql.Replace("WHERE Id IN (", "WHERE CollaborationMode=0 AND Id IN (")
                .Replace("WHERE SyncEnabled=0 AND", "WHERE SyncEnabled=0 AND NOT EXISTS(SELECT 1 FROM Documents shared WHERE shared.ProjectId=Projects.Id AND shared.CollaborationMode<>0) AND")
                .Replace("WHERE PlanningSyncEnabled=0 AND", "WHERE PlanningSyncEnabled=0 AND NOT EXISTS(SELECT 1 FROM Documents shared WHERE shared.ProjectId=Projects.Id AND shared.CollaborationMode<>0) AND");
    }
}
