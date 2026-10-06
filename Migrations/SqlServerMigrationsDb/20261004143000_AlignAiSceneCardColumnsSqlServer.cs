using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorApp.Migrations.SqlServerMigrationsDb;

public partial class AlignAiSceneCardColumnsSqlServer : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // The EF 10 SQLite upgrade added these supported narrative fields. SQL Server's
        // snapshot already advertised them, but no corresponding schema operation existed.
        // Keep any existing/manual column repair and authored field values intact.
        foreach(string table in new[]{"SceneCards","SectionSceneCards"})
        foreach(var (column,type) in new[]{("Summary","nvarchar(max)"),("Status","nvarchar(16)"),("SubplotTagsJson","nvarchar(max)")})
            migrationBuilder.Sql($"IF COL_LENGTH(N'{table}', N'{column}') IS NULL ALTER TABLE [{table}] ADD [{column}] {type} NULL;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // These authored fields are part of the preexisting current model. Removing them
        // on rollback would destroy data and recreate the previously broken schema.
        throw new System.NotSupportedException("This schema alignment preserves authored scene fields. Restore a verified database backup to roll back.");
    }
}
