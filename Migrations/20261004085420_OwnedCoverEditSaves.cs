using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorApp.Migrations
{
    /// <inheritdoc />
    public partial class OwnedCoverEditSaves : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CoverEditSaves",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    OwnerUserId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: false),
                    State = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    ExpectedMetadataRevision = table.Column<long>(type: "INTEGER", nullable: false),
                    SavedMetadataRevision = table.Column<long>(type: "INTEGER", nullable: true),
                    RestoredMetadataRevision = table.Column<long>(type: "INTEGER", nullable: true),
                    BeforeCover = table.Column<string>(type: "TEXT", nullable: true),
                    AfterCover = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoverEditSaves", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CoverEditSaves_OwnerUserId_ProjectId",
                table: "CoverEditSaves",
                columns: new[] { "OwnerUserId", "ProjectId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "CoverEditSaves");
        }
    }
}
