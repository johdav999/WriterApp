using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorApp.Migrations.SqlServerMigrationsDb
{
    /// <inheritdoc />
    public partial class WebTranslationOperationsSqlServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WebTranslationOperations",
                columns: table => new
                {
                    OwnerUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProposalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ApprovalJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RecoveryJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReceiptJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebTranslationOperations", x => new { x.OwnerUserId, x.OperationId });
                });

            migrationBuilder.CreateIndex(
                name: "IX_WebTranslationOperations_OwnerUserId_DocumentId",
                table: "WebTranslationOperations",
                columns: new[] { "OwnerUserId", "DocumentId" });

            migrationBuilder.CreateIndex(
                name: "IX_WebTranslationOperations_OwnerUserId_ProposalId",
                table: "WebTranslationOperations",
                columns: new[] { "OwnerUserId", "ProposalId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WebTranslationOperations");
        }
    }
}
