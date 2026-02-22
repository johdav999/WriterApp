using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WriterApp.Data;

#nullable disable

namespace BlazorApp.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260202190000_AddPageVersions")]
    public partial class AddPageVersions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PageVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PageId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DocumentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", nullable: false),
                    ContentCompressed = table.Column<byte[]>(type: "BLOB", nullable: false),
                    ContentTextHash = table.Column<string>(type: "TEXT", nullable: false),
                    SizeBytes = table.Column<int>(type: "INTEGER", nullable: false),
                    WordCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PageVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PageVersions_Pages_PageId",
                        column: x => x.PageId,
                        principalTable: "Pages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PageVersions_PageId",
                table: "PageVersions",
                column: "PageId");

            migrationBuilder.CreateIndex(
                name: "IX_PageVersions_DocumentId",
                table: "PageVersions",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_PageVersions_CreatedAt",
                table: "PageVersions",
                column: "CreatedAt");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PageVersions");
        }
    }
}
