using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WriterApp.Data;

#nullable disable

namespace BlazorApp.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260207110000_AddPromptPresets")]
    public partial class AddPromptPresets : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PromptPresets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OwnerUserId = table.Column<string>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Category = table.Column<string>(type: "TEXT", nullable: true),
                    Kind = table.Column<string>(type: "TEXT", nullable: false),
                    BuiltinActionId = table.Column<string>(type: "TEXT", nullable: true),
                    TemplateText = table.Column<string>(type: "TEXT", nullable: true),
                    ParametersJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromptPresets", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PromptPresets_OwnerUserId",
                table: "PromptPresets",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PromptPresets_OwnerUserId_ProjectId",
                table: "PromptPresets",
                columns: new[] { "OwnerUserId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_PromptPresets_OwnerUserId_Kind",
                table: "PromptPresets",
                columns: new[] { "OwnerUserId", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_PromptPresets_UpdatedUtc",
                table: "PromptPresets",
                column: "UpdatedUtc");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PromptPresets");
        }
    }
}
