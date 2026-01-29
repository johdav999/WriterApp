using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorApp.Migrations
{
    /// <inheritdoc />
    public partial class AddExportTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExportTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OwnerUserId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    PresetKey = table.Column<string>(type: "TEXT", nullable: true),
                    PageWidthMm = table.Column<int>(type: "INTEGER", nullable: false),
                    PageHeightMm = table.Column<int>(type: "INTEGER", nullable: false),
                    MarginTopMm = table.Column<int>(type: "INTEGER", nullable: false),
                    MarginRightMm = table.Column<int>(type: "INTEGER", nullable: false),
                    MarginBottomMm = table.Column<int>(type: "INTEGER", nullable: false),
                    MarginLeftMm = table.Column<int>(type: "INTEGER", nullable: false),
                    FontFamily = table.Column<string>(type: "TEXT", nullable: false),
                    BodyFontSizePt = table.Column<int>(type: "INTEGER", nullable: false),
                    LineHeight = table.Column<decimal>(type: "TEXT", nullable: false),
                    ParagraphSpacingPt = table.Column<int>(type: "INTEGER", nullable: false),
                    HeaderEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    HeaderLeft = table.Column<string>(type: "TEXT", nullable: true),
                    HeaderCenter = table.Column<string>(type: "TEXT", nullable: true),
                    HeaderRight = table.Column<string>(type: "TEXT", nullable: true),
                    FooterEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    FooterLeft = table.Column<string>(type: "TEXT", nullable: true),
                    FooterCenter = table.Column<string>(type: "TEXT", nullable: true),
                    FooterRight = table.Column<string>(type: "TEXT", nullable: true),
                    PageNumbersEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    PageNumberStart = table.Column<int>(type: "INTEGER", nullable: false),
                    TocEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    TocDepth = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExportTemplates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExportTemplates_OwnerUserId",
                table: "ExportTemplates",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ExportTemplates_OwnerUserId_PresetKey",
                table: "ExportTemplates",
                columns: new[] { "OwnerUserId", "PresetKey" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExportTemplates");
        }
    }
}
