using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WriterApp.Data;

#nullable disable

namespace BlazorApp.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260302170000_AddSearchIndexProjectId")]
    public partial class AddSearchIndexProjectId : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
ALTER TABLE "SearchIndexEntries"
ADD COLUMN "ProjectId" TEXT NOT NULL DEFAULT '';
""");

            migrationBuilder.Sql("""
UPDATE "SearchIndexEntries"
SET "ProjectId" = COALESCE((
    SELECT lower(d."ProjectId")
    FROM "Documents" d
    WHERE lower(d."Id") = lower("SearchIndexEntries"."DocumentId")
    LIMIT 1
), '');
""");

            migrationBuilder.Sql("""
CREATE INDEX IF NOT EXISTS "IX_SearchIndexEntries_ProjectId"
ON "SearchIndexEntries" ("ProjectId");
""");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
DROP INDEX IF EXISTS "IX_SearchIndexEntries_ProjectId";
""");
        }
    }
}
