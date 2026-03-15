using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WriterApp.Data;

#nullable disable

namespace BlazorApp.Migrations.SqlServerMigrationsDb
{
    [DbContext(typeof(SqlServerMigrationsDbContext))]
    [Migration("20260314152500_AddAdminRoleAssignmentsSqlServer")]
    public partial class AddAdminRoleAssignmentsSqlServer : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdminRoleAssignments",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    AssignedByUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    AssignedByEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    AssignedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminRoleAssignments", x => x.UserId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminRoleAssignments_AssignedUtc",
                table: "AdminRoleAssignments",
                column: "AssignedUtc");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminRoleAssignments");
        }
    }
}
