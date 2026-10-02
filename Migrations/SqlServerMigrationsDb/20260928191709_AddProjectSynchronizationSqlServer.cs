using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorApp.Migrations.SqlServerMigrationsDb
{
    /// <inheritdoc />
    public partial class AddProjectSynchronizationSqlServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SyncEnabled",
                table: "Projects",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "SyncDeletionId",
                table: "ProjectNodes",
                type: "uniqueidentifier",
                nullable: true);
            foreach (string sql in WriterApp.Data.Documents.ProjectSyncSchemaV2.Install(true)) migrationBuilder.Sql(sql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (string sql in WriterApp.Data.Documents.ProjectSyncSchemaV2.Uninstall(true)) migrationBuilder.Sql(sql);
            migrationBuilder.DropColumn(
                name: "SyncEnabled",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "SyncDeletionId",
                table: "ProjectNodes");
        }
    }
}
