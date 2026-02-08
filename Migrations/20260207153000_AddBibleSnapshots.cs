using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WriterApp.Data;

#nullable disable

namespace BlazorApp.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260207153000_AddBibleSnapshots")]
    public partial class AddBibleSnapshots : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BibleSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DocumentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BibleType = table.Column<string>(type: "TEXT", nullable: false),
                    SchemaVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    ContentJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastRefreshUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LastRefreshSourceHash = table.Column<string>(type: "TEXT", nullable: false),
                    LastRefreshStatsJson = table.Column<string>(type: "TEXT", nullable: false),
                    LastRefreshCursorJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BibleSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BibleSnapshots_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BibleSnapshots_DocumentId_BibleType",
                table: "BibleSnapshots",
                columns: new[] { "DocumentId", "BibleType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BibleSnapshots_LastRefreshUtc",
                table: "BibleSnapshots",
                column: "LastRefreshUtc");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BibleSnapshots");
        }
    }
}
