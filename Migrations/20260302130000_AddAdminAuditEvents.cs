using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WriterApp.Data;

#nullable disable

namespace BlazorApp.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260302130000_AddAdminAuditEvents")]
    public partial class AddAdminAuditEvents : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE TABLE IF NOT EXISTS "AdminAuditEvents" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_AdminAuditEvents" PRIMARY KEY AUTOINCREMENT,
                    "OccurredAtUtc" TEXT NOT NULL,
                    "AdminUserId" TEXT NULL,
                    "AdminEmail" TEXT NULL,
                    "Action" TEXT NOT NULL,
                    "TargetUserId" TEXT NULL,
                    "TargetEmail" TEXT NULL,
                    "DetailsJson" TEXT NULL
                );

                CREATE INDEX IF NOT EXISTS "IX_AdminAuditEvents_OccurredAtUtc" ON "AdminAuditEvents" ("OccurredAtUtc");
                CREATE INDEX IF NOT EXISTS "IX_AdminAuditEvents_TargetUserId" ON "AdminAuditEvents" ("TargetUserId");
                CREATE INDEX IF NOT EXISTS "IX_AdminAuditEvents_AdminUserId" ON "AdminAuditEvents" ("AdminUserId");
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP TABLE IF EXISTS "AdminAuditEvents";""");
        }
    }
}
