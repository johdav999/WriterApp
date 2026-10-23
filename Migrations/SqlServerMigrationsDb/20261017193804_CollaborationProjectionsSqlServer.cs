using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorApp.Migrations.SqlServerMigrationsDb
{
    /// <inheritdoc />
    public partial class CollaborationProjectionsSqlServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CollaborationCommands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    StructureRevision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollaborationCommands", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CollaborationCommands_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CollaborationManifests",
                columns: table => new
                {
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StructureRevision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollaborationManifests", x => x.DocumentId);
                    table.ForeignKey(
                        name: "FK_CollaborationManifests_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CollaborationProjections",
                columns: table => new
                {
                    PageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Generation = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    Html = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Json = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StateSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollaborationProjections", x => new { x.PageId, x.Generation });
                    table.ForeignKey(
                        name: "FK_CollaborationProjections_CollaborationPages_PageId_Generation",
                        columns: x => new { x.PageId, x.Generation },
                        principalTable: "CollaborationPages",
                        principalColumns: new[] { "PageId", "Generation" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CollaborationCommands_DocumentId",
                table: "CollaborationCommands",
                column: "DocumentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS(SELECT 1 FROM CollaborationManifests) OR EXISTS(SELECT 1 FROM CollaborationCommands) OR EXISTS(SELECT 1 FROM CollaborationProjections) THROW 51070, 'collaboration_retained_state', 1;");
            migrationBuilder.DropTable(
                name: "CollaborationCommands");

            migrationBuilder.DropTable(
                name: "CollaborationManifests");

            migrationBuilder.DropTable(
                name: "CollaborationProjections");
        }
    }
}
