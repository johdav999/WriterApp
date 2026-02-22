using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WriterApp.Data;

#nullable disable

namespace BlazorApp.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260204190000_AddExportPresets")]
    public partial class AddExportPresets : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExportPresets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OwnerUserId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    IsGlobalDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                    SettingsJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExportPresets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProjectExportSettings",
                columns: table => new
                {
                    DocumentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    DefaultPresetId = table.Column<Guid>(type: "TEXT", nullable: true),
                    OverridesJson = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectExportSettings", x => new { x.DocumentId, x.UserId });
                    table.ForeignKey(
                        name: "FK_ProjectExportSettings_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectExportSettings_ExportPresets_DefaultPresetId",
                        column: x => x.DefaultPresetId,
                        principalTable: "ExportPresets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExportPresets_OwnerUserId",
                table: "ExportPresets",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ExportPresets_OwnerUserId_IsGlobalDefault",
                table: "ExportPresets",
                columns: new[] { "OwnerUserId", "IsGlobalDefault" });

            migrationBuilder.CreateIndex(
                name: "IX_ExportPresets_UpdatedAt",
                table: "ExportPresets",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectExportSettings_DefaultPresetId",
                table: "ProjectExportSettings",
                column: "DefaultPresetId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectExportSettings_UserId",
                table: "ProjectExportSettings",
                column: "UserId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectExportSettings");

            migrationBuilder.DropTable(
                name: "ExportPresets");
        }
    }
}
