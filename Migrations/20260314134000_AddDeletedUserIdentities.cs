using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using WriterApp.Data;

#nullable disable

namespace BlazorApp.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260314134000_AddDeletedUserIdentities")]
    public partial class AddDeletedUserIdentities : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeletedUserIdentities",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 320, nullable: true),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    DeletedByAdminUserId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    DeletedByAdminEmail = table.Column<string>(type: "TEXT", maxLength: 320, nullable: true),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    DeletedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeletedUserIdentities", x => x.UserId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeletedUserIdentities_DeletedAtUtc",
                table: "DeletedUserIdentities",
                column: "DeletedAtUtc");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeletedUserIdentities");
        }
    }
}
