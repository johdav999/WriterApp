using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WriterApp.Migrations.SqlServerMigrationsDb
{
    /// <inheritdoc />
    public partial class ReusablePresetTransfersSqlServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Pinned",
                table: "PromptPresets",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Scope",
                table: "PromptPresets",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PromptPresetTransfers",
                columns: table => new
                {
                    OwnerUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ResultJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromptPresetTransfers", x => new { x.OwnerUserId, x.OperationId });
                });

            migrationBuilder.CreateIndex(
                name: "IX_PromptPresetTransfers_CreatedUtc",
                table: "PromptPresetTransfers",
                column: "CreatedUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PromptPresetTransfers");

            migrationBuilder.DropColumn(
                name: "Pinned",
                table: "PromptPresets");

            migrationBuilder.DropColumn(
                name: "Scope",
                table: "PromptPresets");
        }
    }
}
