using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WriterApp.Migrations.SqlServerMigrationsDb
{
    /// <inheritdoc />
    public partial class MultiDocumentProjectsSqlServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (string sql in WriterApp.Data.Documents.MultiDocumentSyncSchemaV4.Uninstall(true)) migrationBuilder.Sql(sql);
            migrationBuilder.Sql("DROP INDEX IF EXISTS IX_Documents_ProjectId_DocumentKind ON Documents;");

            migrationBuilder.AddColumn<long>(
                name: "MetadataRevision",
                table: "Projects",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<Guid>(
                name: "PrimaryDocumentId",
                table: "Projects",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DocumentId",
                table: "ProjectNodes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectNodes_DocumentId_ParentId_OrderIndex",
                table: "ProjectNodes",
                columns: new[] { "DocumentId", "ParentId", "OrderIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_ProjectId_DocumentKind",
                table: "Documents",
                columns: new[] { "ProjectId", "DocumentKind" });

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectNodes_Documents_DocumentId",
                table: "ProjectNodes",
                column: "DocumentId",
                principalTable: "Documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.Sql("UPDATE Projects SET PrimaryDocumentId=(SELECT TOP(1) d.Id FROM Documents d WHERE d.ProjectId=Projects.Id AND d.DocumentKind=0 ORDER BY d.CreatedAtUnixSeconds,d.Id); UPDATE ProjectNodes SET DocumentId=(SELECT p.PrimaryDocumentId FROM Projects p WHERE p.Id=ProjectNodes.ProjectId);");
            foreach (string sql in WriterApp.Data.Documents.MultiDocumentSyncSchemaV4.Install(true)) migrationBuilder.Sql(sql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Multi-document ownership cannot be downgraded safely. Restore a pre-migration backup instead.");
        }

    }
}
