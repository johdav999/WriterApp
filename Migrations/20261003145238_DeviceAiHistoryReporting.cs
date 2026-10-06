using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorApp.Migrations
{
    /// <inheritdoc />
    public partial class DeviceAiHistoryReporting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeviceAiHistoryEvents",
                columns: table => new
                {
                    OwnerUserId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    OperationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProposalId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DocumentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LocalEntryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Sequence = table.Column<int>(type: "INTEGER", nullable: false),
                    State = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    RequestHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ReportJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceAiHistoryEvents", x => new { x.OwnerUserId, x.OperationId });
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceAiHistoryEvents_OwnerUserId_DocumentId",
                table: "DeviceAiHistoryEvents",
                columns: new[] { "OwnerUserId", "DocumentId" });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceAiHistoryEvents_OwnerUserId_LocalEntryId_Sequence",
                table: "DeviceAiHistoryEvents",
                columns: new[] { "OwnerUserId", "LocalEntryId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeviceAiHistoryEvents_ProposalId",
                table: "DeviceAiHistoryEvents",
                column: "ProposalId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeviceAiHistoryEvents");
        }
    }
}
