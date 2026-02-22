using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WriterApp.Data;

#nullable disable

namespace BlazorApp.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260208163000_AddDocumentProjectRelationship")]
    public partial class AddDocumentProjectRelationship : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                -- Self-heal for SQLite schema/history mismatches:
                -- Some environments can report prior migrations as applied while Projects is missing.
                -- Ensure Projects exists before INSERT INTO Projects below.
                CREATE TABLE IF NOT EXISTS "Projects" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_Projects" PRIMARY KEY,
                    "OwnerUserId" TEXT NOT NULL,
                    "Title" TEXT NOT NULL,
                    "Subtitle" TEXT NULL,
                    "AuthorName" TEXT NULL,
                    "Language" TEXT NULL,
                    "Genre" TEXT NULL,
                    "DefaultExportSettingsJson" TEXT NULL,
                    "CreatedUtc" TEXT NOT NULL,
                    "UpdatedUtc" TEXT NOT NULL
                );
                """);

            migrationBuilder.AddColumn<int>(
                name: "DocumentKind",
                table: "Documents",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "Documents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.Sql(
                """
                CREATE TEMP TABLE IF NOT EXISTS __DocumentProjectMap (
                    DocumentId TEXT NOT NULL PRIMARY KEY,
                    ProjectId TEXT NOT NULL
                );
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO __DocumentProjectMap (DocumentId, ProjectId)
                SELECT
                    d.Id,
                    lower(hex(randomblob(4))) || '-' ||
                    lower(hex(randomblob(2))) || '-' ||
                    lower(hex(randomblob(2))) || '-' ||
                    lower(hex(randomblob(2))) || '-' ||
                    lower(hex(randomblob(6)))
                FROM Documents d
                WHERE d.ProjectId IS NULL;
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO Projects (
                    Id,
                    OwnerUserId,
                    Title,
                    Subtitle,
                    AuthorName,
                    Language,
                    Genre,
                    DefaultExportSettingsJson,
                    CreatedUtc,
                    UpdatedUtc
                )
                SELECT
                    map.ProjectId,
                    d.OwnerUserId,
                    CASE
                        WHEN trim(ifnull(d.Title, '')) = '' THEN 'Imported Project'
                        ELSE d.Title || ' Project'
                    END,
                    NULL,
                    NULL,
                    d.LanguageCode,
                    NULL,
                    NULL,
                    d.CreatedAt,
                    d.UpdatedAt
                FROM __DocumentProjectMap map
                INNER JOIN Documents d ON d.Id = map.DocumentId;
                """);

            migrationBuilder.Sql(
                """
                UPDATE Documents
                SET ProjectId = (
                    SELECT map.ProjectId
                    FROM __DocumentProjectMap map
                    WHERE map.DocumentId = Documents.Id
                )
                WHERE ProjectId IS NULL;
                """);

            migrationBuilder.Sql("DROP TABLE IF EXISTS __DocumentProjectMap;");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_ProjectId",
                table: "Documents",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_DocumentKind",
                table: "Documents",
                column: "DocumentKind");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_ProjectId_Manuscript",
                table: "Documents",
                columns: new[] { "ProjectId", "DocumentKind" },
                unique: true,
                filter: "\"DocumentKind\" = 0");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Documents_ProjectId_Manuscript",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Documents_DocumentKind",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Documents_ProjectId",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "DocumentKind",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "Documents");
        }
    }
}
