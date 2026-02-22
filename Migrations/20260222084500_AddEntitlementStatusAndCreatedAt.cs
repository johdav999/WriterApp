using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WriterApp.Data;

#nullable disable

namespace BlazorApp.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260222084500_AddEntitlementStatusAndCreatedAt")]
    public partial class AddEntitlementStatusAndCreatedAt : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "UserEntitlements",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(2026, 2, 22, 0, 0, 0, DateTimeKind.Utc), TimeSpan.Zero));

            migrationBuilder.AddColumn<string>(
                name: "SubscriptionStatus",
                table: "UserEntitlements",
                type: "TEXT",
                nullable: false,
                defaultValue: "Active");

            migrationBuilder.Sql(
                """
                UPDATE "UserEntitlements"
                SET "CreatedAt" = COALESCE(NULLIF("CreatedAt", '0001-01-01 00:00:00+00:00'), "PeriodStartUtc"),
                    "SubscriptionStatus" = CASE
                        WHEN "SubscriptionStatus" IS NULL OR TRIM("SubscriptionStatus") = '' THEN 'Active'
                        ELSE "SubscriptionStatus"
                    END;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "UserEntitlements");

            migrationBuilder.DropColumn(
                name: "SubscriptionStatus",
                table: "UserEntitlements");
        }
    }
}
