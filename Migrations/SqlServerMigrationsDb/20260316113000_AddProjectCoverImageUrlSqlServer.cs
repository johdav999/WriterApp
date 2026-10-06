using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using WriterApp.Data;

#nullable disable

namespace BlazorApp.Migrations.SqlServerMigrationsDb
{
    [DbContext(typeof(SqlServerMigrationsDbContext))]
    [Migration("20260316113000_AddProjectCoverImageUrlSqlServer")]
    public partial class AddProjectCoverImageUrlSqlServer : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Previously missing discovery metadata left this migration out of the SQL Server chain.
            // Existing installations may already have repaired the column manually; retain their bytes.
            migrationBuilder.Sql("IF COL_LENGTH(N'Projects', N'CoverImageUrl') IS NULL ALTER TABLE [Projects] ADD [CoverImageUrl] nvarchar(max) NULL;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CoverImageUrl",
                table: "Projects");
        }
    }
}
