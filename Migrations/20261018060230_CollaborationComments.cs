using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorApp.Migrations
{
    /// <inheritdoc />
    public partial class CollaborationComments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CollaborationComments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DocumentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PageId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Generation = table.Column<Guid>(type: "TEXT", nullable: false),
                    AuthorId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Start = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    End = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    Body = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    Resolved = table.Column<bool>(type: "INTEGER", nullable: false),
                    Revision = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAtUnixMilliseconds = table.Column<long>(type: "INTEGER", nullable: false),
                    LegacySource = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollaborationComments", x => x.Id);
                    table.CheckConstraint("CK_CollaborationComments_Revision", "Revision > 0");
                    table.ForeignKey(
                        name: "FK_CollaborationComments_CollaborationPages_PageId_Generation",
                        columns: x => new { x.PageId, x.Generation },
                        principalTable: "CollaborationPages",
                        principalColumns: new[] { "PageId", "Generation" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CollaborationComments_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CollaborationCommentMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ThreadId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AuthorId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Body = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    CreatedAtUnixMilliseconds = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollaborationCommentMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CollaborationCommentMessages_CollaborationComments_ThreadId",
                        column: x => x.ThreadId,
                        principalTable: "CollaborationComments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CollaborationCommentOperations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ThreadId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ActorId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    RequestSha256 = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ResultRevision = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollaborationCommentOperations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CollaborationCommentOperations_CollaborationComments_ThreadId",
                        column: x => x.ThreadId,
                        principalTable: "CollaborationComments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CollaborationCommentMessages_ThreadId",
                table: "CollaborationCommentMessages",
                column: "ThreadId");

            migrationBuilder.CreateIndex(
                name: "IX_CollaborationCommentOperations_ThreadId",
                table: "CollaborationCommentOperations",
                column: "ThreadId");

            migrationBuilder.CreateIndex(
                name: "IX_CollaborationComments_DocumentId",
                table: "CollaborationComments",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_CollaborationComments_PageId_Generation",
                table: "CollaborationComments",
                columns: new[] { "PageId", "Generation" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE TEMP TABLE CollaborationCommentsRollbackGuard (Value INTEGER CHECK (Value = 0)); INSERT INTO CollaborationCommentsRollbackGuard SELECT COUNT(*) FROM CollaborationComments; DROP TABLE CollaborationCommentsRollbackGuard;");
            migrationBuilder.DropTable(
                name: "CollaborationCommentMessages");

            migrationBuilder.DropTable(
                name: "CollaborationCommentOperations");

            migrationBuilder.DropTable(
                name: "CollaborationComments");
        }
    }
}
