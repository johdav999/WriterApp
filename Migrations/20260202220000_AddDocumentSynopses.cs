using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WriterApp.Data;

#nullable disable

namespace BlazorApp.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260202220000_AddDocumentSynopses")]
    public partial class AddDocumentSynopses : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentSynopses",
                columns: table => new
                {
                    DocumentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Logline = table.Column<string>(type: "TEXT", nullable: false),
                    Premise = table.Column<string>(type: "TEXT", nullable: false),
                    Theme = table.Column<string>(type: "TEXT", nullable: false),
                    ProtagonistArc = table.Column<string>(type: "TEXT", nullable: false),
                    CentralConflict = table.Column<string>(type: "TEXT", nullable: false),
                    Stakes = table.Column<string>(type: "TEXT", nullable: false),
                    Setting = table.Column<string>(type: "TEXT", nullable: false),
                    EndingIntent = table.Column<string>(type: "TEXT", nullable: false),
                    OpenQuestions = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentSynopses", x => x.DocumentId);
                    table.ForeignKey(
                        name: "FK_DocumentSynopses_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentSynopses_UpdatedAt",
                table: "DocumentSynopses",
                column: "UpdatedAt");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentSynopses");
        }
    }
}
