using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorApp.Migrations
{
    /// <inheritdoc />
    public partial class OwnedOnboardingDemo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OnboardingDemoWorkspaces",
                columns: table => new
                {
                    OwnerUserId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    OperationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Intent = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DocumentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SectionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SceneNodeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    RequestUsed = table.Column<bool>(type: "INTEGER", nullable: false),
                    ProposalId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnboardingDemoWorkspaces", x => x.OwnerUserId);
                });

            migrationBuilder.CreateTable(
                name: "OnboardingProgressOperations",
                columns: table => new
                {
                    OwnerUserId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    OperationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RequestHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ReceiptJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnboardingProgressOperations", x => new { x.OwnerUserId, x.OperationId });
                });

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingDemoWorkspaces_DocumentId",
                table: "OnboardingDemoWorkspaces",
                column: "DocumentId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OnboardingDemoWorkspaces");

            migrationBuilder.DropTable(
                name: "OnboardingProgressOperations");
        }
    }
}
