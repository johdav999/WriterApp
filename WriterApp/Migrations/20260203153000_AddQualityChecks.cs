using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WriterApp.Data;

#nullable disable

namespace BlazorApp.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260203153000_AddQualityChecks")]
    public partial class AddQualityChecks : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentGlossaryEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DocumentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Term = table.Column<string>(type: "TEXT", nullable: false),
                    NormalizedTerm = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentGlossaryEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentGlossaryEntries_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PageQualityIssues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DocumentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PageId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Scope = table.Column<string>(type: "TEXT", nullable: false),
                    IssueKey = table.Column<string>(type: "TEXT", nullable: false),
                    RuleId = table.Column<string>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", nullable: false),
                    Severity = table.Column<string>(type: "TEXT", nullable: false),
                    Message = table.Column<string>(type: "TEXT", nullable: false),
                    Suggestion = table.Column<string>(type: "TEXT", nullable: true),
                    AnchorText = table.Column<string>(type: "TEXT", nullable: true),
                    StartOffset = table.Column<int>(type: "INTEGER", nullable: false),
                    EndOffset = table.Column<int>(type: "INTEGER", nullable: false),
                    ContentHash = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PageQualityIssues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PageQualityIssues_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PageQualityIssues_Pages_PageId",
                        column: x => x.PageId,
                        principalTable: "Pages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PageQualityIssueDismissals",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    PageId = table.Column<Guid>(type: "TEXT", nullable: false),
                    IssueKey = table.Column<string>(type: "TEXT", nullable: false),
                    DismissedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PageQualityIssueDismissals", x => new { x.UserId, x.PageId, x.IssueKey });
                    table.ForeignKey(
                        name: "FK_PageQualityIssueDismissals_Pages_PageId",
                        column: x => x.PageId,
                        principalTable: "Pages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentGlossaryEntries_DocumentId",
                table: "DocumentGlossaryEntries",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentGlossaryEntries_NormalizedTerm",
                table: "DocumentGlossaryEntries",
                column: "NormalizedTerm");

            migrationBuilder.CreateIndex(
                name: "IX_PageQualityIssueDismissals_PageId",
                table: "PageQualityIssueDismissals",
                column: "PageId");

            migrationBuilder.CreateIndex(
                name: "IX_PageQualityIssues_ContentHash",
                table: "PageQualityIssues",
                column: "ContentHash");

            migrationBuilder.CreateIndex(
                name: "IX_PageQualityIssues_DocumentId",
                table: "PageQualityIssues",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_PageQualityIssues_IssueKey",
                table: "PageQualityIssues",
                column: "IssueKey");

            migrationBuilder.CreateIndex(
                name: "IX_PageQualityIssues_PageId",
                table: "PageQualityIssues",
                column: "PageId");

            migrationBuilder.CreateIndex(
                name: "IX_PageQualityIssues_Scope",
                table: "PageQualityIssues",
                column: "Scope");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentGlossaryEntries");

            migrationBuilder.DropTable(
                name: "PageQualityIssueDismissals");

            migrationBuilder.DropTable(
                name: "PageQualityIssues");
        }
    }
}
