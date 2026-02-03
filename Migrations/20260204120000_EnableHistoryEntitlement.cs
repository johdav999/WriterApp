using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WriterApp.Data;

namespace BlazorApp.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260204120000_EnableHistoryEntitlement")]
    public partial class EnableHistoryEntitlement : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE \"PlanEntitlements\" SET \"Value\" = 'true' WHERE \"Key\" = 'history.enabled';");

            migrationBuilder.Sql(
                "INSERT INTO \"PlanEntitlements\" (\"PlanId\", \"Key\", \"Value\") " +
                "SELECT p.\"PlanId\", 'history.enabled', 'true' " +
                "FROM \"Plans\" p " +
                "WHERE (lower(p.\"Key\") = 'professional' OR p.\"Name\" = 'Professional') " +
                "AND NOT EXISTS (SELECT 1 FROM \"PlanEntitlements\" e WHERE e.\"PlanId\" = p.\"PlanId\" AND e.\"Key\" = 'history.enabled');");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE \"PlanEntitlements\" SET \"Value\" = 'false' WHERE \"Key\" = 'history.enabled';");
        }
    }
}
