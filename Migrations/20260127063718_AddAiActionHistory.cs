using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorApp.Migrations
{
    /// <inheritdoc />
    public partial class AddAiActionHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiActionHistoryEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OwnerUserId = table.Column<string>(type: "TEXT", nullable: false),
                    DocumentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SectionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    PageId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ActionKey = table.Column<string>(type: "TEXT", nullable: false),
                    ProviderId = table.Column<string>(type: "TEXT", nullable: true),
                    ModelId = table.Column<string>(type: "TEXT", nullable: true),
                    RequestJson = table.Column<string>(type: "TEXT", nullable: false),
                    ResultJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiActionHistoryEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiActionAppliedEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OwnerUserId = table.Column<string>(type: "TEXT", nullable: false),
                    HistoryEntryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AppliedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    AppliedToPageId = table.Column<Guid>(type: "TEXT", nullable: true),
                    AppliedToSectionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    AppliedToDocumentId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiActionAppliedEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiActionAppliedEvents_AiActionHistoryEntries_HistoryEntryId",
                        column: x => x.HistoryEntryId,
                        principalTable: "AiActionHistoryEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiActionAppliedEvents_AppliedAt",
                table: "AiActionAppliedEvents",
                column: "AppliedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AiActionAppliedEvents_HistoryEntryId",
                table: "AiActionAppliedEvents",
                column: "HistoryEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_AiActionAppliedEvents_OwnerUserId",
                table: "AiActionAppliedEvents",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AiActionHistoryEntries_ActionKey",
                table: "AiActionHistoryEntries",
                column: "ActionKey");

            migrationBuilder.CreateIndex(
                name: "IX_AiActionHistoryEntries_CreatedAt",
                table: "AiActionHistoryEntries",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AiActionHistoryEntries_DocumentId",
                table: "AiActionHistoryEntries",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_AiActionHistoryEntries_OwnerUserId",
                table: "AiActionHistoryEntries",
                column: "OwnerUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiActionAppliedEvents");

            migrationBuilder.DropTable(
                name: "AiActionHistoryEntries");
        }
    }
}
