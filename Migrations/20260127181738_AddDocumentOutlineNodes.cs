using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorApp.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentOutlineNodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentOutlineNodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DocumentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ParentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    LinkedSectionId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentOutlineNodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentOutlineNodes_DocumentOutlineNodes_ParentId",
                        column: x => x.ParentId,
                        principalTable: "DocumentOutlineNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DocumentOutlineNodes_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DocumentOutlineNodes_Sections_LinkedSectionId",
                        column: x => x.LinkedSectionId,
                        principalTable: "Sections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentOutlineNodes_DocumentId_ParentId_Order",
                table: "DocumentOutlineNodes",
                columns: new[] { "DocumentId", "ParentId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentOutlineNodes_LinkedSectionId",
                table: "DocumentOutlineNodes",
                column: "LinkedSectionId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentOutlineNodes_ParentId",
                table: "DocumentOutlineNodes",
                column: "ParentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentOutlineNodes");
        }
    }
}
