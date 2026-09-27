namespace WriterApp.Data.Documents;

// Frozen migration SQL. Triggers cover EF, bulk statements and cascading web deletions.
public static class DocumentSyncSchemaV1
{
    public static IEnumerable<string> Install(bool sqlServer)
    {
        yield return sqlServer
            ? "INSERT INTO DocumentSyncRecords (DocumentId,OwnerUserId,Sequence,Version,IsDeleted,IsTrashed) SELECT Id,OwnerUserId,ROW_NUMBER() OVER(ORDER BY Id),CONVERT(varchar(36),NEWID()),0,CASE WHEN DeletedAtUtc IS NULL THEN 0 ELSE 1 END FROM Documents; UPDATE DocumentSyncClocks SET Sequence=(SELECT COUNT(*) FROM Documents) WHERE Id=1;"
            : "INSERT INTO DocumentSyncRecords (DocumentId,OwnerUserId,Sequence,Version,IsDeleted,IsTrashed) SELECT Id,OwnerUserId,ROW_NUMBER() OVER(ORDER BY Id),lower(hex(randomblob(16))),0,CASE WHEN DeletedAtUtc IS NULL THEN 0 ELSE 1 END FROM Documents; UPDATE DocumentSyncClocks SET Sequence=(SELECT COUNT(*) FROM Documents) WHERE Id=1;";
        foreach (string table in new[] { "Documents", "Sections", "Pages" })
        {
            // Dynamic DDL also works inside EF's idempotent migration IF blocks.
            if (sqlServer) { yield return "EXEC(N'" + SqlServerTrigger(table).Replace("'", "''") + "');"; continue; }
            foreach (string action in new[] { "INSERT", "UPDATE", "DELETE" })
            {
                string row = action == "DELETE" ? "OLD" : "NEW";
                string id = table == "Documents" ? $"{row}.Id" : $"{row}.DocumentId";
                string select = table == "Documents"
                    ? $"SELECT {id},{row}.OwnerUserId,(SELECT Sequence FROM DocumentSyncClocks WHERE Id=1),lower(hex(randomblob(16))),{(action == "DELETE" ? 1 : 0)},CASE WHEN {row}.DeletedAtUtc IS NULL THEN 0 ELSE 1 END"
                    : $"SELECT Id,OwnerUserId,(SELECT Sequence FROM DocumentSyncClocks WHERE Id=1),lower(hex(randomblob(16))),0,CASE WHEN DeletedAtUtc IS NULL THEN 0 ELSE 1 END FROM Documents WHERE Id={id}";
                yield return $"""
                    CREATE TRIGGER Sync_{table}_{action} AFTER {action} ON {table} BEGIN
                      UPDATE DocumentSyncClocks SET Sequence=Sequence+1 WHERE Id=1;
                      INSERT INTO DocumentSyncRecords (DocumentId,OwnerUserId,Sequence,Version,IsDeleted,IsTrashed)
                      {select}
                      ON CONFLICT(DocumentId) DO UPDATE SET OwnerUserId=excluded.OwnerUserId,Sequence=excluded.Sequence,Version=excluded.Version,IsDeleted=excluded.IsDeleted,IsTrashed=excluded.IsTrashed;
                    END;
                    """;
            }
        }
    }

    private static string SqlServerTrigger(string table)
    {
        string changes = table == "Documents"
            ? "SELECT COALESCE(i.Id,d.Id) Id,COALESCE(i.OwnerUserId,d.OwnerUserId) OwnerUserId,CASE WHEN i.Id IS NULL THEN 1 ELSE 0 END IsDeleted,CASE WHEN (CASE WHEN i.Id IS NULL THEN d.DeletedAtUtc ELSE i.DeletedAtUtc END) IS NULL THEN 0 ELSE 1 END IsTrashed FROM inserted i FULL JOIN deleted d ON i.Id=d.Id"
            : "SELECT d.Id,d.OwnerUserId,0 IsDeleted,CASE WHEN d.DeletedAtUtc IS NULL THEN 0 ELSE 1 END IsTrashed FROM Documents d JOIN (SELECT DocumentId FROM inserted UNION SELECT DocumentId FROM deleted) c ON c.DocumentId=d.Id";
        return $"""
            CREATE TRIGGER Sync_{table} ON {table} AFTER INSERT,UPDATE,DELETE AS
            BEGIN
              SET NOCOUNT ON;
              DECLARE @changes TABLE (Id uniqueidentifier PRIMARY KEY,OwnerUserId nvarchar(128),IsDeleted bit,IsTrashed bit,Position bigint);
              INSERT INTO @changes SELECT Id,OwnerUserId,IsDeleted,IsTrashed,ROW_NUMBER() OVER(ORDER BY Id) FROM ({changes}) c;
              DECLARE @count bigint=(SELECT COUNT(*) FROM @changes);
              UPDATE DocumentSyncClocks SET Sequence=Sequence+@count WHERE Id=1;
              DECLARE @base bigint=(SELECT Sequence-@count FROM DocumentSyncClocks WHERE Id=1);
              UPDATE r SET OwnerUserId=c.OwnerUserId,Sequence=@base+c.Position,Version=CONVERT(varchar(36),NEWID()),IsDeleted=c.IsDeleted,IsTrashed=c.IsTrashed FROM DocumentSyncRecords r JOIN @changes c ON r.DocumentId=c.Id;
              INSERT INTO DocumentSyncRecords (DocumentId,OwnerUserId,Sequence,Version,IsDeleted,IsTrashed)
              SELECT Id,OwnerUserId,@base+Position,CONVERT(varchar(36),NEWID()),IsDeleted,IsTrashed FROM @changes c WHERE NOT EXISTS (SELECT 1 FROM DocumentSyncRecords r WHERE r.DocumentId=c.Id);
            END;
            """;
    }

    public static IEnumerable<string> Uninstall(bool sqlServer) =>
        new[] { "Documents", "Sections", "Pages" }.SelectMany(table => sqlServer
            ? new[] { $"DROP TRIGGER Sync_{table};" }
            : new[] { "INSERT", "UPDATE", "DELETE" }.Select(action => $"DROP TRIGGER Sync_{table}_{action};"));
}
