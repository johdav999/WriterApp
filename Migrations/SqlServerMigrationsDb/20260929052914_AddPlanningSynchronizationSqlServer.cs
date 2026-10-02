using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorApp.Migrations.SqlServerMigrationsDb
{
    /// <inheritdoc />
    public partial class AddPlanningSynchronizationSqlServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AnchorDetached",
                table: "SceneAnnotations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PlanningSyncEnabled",
                table: "Projects",
                type: "bit",
                nullable: false,
                defaultValue: false);
            foreach (string sql in WriterApp.Data.Documents.PlanningSyncSchemaV3.Install(true)) migrationBuilder.Sql(sql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (string sql in WriterApp.Data.Documents.PlanningSyncSchemaV3.Uninstall(true)) migrationBuilder.Sql(sql);
            migrationBuilder.DropColumn(
                name: "AnchorDetached",
                table: "SceneAnnotations");

            migrationBuilder.DropColumn(
                name: "PlanningSyncEnabled",
                table: "Projects");
        }
    }
}
