namespace WriterApp.Data.Collaboration;

/// <summary>
/// Database enforcement covers tracked writes, ExecuteUpdate/Delete, and raw SQL alike.
/// There is deliberately no client-controlled projection/migration bypass in this phase.
/// Frozen migration SQL: later write capabilities need a new version and migration.
/// </summary>
public static class CollaborationWriteFenceSchema
{
    public const string MigratingMarker = "collaboration_migration_in_progress";
    public const string BlockedMarker = "collaboration_legacy_write_blocked";

    // Both sides of UPDATE are checked: moving a row cannot escape its original manuscript.
    private static readonly Dictionary<string, string> DocumentIds = new()
    {
        ["Pages"] = "SELECT r.DocumentId FROM {0} r",
        ["Sections"] = "SELECT r.DocumentId FROM {0} r",
        ["DocumentOutlineNodes"] = "SELECT r.DocumentId FROM {0} r",
        ["DocumentOutlines"] = "SELECT r.DocumentId FROM {0} r",
        ["DocumentSynopses"] = "SELECT r.DocumentId FROM {0} r",
        ["Projects"] = "SELECT d.Id FROM Documents d JOIN {0} r ON r.Id=d.ProjectId",
        // Legacy project trees may have a null DocumentId. Fence the shared tree conservatively.
        ["ProjectNodes"] = "SELECT d.Id FROM Documents d JOIN {0} r ON r.ProjectId=d.ProjectId",
        ["SceneContents"] = "SELECT d.Id FROM Documents d JOIN ProjectNodes n ON n.ProjectId=d.ProjectId JOIN {0} r ON r.SceneNodeId=n.Id"
    };

    private static readonly string[] LegacyDocumentColumns = ["Id", "ProjectId", "OwnerUserId", "Title", "DocumentKind",
        "LanguageCode", "TranslationGroupId", "CreatedAt", "UpdatedAt", "CreatedAtUnixSeconds", "UpdatedAtUnixSeconds",
        "IsArchived", "ArchivedAt", "DeletedAtUtc"];

    public static IEnumerable<string> Install(bool sqlServer)
    {
        foreach (var (table, template) in DocumentIds)
        {
            if (sqlServer)
            {
                string ids = string.Format(template, "inserted") + " UNION " + string.Format(template, "deleted");
                string body = $"CREATE TRIGGER CollaborationFence_{table} ON [{table}] AFTER INSERT, UPDATE, DELETE AS BEGIN SET NOCOUNT ON; "
                    // Lock even Legacy document rows. Transitioning mode must serialize with this write.
                    + $"DECLARE @mode int; SELECT @mode=MAX(CONVERT(int,d.CollaborationMode)) FROM Documents d WITH (UPDLOCK,HOLDLOCK) WHERE d.Id IN ({ids}); "
                    + $"IF @mode=1 THROW 51071, '{MigratingMarker}', 1; IF @mode>0 THROW 51070, '{BlockedMarker}', 1; END;";
                yield return Exec(body);
            }
            else foreach (string action in new[] { "INSERT", "UPDATE", "DELETE" })
            {
                // SQLite cannot SELECT OLD.*. Build one-row relation using only required columns.
                string Row(string row) => table switch
                {
                    "Projects" => $"(SELECT {row}.Id AS Id)",
                    "ProjectNodes" => $"(SELECT {row}.ProjectId AS ProjectId)",
                    "SceneContents" => $"(SELECT {row}.SceneNodeId AS SceneNodeId)",
                    _ => $"(SELECT {row}.DocumentId AS DocumentId)"
                };
                string ids = string.Format(template, Row(action == "DELETE" ? "OLD" : "NEW"))
                    + (action == "UPDATE" ? " UNION " + string.Format(template, Row("OLD")) : "");
                yield return $"CREATE TRIGGER CollaborationFence_{table}_{action} BEFORE {action} ON [{table}] BEGIN "
                    + $"SELECT RAISE(ABORT,'{MigratingMarker}') WHERE EXISTS(SELECT 1 FROM Documents WHERE Id IN ({ids}) AND CollaborationMode=1); "
                    + $"SELECT RAISE(ABORT,'{BlockedMarker}') WHERE EXISTS(SELECT 1 FROM Documents WHERE Id IN ({ids}) AND CollaborationMode<>0); END;";
            }
        }

        if (sqlServer)
        {
            string columns = string.Join(",", LegacyDocumentColumns.Select(c => $"i.[{c}]"));
            string oldColumns = string.Join(",", LegacyDocumentColumns.Select(c => $"o.[{c}]"));
            yield return Exec($"CREATE TRIGGER CollaborationFence_Documents ON Documents AFTER UPDATE,DELETE AS BEGIN SET NOCOUNT ON; "
                + "IF EXISTS(SELECT 1 FROM deleted o LEFT JOIN inserted i ON i.Id=o.Id WHERE "
                + $"(o.CollaborationMode<>0 AND (i.Id IS NULL OR i.CollaborationMode<o.CollaborationMode)) OR "
                + $"((o.CollaborationMode<>0 OR i.CollaborationMode<>0) AND EXISTS(SELECT {columns} EXCEPT SELECT {oldColumns}))) "
                + $"THROW 51070, '{BlockedMarker}', 1; END;");
        }
        else
        {
            string changed = string.Join(" OR ", LegacyDocumentColumns.Select(c => $"NEW.[{c}] IS NOT OLD.[{c}]"));
            yield return $"CREATE TRIGGER CollaborationFence_Documents_UPDATE BEFORE UPDATE ON Documents "
                + $"WHEN (OLD.CollaborationMode<>0 AND NEW.CollaborationMode<OLD.CollaborationMode) OR ((OLD.CollaborationMode<>0 OR NEW.CollaborationMode<>0) AND ({changed})) "
                + $"BEGIN SELECT RAISE(ABORT,'{MigratingMarker}') WHERE OLD.CollaborationMode=1; SELECT RAISE(ABORT,'{BlockedMarker}'); END;";
            yield return $"CREATE TRIGGER CollaborationFence_Documents_DELETE BEFORE DELETE ON Documents WHEN OLD.CollaborationMode<>0 "
                + $"BEGIN SELECT RAISE(ABORT,'{MigratingMarker}') WHERE OLD.CollaborationMode=1; SELECT RAISE(ABORT,'{BlockedMarker}'); END;";
        }
    }

    public static IEnumerable<string> Uninstall(bool sqlServer)
    {
        foreach (string table in DocumentIds.Keys.Append("Documents"))
            foreach (string suffix in sqlServer ? new[] { "" } : table == "Documents" ? new[] { "_UPDATE", "_DELETE" } : new[] { "_INSERT", "_UPDATE", "_DELETE" })
                yield return $"DROP TRIGGER [CollaborationFence_{table}{suffix}];";
    }

    private static string Exec(string sql) => "EXEC(N'" + sql.Replace("'", "''") + "');";
}
