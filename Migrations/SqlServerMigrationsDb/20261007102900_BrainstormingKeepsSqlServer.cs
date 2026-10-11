using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorApp.Migrations.SqlServerMigrationsDb
{
    /// <inheritdoc />
    public partial class BrainstormingKeepsSqlServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BrainstormingKeeps",
                columns: table => new
                {
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReceiptJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MovesJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrainstormingKeeps", x => new { x.SessionId, x.OperationId });
                    table.ForeignKey(
                        name: "FK_BrainstormingKeeps_BrainstormingSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "BrainstormingSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BrainstormingKeeps_SessionId_CandidateId",
                table: "BrainstormingKeeps",
                columns: new[] { "SessionId", "CandidateId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BrainstormingKeeps");
        }
    }
}
