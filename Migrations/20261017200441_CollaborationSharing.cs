using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorApp.Migrations
{
    /// <inheritdoc />
    public partial class CollaborationSharing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "MembershipRevision",
                table: "CollaborationSessions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "ResourceOwnerUserId",
                table: "CollaborationSessions",
                type: "TEXT",
                maxLength: 128,
                nullable: false,
                defaultValue: "");
            migrationBuilder.Sql("UPDATE CollaborationSessions SET ResourceOwnerUserId=OwnerUserId, Revoked=1, Version=Version+1;");

            migrationBuilder.CreateTable(
                name: "CollaborationInvitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DocumentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Role = table.Column<int>(type: "INTEGER", nullable: false),
                    RecipientUserId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    RecipientEmail = table.Column<string>(type: "TEXT", maxLength: 254, nullable: true),
                    TokenSha256 = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    RequestSha256 = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ExpiresAtUnixMilliseconds = table.Column<long>(type: "INTEGER", nullable: false),
                    Revision = table.Column<long>(type: "INTEGER", nullable: false),
                    Revoked = table.Column<bool>(type: "INTEGER", nullable: false),
                    AcceptedBy = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    AcceptedAtUnixMilliseconds = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollaborationInvitations", x => x.Id);
                    table.CheckConstraint("CK_CollaborationInvitations_TargetRole", "Role IN (1,2) AND Revision > 0 AND ((RecipientUserId IS NULL AND RecipientEmail IS NOT NULL) OR (RecipientUserId IS NOT NULL AND RecipientEmail IS NULL))");
                    table.ForeignKey(
                        name: "FK_CollaborationInvitations_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CollaborationMembers",
                columns: table => new
                {
                    DocumentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Role = table.Column<int>(type: "INTEGER", nullable: false),
                    Revision = table.Column<long>(type: "INTEGER", nullable: false),
                    Revoked = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollaborationMembers", x => new { x.DocumentId, x.UserId });
                    table.CheckConstraint("CK_CollaborationMembers_RoleRevision", "Role IN (1,2) AND Revision > 0");
                    table.ForeignKey(
                        name: "FK_CollaborationMembers_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CollaborationSupportingLinks",
                columns: table => new
                {
                    ManuscriptId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SupportingDocumentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Revision = table.Column<long>(type: "INTEGER", nullable: false),
                    Revoked = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollaborationSupportingLinks", x => new { x.ManuscriptId, x.SupportingDocumentId });
                    table.ForeignKey(
                        name: "FK_CollaborationSupportingLinks_Documents_ManuscriptId",
                        column: x => x.ManuscriptId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CollaborationSupportingLinks_Documents_SupportingDocumentId",
                        column: x => x.SupportingDocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CollaborationInvitations_DocumentId",
                table: "CollaborationInvitations",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_CollaborationMembers_UserId_Revoked",
                table: "CollaborationMembers",
                columns: new[] { "UserId", "Revoked" });

            migrationBuilder.CreateIndex(
                name: "IX_CollaborationSupportingLinks_SupportingDocumentId",
                table: "CollaborationSupportingLinks",
                column: "SupportingDocumentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE TEMP TABLE CollaborationP8RollbackGuard (Value INTEGER CHECK(Value=0)); INSERT INTO CollaborationP8RollbackGuard SELECT 1 WHERE EXISTS(SELECT 1 FROM CollaborationMembers) OR EXISTS(SELECT 1 FROM CollaborationInvitations) OR EXISTS(SELECT 1 FROM CollaborationSupportingLinks) OR EXISTS(SELECT 1 FROM CollaborationSessions WHERE ResourceOwnerUserId<>OwnerUserId); DROP TABLE CollaborationP8RollbackGuard;");
            migrationBuilder.DropTable(
                name: "CollaborationInvitations");

            migrationBuilder.DropTable(
                name: "CollaborationMembers");

            migrationBuilder.DropTable(
                name: "CollaborationSupportingLinks");

            migrationBuilder.DropColumn(
                name: "MembershipRevision",
                table: "CollaborationSessions");

            migrationBuilder.DropColumn(
                name: "ResourceOwnerUserId",
                table: "CollaborationSessions");
        }
    }
}
