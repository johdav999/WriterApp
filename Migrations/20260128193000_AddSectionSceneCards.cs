using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorApp.Migrations
{
    /// <inheritdoc />
    public partial class AddSectionSceneCards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SectionSceneCards",
                columns: table => new
                {
                    SectionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    NarrativePurpose = table.Column<string>(type: "TEXT", nullable: true),
                    EmotionalBeat = table.Column<string>(type: "TEXT", nullable: true),
                    KeyEvents = table.Column<string>(type: "TEXT", nullable: true),
                    OpenQuestions = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SectionSceneCards", x => x.SectionId);
                    table.ForeignKey(
                        name: "FK_SectionSceneCards_Sections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "Sections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SectionSceneCards");
        }
    }
}
