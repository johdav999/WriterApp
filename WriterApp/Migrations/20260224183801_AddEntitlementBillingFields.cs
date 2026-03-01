using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorApp.Migrations
{
    /// <inheritdoc />
    public partial class AddEntitlementBillingFields : Migration
    {
        /// <inheritdoc />
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
