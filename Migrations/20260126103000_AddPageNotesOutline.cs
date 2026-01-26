using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorApp.Migrations
{
    public partial class AddPageNotesOutline : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentOutlines",
                columns: table => new
                {
                    DocumentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Outline = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentOutlines", x => x.DocumentId);
                    table.ForeignKey(
                        name: "FK_DocumentOutlines_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PageNotes",
                columns: table => new
                {
                    PageId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PageNotes", x => x.PageId);
                    table.ForeignKey(
                        name: "FK_PageNotes_Pages_PageId",
                        column: x => x.PageId,
                        principalTable: "Pages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentOutlines");

            migrationBuilder.DropTable(
                name: "PageNotes");
        }
    }
}
