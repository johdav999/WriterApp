namespace WriterApp.Data.Collaboration;

// Permits exist only inside the checked command's serializable transaction. Other
// connections cannot observe one before it is removed. They never authorize prose writes.
public static class CollaborationStructureSchema
{
    public static IEnumerable<string> Install(bool sqlServer)
    {
        yield return sqlServer
            ? "CREATE TABLE CollaborationStructurePermits (DocumentId uniqueidentifier NOT NULL PRIMARY KEY, OperationId uniqueidentifier NOT NULL);"
            : "CREATE TABLE CollaborationStructurePermits (DocumentId TEXT NOT NULL PRIMARY KEY, OperationId TEXT NOT NULL);";
        foreach (var table in new[] { "Pages", "Sections" })
        {
            foreach (var suffix in sqlServer ? new[] { "" } : new[] { "_INSERT", "_UPDATE", "_DELETE" })
                yield return $"DROP TRIGGER CollaborationFence_{table}{suffix};";
            if (sqlServer)
            {
                var content = table == "Pages" ? "IF EXISTS(SELECT 1 FROM inserted i JOIN deleted o ON i.Id=o.Id JOIN Documents d ON d.Id=o.DocumentId WHERE d.CollaborationMode<>0 AND i.Content<>o.Content) THROW 51070,'collaboration_legacy_write_blocked',1; " : "";
                var body = $"CREATE TRIGGER CollaborationFence_{table} ON {table} AFTER INSERT,UPDATE,DELETE AS BEGIN SET NOCOUNT ON; "
                    + "DECLARE @mode int; SELECT @mode=MAX(CONVERT(int,d.CollaborationMode)) FROM Documents d WITH(UPDLOCK,HOLDLOCK) WHERE d.Id IN(SELECT DocumentId FROM inserted UNION SELECT DocumentId FROM deleted); "
                    + "IF @mode=1 THROW 51071,'collaboration_migration_in_progress',1; "
                    + "IF EXISTS(SELECT 1 FROM Documents d WHERE d.CollaborationMode<>0 AND d.Id IN(SELECT DocumentId FROM inserted UNION SELECT DocumentId FROM deleted) AND NOT EXISTS(SELECT 1 FROM CollaborationStructurePermits p WHERE p.DocumentId=d.Id)) THROW 51070,'collaboration_legacy_write_blocked',1; "
                    + "IF EXISTS(SELECT 1 FROM deleted o JOIN Documents d ON d.Id=o.DocumentId WHERE d.CollaborationMode<>0 AND NOT EXISTS(SELECT 1 FROM inserted i WHERE i.Id=o.Id AND i.DocumentId=o.DocumentId)) THROW 51070,'collaboration_legacy_write_blocked',1; "
                    + content + "END;";
                yield return "EXEC(N'" + body.Replace("'", "''") + "');";
            }
            else foreach (var action in new[] { "INSERT", "UPDATE", "DELETE" })
            {
                var ids = action == "INSERT" ? "NEW.DocumentId" : action == "DELETE" ? "OLD.DocumentId" : "OLD.DocumentId,NEW.DocumentId";
                var extra = action == "DELETE" ? "1=1" : action == "UPDATE" ? "OLD.DocumentId IS NOT NEW.DocumentId OR OLD.Id IS NOT NEW.Id" + (table == "Pages" ? " OR OLD.Content IS NOT NEW.Content" : "") : "0=1";
                yield return $"CREATE TRIGGER CollaborationFence_{table}_{action} BEFORE {action} ON {table} BEGIN "
                    + $"SELECT RAISE(ABORT,'collaboration_migration_in_progress') WHERE EXISTS(SELECT 1 FROM Documents WHERE Id IN({ids}) AND CollaborationMode=1); "
                    + $"SELECT RAISE(ABORT,'collaboration_legacy_write_blocked') WHERE EXISTS(SELECT 1 FROM Documents d WHERE d.Id IN({ids}) AND d.CollaborationMode<>0 AND (({extra}) OR NOT EXISTS(SELECT 1 FROM CollaborationStructurePermits p WHERE p.DocumentId=d.Id))); END;";
            }
        }
    }
    public static IEnumerable<string> Uninstall(bool sqlServer)
    {
        foreach (var table in new[] { "Pages", "Sections" })
            foreach (var suffix in sqlServer ? new[] { "" } : new[] { "_INSERT", "_UPDATE", "_DELETE" }) yield return $"DROP TRIGGER CollaborationFence_{table}{suffix};";
        foreach (var sql in CollaborationWriteFenceSchema.Install(sqlServer).Where(s => s.Contains("CREATE TRIGGER CollaborationFence_Pages") || s.Contains("CREATE TRIGGER CollaborationFence_Sections"))) yield return sql;
        yield return "DROP TABLE CollaborationStructurePermits;";
    }
}
