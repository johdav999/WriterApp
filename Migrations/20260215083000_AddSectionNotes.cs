using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WriterApp.Data;

#nullable disable

namespace BlazorApp.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260215083000_AddSectionNotes")]
    public partial class AddSectionNotes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SectionNotes",
                columns: table => new
                {
                    SectionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    NotesText = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SectionNotes", x => x.SectionId);
                    table.ForeignKey(
                        name: "FK_SectionNotes_Sections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "Sections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SectionNotes");
        }
    }
}
