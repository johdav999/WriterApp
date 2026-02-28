using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorApp.Migrations
{
    /// <inheritdoc />
    public partial class AddStripeEventLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StripeEventLogs",
                columns: table => new
                {
                    StripeEventId = table.Column<string>(maxLength: 100, nullable: false),
                    Type = table.Column<string>(maxLength: 100, nullable: false),
                    Status = table.Column<string>(maxLength: 50, nullable: false),
                    ReceivedUtc = table.Column<DateTime>(nullable: false),
                    ProcessedUtc = table.Column<DateTime>(nullable: true),
                    Error = table.Column<string>(maxLength: 2000, nullable: true),
                    UserId = table.Column<string>(maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StripeEventLogs", x => x.StripeEventId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StripeEventLogs_ReceivedUtc",
                table: "StripeEventLogs",
                column: "ReceivedUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StripeEventLogs");
        }
    }
}
