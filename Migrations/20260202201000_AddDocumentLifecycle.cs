using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WriterApp.Data;

#nullable disable

namespace BlazorApp.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260202201000_AddDocumentLifecycle")]
    public partial class AddDocumentLifecycle : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Documents",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ArchivedAt",
                table: "Documents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "Documents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Documents_IsArchived",
                table: "Documents",
                column: "IsArchived");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_DeletedAt",
                table: "Documents",
                column: "DeletedAt");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Documents_IsArchived",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Documents_DeletedAt",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Documents");
        }
    }
}
