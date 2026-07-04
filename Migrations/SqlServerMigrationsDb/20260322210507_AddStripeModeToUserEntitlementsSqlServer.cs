using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorApp.Migrations.SqlServerMigrationsDb
{
    /// <inheritdoc />
    public partial class AddStripeModeToUserEntitlementsSqlServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "StripeCustomerId",
                table: "UserEntitlements",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "StripeSubscriptionId",
                table: "UserEntitlements",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripeMode",
                table: "UserEntitlements",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripeMode",
                table: "StripeEventLogs",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "legacy");

            migrationBuilder.CreateIndex(
                name: "IX_UserEntitlements_StripeMode_StripeCustomerId",
                table: "UserEntitlements",
                columns: new[] { "StripeMode", "StripeCustomerId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserEntitlements_StripeMode_StripeSubscriptionId",
                table: "UserEntitlements",
                columns: new[] { "StripeMode", "StripeSubscriptionId" });

            migrationBuilder.CreateIndex(
                name: "IX_StripeEventLogs_StripeMode_ReceivedUtc",
                table: "StripeEventLogs",
                columns: new[] { "StripeMode", "ReceivedUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserEntitlements_StripeMode_StripeCustomerId",
                table: "UserEntitlements");

            migrationBuilder.DropIndex(
                name: "IX_UserEntitlements_StripeMode_StripeSubscriptionId",
                table: "UserEntitlements");

            migrationBuilder.DropIndex(
                name: "IX_StripeEventLogs_StripeMode_ReceivedUtc",
                table: "StripeEventLogs");

            migrationBuilder.DropColumn(
                name: "StripeMode",
                table: "UserEntitlements");

            migrationBuilder.DropColumn(
                name: "StripeMode",
                table: "StripeEventLogs");

            migrationBuilder.AlterColumn<string>(
                name: "StripeCustomerId",
                table: "UserEntitlements",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "StripeSubscriptionId",
                table: "UserEntitlements",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450,
                oldNullable: true);
        }
    }
}
