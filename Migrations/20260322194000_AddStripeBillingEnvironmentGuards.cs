using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using WriterApp.Data;

#nullable disable

namespace BlazorApp.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260322194000_AddStripeBillingEnvironmentGuards")]
    public partial class AddStripeBillingEnvironmentGuards : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StripeMode",
                table: "UserEntitlements",
                type: "TEXT",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripeMode",
                table: "StripeEventLogs",
                type: "TEXT",
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
        }
    }
}
