using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using WriterApp.Data;

#nullable disable

namespace BlazorApp.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260308162000_AddUserProfileEmail")]
    public partial class AddUserProfileEmail : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "UserProfiles",
                type: "TEXT",
                maxLength: 320,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "UserProfiles"
                SET "Email" = CASE
                    WHEN instr("DisplayName", '@') > 0 THEN "DisplayName"
                    WHEN instr("UserId", '@') > 0 THEN "UserId"
                    ELSE NULL
                END
                WHERE "Email" IS NULL;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Email",
                table: "UserProfiles");
        }
    }
}
