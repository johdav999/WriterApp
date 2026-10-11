using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WriterApp.Migrations
{
    /// <inheritdoc />
    public partial class BrainstormingPlanningSaves : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BrainstormingPlanningSaves",
                columns: table => new
                {
                    SessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OperationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CandidateId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReviewJson = table.Column<string>(type: "TEXT", nullable: false),
                    ReceiptJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrainstormingPlanningSaves", x => new { x.SessionId, x.OperationId });
                    table.ForeignKey(
                        name: "FK_BrainstormingPlanningSaves_BrainstormingSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "BrainstormingSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BrainstormingPlanningSaves_SessionId_CandidateId",
                table: "BrainstormingPlanningSaves",
                columns: new[] { "SessionId", "CandidateId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BrainstormingPlanningSaves");
        }
    }
}
