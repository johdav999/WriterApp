using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WriterApp.Data;

#nullable disable

namespace BlazorApp.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260203120000_AddPageAnnotations")]
    public partial class AddPageAnnotations : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PageAnnotations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DocumentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PageId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    AnchorFrom = table.Column<int>(type: "INTEGER", nullable: false),
                    AnchorTo = table.Column<int>(type: "INTEGER", nullable: false),
                    AnchorText = table.Column<string>(type: "TEXT", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    AuthorUserId = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PageAnnotations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PageAnnotations_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PageAnnotations_Pages_PageId",
                        column: x => x.PageId,
                        principalTable: "Pages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PageAnnotations_CreatedAt",
                table: "PageAnnotations",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PageAnnotations_DocumentId",
                table: "PageAnnotations",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_PageAnnotations_Kind",
                table: "PageAnnotations",
                column: "Kind");

            migrationBuilder.CreateIndex(
                name: "IX_PageAnnotations_PageId",
                table: "PageAnnotations",
                column: "PageId");

            migrationBuilder.CreateIndex(
                name: "IX_PageAnnotations_Status",
                table: "PageAnnotations",
                column: "Status");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PageAnnotations");
        }
    }
}
