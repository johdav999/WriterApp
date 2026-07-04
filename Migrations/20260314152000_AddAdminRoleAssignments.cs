using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using WriterApp.Data;

#nullable disable

namespace BlazorApp.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260314152000_AddAdminRoleAssignments")]
    public partial class AddAdminRoleAssignments : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdminRoleAssignments",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    AssignedByUserId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    AssignedByEmail = table.Column<string>(type: "TEXT", maxLength: 320, nullable: true),
                    AssignedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
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
