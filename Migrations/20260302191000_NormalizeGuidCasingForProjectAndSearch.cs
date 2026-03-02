using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WriterApp.Data;

#nullable disable

namespace BlazorApp.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260302191000_NormalizeGuidCasingForProjectAndSearch")]
    public partial class NormalizeGuidCasingForProjectAndSearch : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                PRAGMA foreign_keys=OFF;

                UPDATE "Projects"
                SET "Id" = lower("Id")
                WHERE EXISTS (SELECT 1 FROM pragma_table_info('Projects') WHERE name = 'Id')
                  AND "Id" <> lower("Id");

                UPDATE "Documents"
                SET "Id" = lower("Id")
                WHERE EXISTS (SELECT 1 FROM pragma_table_info('Documents') WHERE name = 'Id')
                  AND "Id" <> lower("Id");

                UPDATE "Documents"
                SET "ProjectId" = lower("ProjectId")
                WHERE EXISTS (SELECT 1 FROM pragma_table_info('Documents') WHERE name = 'ProjectId')
                  AND "ProjectId" <> lower("ProjectId");

                UPDATE "Sections"
                SET "Id" = lower("Id")
                WHERE EXISTS (SELECT 1 FROM pragma_table_info('Sections') WHERE name = 'Id')
                  AND "Id" <> lower("Id");

                UPDATE "Sections"
                SET "DocumentId" = lower("DocumentId")
                WHERE EXISTS (SELECT 1 FROM pragma_table_info('Sections') WHERE name = 'DocumentId')
                  AND "DocumentId" <> lower("DocumentId");

                UPDATE "Pages"
                SET "Id" = lower("Id")
                WHERE EXISTS (SELECT 1 FROM pragma_table_info('Pages') WHERE name = 'Id')
                  AND "Id" <> lower("Id");

                UPDATE "Pages"
                SET "DocumentId" = lower("DocumentId")
                WHERE EXISTS (SELECT 1 FROM pragma_table_info('Pages') WHERE name = 'DocumentId')
                  AND "DocumentId" <> lower("DocumentId");

                UPDATE "Pages"
                SET "SectionId" = lower("SectionId")
                WHERE EXISTS (SELECT 1 FROM pragma_table_info('Pages') WHERE name = 'SectionId')
                  AND "SectionId" <> lower("SectionId");

                UPDATE "PageVersions"
                SET "Id" = lower("Id")
                WHERE EXISTS (SELECT 1 FROM pragma_table_info('PageVersions') WHERE name = 'Id')
                  AND "Id" <> lower("Id");

                UPDATE "PageVersions"
                SET "PageId" = lower("PageId")
                WHERE EXISTS (SELECT 1 FROM pragma_table_info('PageVersions') WHERE name = 'PageId')
                  AND "PageId" <> lower("PageId");

                UPDATE "PageVersions"
                SET "DocumentId" = lower("DocumentId")
                WHERE EXISTS (SELECT 1 FROM pragma_table_info('PageVersions') WHERE name = 'DocumentId')
                  AND "DocumentId" <> lower("DocumentId");

                UPDATE "SearchIndexEntries"
                SET "EntityId" = lower("EntityId")
                WHERE EXISTS (SELECT 1 FROM pragma_table_info('SearchIndexEntries') WHERE name = 'EntityId')
                  AND "EntityId" <> lower("EntityId");

                UPDATE "SearchIndexEntries"
                SET "DocumentId" = lower("DocumentId")
                WHERE EXISTS (SELECT 1 FROM pragma_table_info('SearchIndexEntries') WHERE name = 'DocumentId')
                  AND "DocumentId" <> lower("DocumentId");

                UPDATE "SearchIndexEntries"
                SET "PageId" = lower("PageId")
                WHERE EXISTS (SELECT 1 FROM pragma_table_info('SearchIndexEntries') WHERE name = 'PageId')
                  AND "PageId" IS NOT NULL
                  AND "PageId" <> lower("PageId");

                UPDATE "SearchIndexEntries"
                SET "SectionId" = lower("SectionId")
                WHERE EXISTS (SELECT 1 FROM pragma_table_info('SearchIndexEntries') WHERE name = 'SectionId')
                  AND "SectionId" IS NOT NULL
                  AND "SectionId" <> lower("SectionId");

                UPDATE "SearchIndexEntries"
                SET "ProjectId" = lower("ProjectId")
                WHERE EXISTS (SELECT 1 FROM pragma_table_info('SearchIndexEntries') WHERE name = 'ProjectId')
                  AND "ProjectId" <> lower("ProjectId");

                PRAGMA foreign_keys=ON;
                """,
                suppressTransaction: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op: normalization to lowercase is irreversible.
        }
    }
}
