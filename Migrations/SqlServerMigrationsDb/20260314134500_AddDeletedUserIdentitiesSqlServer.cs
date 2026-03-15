using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WriterApp.Data;

#nullable disable

namespace BlazorApp.Migrations.SqlServerMigrationsDb
{
    [DbContext(typeof(SqlServerMigrationsDbContext))]
    [Migration("20260314134500_AddDeletedUserIdentitiesSqlServer")]
    public partial class AddDeletedUserIdentitiesSqlServer : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeletedUserIdentities",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    DisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    DeletedByAdminUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    DeletedByAdminEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
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
