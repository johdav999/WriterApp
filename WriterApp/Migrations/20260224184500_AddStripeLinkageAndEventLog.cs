using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorApp.Migrations
{
    public partial class AddStripeLinkageAndEventLog : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CancelAtPeriodEnd",
                table: "UserEntitlements",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CurrentPeriodEndUtc",
                table: "UserEntitlements",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripeCustomerId",
                table: "UserEntitlements",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripePriceId",
                table: "UserEntitlements",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripeSubscriptionId",
                table: "UserEntitlements",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StripeEventLogs",
                columns: table => new
                {
                    StripeEventId = table.Column<string>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", nullable: false),
                    ReceivedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ProcessedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    UserId = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    Error = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StripeEventLogs", x => x.StripeEventId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StripeEventLogs_ReceivedUtc",
                table: "StripeEventLogs",
                column: "ReceivedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_StripeEventLogs_Status",
                table: "StripeEventLogs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_StripeEventLogs_UserId",
                table: "StripeEventLogs",
                column: "UserId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StripeEventLogs");

            migrationBuilder.DropColumn(
                name: "CancelAtPeriodEnd",
                table: "UserEntitlements");

            migrationBuilder.DropColumn(
                name: "CurrentPeriodEndUtc",
                table: "UserEntitlements");

            migrationBuilder.DropColumn(
                name: "StripeCustomerId",
                table: "UserEntitlements");

            migrationBuilder.DropColumn(
                name: "StripePriceId",
                table: "UserEntitlements");

            migrationBuilder.DropColumn(
                name: "StripeSubscriptionId",
                table: "UserEntitlements");
        }
    }
}
