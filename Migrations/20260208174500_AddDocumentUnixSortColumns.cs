using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WriterApp.Data;

#nullable disable

namespace BlazorApp.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260208174500_AddDocumentUnixSortColumns")]
    public partial class AddDocumentUnixSortColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CreatedAtUnixSeconds",
                table: "Documents",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "UpdatedAtUnixSeconds",
                table: "Documents",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.Sql(
                """
                UPDATE Documents
                SET CreatedAtUnixSeconds = COALESCE(CAST(strftime('%s', CreatedAt) AS INTEGER), 0),
                    UpdatedAtUnixSeconds = COALESCE(CAST(strftime('%s', UpdatedAt) AS INTEGER), 0)
                WHERE CreatedAtUnixSeconds = 0 OR UpdatedAtUnixSeconds = 0;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Documents_ProjectId_UpdatedAtUnixSeconds",
                table: "Documents",
                columns: new[] { "ProjectId", "UpdatedAtUnixSeconds" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Documents_ProjectId_UpdatedAtUnixSeconds",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "CreatedAtUnixSeconds",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUnixSeconds",
                table: "Documents");
        }
    }
}
