using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorApp.Migrations
{
    /// <inheritdoc />
    public partial class WebAiHistoryOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WebAiHistoryOperations",
                columns: table => new
                {
                    OwnerUserId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    OperationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProposalId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DocumentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Sequence = table.Column<int>(type: "INTEGER", nullable: false),
                    Outcome = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    RequestHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    IntentJson = table.Column<string>(type: "TEXT", nullable: false),
                    SavedResponseJson = table.Column<string>(type: "TEXT", nullable: true),
                    CommittedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ReportedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebAiHistoryOperations", x => new { x.OwnerUserId, x.OperationId });
                });

            migrationBuilder.CreateIndex(
                name: "IX_WebAiHistoryOperations_OwnerUserId_ApplicationId_Sequence",
                table: "WebAiHistoryOperations",
                columns: new[] { "OwnerUserId", "ApplicationId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WebAiHistoryOperations_OwnerUserId_DocumentId",
                table: "WebAiHistoryOperations",
                columns: new[] { "OwnerUserId", "DocumentId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WebAiHistoryOperations");
        }
    }
}
