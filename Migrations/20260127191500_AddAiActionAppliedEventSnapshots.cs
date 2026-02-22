using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorApp.Migrations
{
    /// <inheritdoc />
    public partial class AddAiActionAppliedEventSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AfterContent",
                table: "AiActionAppliedEvents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BeforeContent",
                table: "AiActionAppliedEvents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UndoneAt",
                table: "AiActionAppliedEvents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiActionAppliedEvents_UndoneAt",
                table: "AiActionAppliedEvents",
                column: "UndoneAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AiActionAppliedEvents_UndoneAt",
                table: "AiActionAppliedEvents");

            migrationBuilder.DropColumn(
                name: "AfterContent",
                table: "AiActionAppliedEvents");

            migrationBuilder.DropColumn(
                name: "BeforeContent",
                table: "AiActionAppliedEvents");

            migrationBuilder.DropColumn(
                name: "UndoneAt",
                table: "AiActionAppliedEvents");
        }
    }
}
