using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WriterApp.Data;

#nullable disable

namespace BlazorApp.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260301224500_SelfHealTextGuidCasing")]
    public partial class SelfHealTextGuidCasing : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("PRAGMA foreign_keys=OFF;", suppressTransaction: true);

            migrationBuilder.Sql("""
UPDATE "Projects" SET "Id" = lower("Id") WHERE "Id" <> lower("Id");
UPDATE "Documents" SET "Id" = lower("Id") WHERE "Id" <> lower("Id");
UPDATE "Documents" SET "ProjectId" = lower("ProjectId") WHERE "ProjectId" <> lower("ProjectId");
UPDATE "Sections" SET "Id" = lower("Id") WHERE "Id" <> lower("Id");
UPDATE "Sections" SET "DocumentId" = lower("DocumentId") WHERE "DocumentId" <> lower("DocumentId");
UPDATE "Pages" SET "Id" = lower("Id") WHERE "Id" <> lower("Id");
UPDATE "Pages" SET "DocumentId" = lower("DocumentId") WHERE "DocumentId" <> lower("DocumentId");
UPDATE "Pages" SET "SectionId" = lower("SectionId") WHERE "SectionId" <> lower("SectionId");

UPDATE "ProjectNodes" SET "Id" = lower("Id") WHERE "Id" <> lower("Id");
UPDATE "ProjectNodes" SET "ProjectId" = lower("ProjectId") WHERE "ProjectId" <> lower("ProjectId");
UPDATE "ProjectNodes" SET "ParentId" = lower("ParentId") WHERE "ParentId" IS NOT NULL AND "ParentId" <> lower("ParentId");
UPDATE "ProjectNodes" SET "LinkedSectionId" = lower("LinkedSectionId") WHERE "LinkedSectionId" IS NOT NULL AND "LinkedSectionId" <> lower("LinkedSectionId");

UPDATE "SceneContents" SET "SceneNodeId" = lower("SceneNodeId") WHERE "SceneNodeId" <> lower("SceneNodeId");
UPDATE "SearchIndexEntries" SET "EntityId" = lower("EntityId") WHERE "EntityId" <> lower("EntityId");
UPDATE "SearchIndexEntries" SET "DocumentId" = lower("DocumentId") WHERE "DocumentId" <> lower("DocumentId");
UPDATE "SearchIndexEntries" SET "SectionId" = lower("SectionId") WHERE "SectionId" IS NOT NULL AND "SectionId" <> lower("SectionId");
UPDATE "SearchIndexEntries" SET "PageId" = lower("PageId") WHERE "PageId" IS NOT NULL AND "PageId" <> lower("PageId");
UPDATE "DocumentOutlineNodes" SET "Id" = lower("Id") WHERE "Id" <> lower("Id");
UPDATE "DocumentOutlineNodes" SET "DocumentId" = lower("DocumentId") WHERE "DocumentId" <> lower("DocumentId");
UPDATE "DocumentOutlineNodes" SET "ParentId" = lower("ParentId") WHERE "ParentId" IS NOT NULL AND "ParentId" <> lower("ParentId");
UPDATE "DocumentOutlineNodes" SET "LinkedSectionId" = lower("LinkedSectionId") WHERE "LinkedSectionId" IS NOT NULL AND "LinkedSectionId" <> lower("LinkedSectionId");
""", suppressTransaction: true);

            migrationBuilder.Sql("PRAGMA foreign_keys=ON;", suppressTransaction: true);

            migrationBuilder.Sql("""
CREATE TEMP TABLE __fk_guard(dummy INTEGER);

CREATE TEMP TRIGGER __fk_guard_abort
BEFORE INSERT ON __fk_guard
WHEN EXISTS (SELECT 1 FROM pragma_foreign_key_check)
BEGIN
    SELECT RAISE(ABORT, 'FK violations after SelfHealTextGuidCasing');
END;

INSERT INTO __fk_guard(dummy) VALUES (1);

DROP TRIGGER __fk_guard_abort;
DROP TABLE __fk_guard;
""", suppressTransaction: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Irreversible by design: lowercasing persisted textual IDs cannot be restored.
        }
    }
}
