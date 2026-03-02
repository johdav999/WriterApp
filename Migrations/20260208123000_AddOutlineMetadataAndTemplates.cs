using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WriterApp.Data;

#nullable disable

namespace BlazorApp.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260208123000_AddOutlineMetadataAndTemplates")]
    public partial class AddOutlineMetadataAndTemplates : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MetadataJson",
                table: "DocumentOutlineNodes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlaceId",
                table: "SectionSceneCards",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PovCharacterId",
                table: "SectionSceneCards",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferencesJson",
                table: "SectionSceneCards",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TagsJson",
                table: "SectionSceneCards",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TimeRef",
                table: "SectionSceneCards",
                type: "TEXT",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TimelineEventId",
                table: "SectionSceneCards",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OutlineTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OwnerUserId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    TemplateJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutlineTemplates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OutlineTemplates_OwnerUserId",
                table: "OutlineTemplates",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OutlineTemplates_UpdatedUtc",
                table: "OutlineTemplates",
                column: "UpdatedUtc");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OutlineTemplates");

            migrationBuilder.DropColumn(
                name: "MetadataJson",
                table: "DocumentOutlineNodes");

            migrationBuilder.DropColumn(
                name: "PlaceId",
                table: "SectionSceneCards");

            migrationBuilder.DropColumn(
                name: "PovCharacterId",
                table: "SectionSceneCards");

            migrationBuilder.DropColumn(
                name: "ReferencesJson",
                table: "SectionSceneCards");

            migrationBuilder.DropColumn(
                name: "TagsJson",
                table: "SectionSceneCards");

            migrationBuilder.DropColumn(
                name: "TimeRef",
                table: "SectionSceneCards");

            migrationBuilder.DropColumn(
                name: "TimelineEventId",
                table: "SectionSceneCards");
        }
    }
}
