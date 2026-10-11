using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WriterApp.Migrations
{
    /// <inheritdoc />
    public partial class BrainstormingTranscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BrainstormingTranscriptions",
                columns: table => new
                {
                    SessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OperationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PayloadHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ResultJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrainstormingTranscriptions", x => new { x.SessionId, x.OperationId });
                    table.ForeignKey(
                        name: "FK_BrainstormingTranscriptions_BrainstormingSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "BrainstormingSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BrainstormingTranscriptions");
        }
    }
}
