using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorApp.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectSynchronization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SyncEnabled",
                table: "Projects",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "SyncDeletionId",
                table: "ProjectNodes",
                type: "TEXT",
                nullable: true);
            foreach (string sql in WriterApp.Data.Documents.ProjectSyncSchemaV2.Install(false)) migrationBuilder.Sql(sql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (string sql in WriterApp.Data.Documents.ProjectSyncSchemaV2.Uninstall(false)) migrationBuilder.Sql(sql);
            migrationBuilder.DropColumn(
                name: "SyncEnabled",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "SyncDeletionId",
                table: "ProjectNodes");
        }
    }
}
