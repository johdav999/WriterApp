using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorApp.Migrations
{
    /// <inheritdoc />
    public partial class CollaborationSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CollaborationSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DocumentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PageId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Generation = table.Column<Guid>(type: "TEXT", nullable: false),
                    InstallationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OwnerUserId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Environment = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    SchemaFingerprint = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ProtocolVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    ContentContractVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    Version = table.Column<long>(type: "INTEGER", nullable: false),
                    ExpiresAtUnixMilliseconds = table.Column<long>(type: "INTEGER", nullable: false),
                    Revoked = table.Column<bool>(type: "INTEGER", nullable: false),
                    ConnectionId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollaborationSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CollaborationSessions_CollaborationPages_PageId_Generation",
                        columns: x => new { x.PageId, x.Generation },
                        principalTable: "CollaborationPages",
                        principalColumns: new[] { "PageId", "Generation" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CollaborationSessions_ExpiresAtUnixMilliseconds",
                table: "CollaborationSessions",
                column: "ExpiresAtUnixMilliseconds");

            migrationBuilder.CreateIndex(
                name: "IX_CollaborationSessions_PageId_Generation",
                table: "CollaborationSessions",
                columns: new[] { "PageId", "Generation" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CollaborationSessions");
        }
    }
}
