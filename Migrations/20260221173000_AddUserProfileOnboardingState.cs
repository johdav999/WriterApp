using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WriterApp.Data;

#nullable disable

namespace BlazorApp.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260221173000_AddUserProfileOnboardingState")]
    public partial class AddUserProfileOnboardingState : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasOnboarded",
                table: "UserProfiles",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedUtc",
                table: "UserProfiles",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2026, 2, 21, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.Sql(
                """
                UPDATE "UserProfiles"
                SET "HasOnboarded" = 1,
                    "UpdatedUtc" = '2025-01-01T00:00:00.0000000Z'
                WHERE "UserId" = 'seed-system';
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasOnboarded",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "UpdatedUtc",
                table: "UserProfiles");
        }
    }
}
