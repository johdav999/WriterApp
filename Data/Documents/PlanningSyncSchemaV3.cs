namespace WriterApp.Data.Documents;

public static class PlanningSyncSchemaV3
{
    public static IEnumerable<string> Install(bool sqlServer)
    {
        yield return "UPDATE Projects SET PlanningSyncEnabled=1 WHERE Id IN (SELECT ProjectId FROM Documents WHERE Id IN (SELECT DocumentId FROM DocumentSynopses)) OR Id IN (SELECT ProjectId FROM ProjectNodes WHERE Id IN (SELECT SceneNodeId FROM SceneAnnotations));";
        foreach (string table in new[] { "SceneAnnotations", "DocumentSynopses" })
        {
            if (sqlServer)
            {
                string Select(string source) => table == "DocumentSynopses" ? $"SELECT DocumentId FROM {source}"
                    : $"SELECT d.Id FROM Documents d JOIN ProjectNodes n ON n.ProjectId=d.ProjectId JOIN {source} a ON a.SceneNodeId=n.Id";
                string ids = Select("inserted") + " UNION " + Select("deleted");
                string body = $"CREATE TRIGGER Sync_{table} ON {table} AFTER INSERT,UPDATE,DELETE AS BEGIN SET NOCOUNT ON; UPDATE Projects SET PlanningSyncEnabled=1 WHERE PlanningSyncEnabled=0 AND Id IN (SELECT ProjectId FROM Documents WHERE Id IN ({ids})); UPDATE Documents SET UpdatedAt=SYSDATETIMEOFFSET() WHERE Id IN ({ids}); END;";
                yield return "EXEC(N'" + body.Replace("'", "''") + "');";
            }
            else foreach (string action in new[] { "INSERT", "UPDATE", "DELETE" })
            {
                string row = action == "DELETE" ? "OLD" : "NEW";
                string Select(string r) => table == "DocumentSynopses" ? $"SELECT {r}.DocumentId"
                    : $"SELECT d.Id FROM Documents d JOIN ProjectNodes n ON n.ProjectId=d.ProjectId WHERE n.Id={r}.SceneNodeId";
                string ids = Select(row) + (action == "UPDATE" ? " UNION " + Select("OLD") : "");
                yield return $"CREATE TRIGGER Sync_{table}_{action} AFTER {action} ON {table} BEGIN UPDATE Projects SET PlanningSyncEnabled=1 WHERE PlanningSyncEnabled=0 AND Id IN (SELECT ProjectId FROM Documents WHERE Id IN ({ids})); UPDATE Documents SET UpdatedAt=strftime('%Y-%m-%d %H:%M:%f+00:00','now') WHERE Id IN ({ids}); END;";
            }
        }
    }
    public static IEnumerable<string> Uninstall(bool sqlServer) => new[] { "SceneAnnotations", "DocumentSynopses" }
        .SelectMany(t => sqlServer ? new[] { $"DROP TRIGGER Sync_{t};" } : new[] { "INSERT", "UPDATE", "DELETE" }.Select(a => $"DROP TRIGGER Sync_{t}_{a};"));
}
