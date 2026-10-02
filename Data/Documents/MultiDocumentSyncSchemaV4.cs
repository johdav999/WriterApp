namespace WriterApp.Data.Documents;

// Manuscript edits invalidate their own snapshot. Shared metadata invalidates every member.
public static class MultiDocumentSyncSchemaV4
{
    private static readonly string[] Tables = ["Projects", "ProjectNodes", "SceneNotes", "SceneCards", "SceneAnnotations", "DocumentSynopses"];
    public static IEnumerable<string> Uninstall(bool sqlServer) => Tables.SelectMany(table => sqlServer
        ? new[] { $"DROP TRIGGER IF EXISTS Sync_{table};" }
        : new[] { "INSERT", "UPDATE", "DELETE" }.Select(action => $"DROP TRIGGER IF EXISTS Sync_{table}_{action};"));

    public static IEnumerable<string> Install(bool sqlServer)
    {
        foreach (var drop in Uninstall(sqlServer)) yield return drop;
        foreach (var table in Tables)
        {
            if (sqlServer)
            {
                string Select(string source) => table switch
                {
                    "Projects" => $"SELECT Id FROM Documents WHERE ProjectId IN (SELECT Id FROM {source})",
                    "ProjectNodes" => $"SELECT DocumentId FROM {source}",
                    "DocumentSynopses" => $"SELECT DocumentId FROM {source}",
                    _ => $"SELECT n.DocumentId FROM ProjectNodes n JOIN {source} r ON r.SceneNodeId=n.Id"
                };
                string guard = table == "Projects" ? "IF EXISTS (SELECT 1 FROM inserted) AND EXISTS (SELECT 1 FROM deleted) AND NOT EXISTS (SELECT Title,Subtitle,AuthorName,Language,Genre,DefaultExportSettingsJson,CoverImageUrl,PrimaryDocumentId,MetadataRevision FROM inserted EXCEPT SELECT Title,Subtitle,AuthorName,Language,Genre,DefaultExportSettingsJson,CoverImageUrl,PrimaryDocumentId,MetadataRevision FROM deleted) RETURN; " : "";
                string flags = table == "ProjectNodes" ? "UPDATE Projects SET SyncEnabled=1 WHERE SyncEnabled=0 AND Id IN (SELECT ProjectId FROM inserted); "
                    : table is "SceneAnnotations" or "DocumentSynopses" ? $"UPDATE Projects SET PlanningSyncEnabled=1 WHERE PlanningSyncEnabled=0 AND Id IN (SELECT ProjectId FROM Documents WHERE Id IN ({Select("inserted")})); " : "";
                string body = $"CREATE TRIGGER Sync_{table} ON {table} AFTER INSERT,UPDATE,DELETE AS BEGIN SET NOCOUNT ON; {guard}{flags}UPDATE Documents SET UpdatedAt=SYSDATETIMEOFFSET() WHERE Id IN ({Select("inserted")} UNION {Select("deleted")}); END;";
                yield return "EXEC(N'" + body.Replace("'", "''") + "');";
            }
            else foreach (var action in new[] { "INSERT", "UPDATE", "DELETE" })
            {
                string row = action == "DELETE" ? "OLD" : "NEW";
                string Select(string source) => table switch
                {
                    "Projects" => $"SELECT Id FROM Documents WHERE ProjectId={source}.Id",
                    "ProjectNodes" or "DocumentSynopses" => $"SELECT {source}.DocumentId",
                    _ => $"SELECT DocumentId FROM ProjectNodes WHERE Id={source}.SceneNodeId"
                };
                string guard = table == "Projects" && action == "UPDATE" ? " WHEN NEW.Title IS NOT OLD.Title OR NEW.Subtitle IS NOT OLD.Subtitle OR NEW.AuthorName IS NOT OLD.AuthorName OR NEW.Language IS NOT OLD.Language OR NEW.Genre IS NOT OLD.Genre OR NEW.DefaultExportSettingsJson IS NOT OLD.DefaultExportSettingsJson OR NEW.CoverImageUrl IS NOT OLD.CoverImageUrl OR NEW.PrimaryDocumentId IS NOT OLD.PrimaryDocumentId OR NEW.MetadataRevision IS NOT OLD.MetadataRevision" : "";
                string ids = Select(row) + (action == "UPDATE" ? " UNION " + Select("OLD") : "");
                string flags = table == "ProjectNodes" && action != "DELETE" ? $"UPDATE Projects SET SyncEnabled=1 WHERE SyncEnabled=0 AND Id={row}.ProjectId; "
                    : table is "SceneAnnotations" or "DocumentSynopses" && action != "DELETE" ? $"UPDATE Projects SET PlanningSyncEnabled=1 WHERE PlanningSyncEnabled=0 AND Id IN (SELECT ProjectId FROM Documents WHERE Id IN ({ids})); " : "";
                yield return $"CREATE TRIGGER Sync_{table}_{action} AFTER {action} ON {table}{guard} BEGIN {flags}UPDATE Documents SET UpdatedAt=strftime('%Y-%m-%d %H:%M:%f+00:00','now') WHERE Id IN ({ids}); END;";
            }
        }
    }
}
