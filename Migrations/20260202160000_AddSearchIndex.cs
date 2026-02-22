using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorApp.Migrations
{
    public sealed class AddSearchIndex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS SearchIndexEntries (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    EntityType TEXT NOT NULL,
    EntityId TEXT NOT NULL,
    DocumentId TEXT NOT NULL,
    SectionId TEXT NULL,
    PageId TEXT NULL,
    Title TEXT NOT NULL DEFAULT '',
    Content TEXT NOT NULL DEFAULT '',
    UpdatedAt TEXT NOT NULL
);
");

            migrationBuilder.Sql(@"
CREATE UNIQUE INDEX IF NOT EXISTS IX_SearchIndexEntries_Entity
ON SearchIndexEntries (EntityType, EntityId);
");

            migrationBuilder.Sql(@"
CREATE INDEX IF NOT EXISTS IX_SearchIndexEntries_Document
ON SearchIndexEntries (DocumentId);
");

            migrationBuilder.Sql(@"
CREATE VIRTUAL TABLE IF NOT EXISTS SearchIndexFts
USING fts5(
    Title,
    Content,
    EntityType UNINDEXED,
    EntityId UNINDEXED,
    DocumentId UNINDEXED,
    SectionId UNINDEXED,
    PageId UNINDEXED,
    content='SearchIndexEntries',
    content_rowid='Id'
);
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS SearchIndexFts;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS IX_SearchIndexEntries_Document;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS IX_SearchIndexEntries_Entity;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS SearchIndexEntries;");
        }
    }
}
