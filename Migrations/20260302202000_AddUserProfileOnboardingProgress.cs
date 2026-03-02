using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WriterApp.Data;

#nullable disable

namespace BlazorApp.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260302202000_AddUserProfileOnboardingProgress")]
    public partial class AddUserProfileOnboardingProgress : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasCompletedOnboarding",
                table: "UserProfiles",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "OnboardingStep",
                table: "UserProfiles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "OnboardingStartedUtc",
                table: "UserProfiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "OnboardingCompletedUtc",
                table: "UserProfiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrimaryWritingIntent",
                table: "UserProfiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "UserProfiles"
                SET "HasCompletedOnboarding" = 1
                WHERE "HasOnboarded" = 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_HasCompletedOnboarding",
                table: "UserProfiles",
                column: "HasCompletedOnboarding");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserProfiles_HasCompletedOnboarding",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "HasCompletedOnboarding",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "OnboardingStep",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "OnboardingStartedUtc",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "OnboardingCompletedUtc",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "PrimaryWritingIntent",
                table: "UserProfiles");
        }
    }
}
