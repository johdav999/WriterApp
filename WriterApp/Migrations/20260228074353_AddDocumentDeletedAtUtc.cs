using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorApp.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentDeletedAtUtc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAtUtc",
                table: "Documents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "Documents"
                SET "DeletedAtUtc" = STRFTIME('%Y-%m-%d %H:%M:%f', "DeletedAt")
                WHERE "DeletedAt" IS NOT NULL AND "DeletedAtUtc" IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Documents_DeletedAtUtc",
                table: "Documents",
                column: "DeletedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Documents_DeletedAtUtc",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "Documents");
        }
    }
}
