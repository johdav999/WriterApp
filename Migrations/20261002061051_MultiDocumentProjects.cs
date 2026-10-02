using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WriterApp.Migrations
{
    /// <inheritdoc />
    public partial class MultiDocumentProjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (string sql in WriterApp.Data.Documents.MultiDocumentSyncSchemaV4.Uninstall(false)) migrationBuilder.Sql(sql);
            migrationBuilder.Sql("DROP INDEX IF EXISTS IX_Documents_ProjectId_DocumentKind;");

            migrationBuilder.AddColumn<long>(
                name: "MetadataRevision",
                table: "Projects",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<Guid>(
                name: "PrimaryDocumentId",
                table: "Projects",
                type: "TEXT",
                nullable: true);

            // Adding a nullable reference directly avoids rebuilding ProjectNodes while
            // existing scene/planning triggers refer to that table.
            migrationBuilder.Sql("ALTER TABLE ProjectNodes ADD COLUMN DocumentId TEXT REFERENCES Documents(Id);");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectNodes_DocumentId_ParentId_OrderIndex",
                table: "ProjectNodes",
                columns: new[] { "DocumentId", "ParentId", "OrderIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_ProjectId_DocumentKind",
                table: "Documents",
                columns: new[] { "ProjectId", "DocumentKind" });

            migrationBuilder.Sql("UPDATE Projects SET PrimaryDocumentId=(SELECT d.Id FROM Documents d WHERE d.ProjectId=Projects.Id AND d.DocumentKind=0 ORDER BY d.CreatedAtUnixSeconds,d.Id LIMIT 1); UPDATE ProjectNodes SET DocumentId=(SELECT p.PrimaryDocumentId FROM Projects p WHERE p.Id=ProjectNodes.ProjectId);");
            foreach (string sql in WriterApp.Data.Documents.MultiDocumentSyncSchemaV4.Install(false)) migrationBuilder.Sql(sql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Multi-document ownership cannot be downgraded safely. Restore a pre-migration backup instead.");
        }

    }
}
