using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WriterApp.Data;

#nullable disable

namespace BlazorApp.Migrations.SqlServerMigrationsDb
{
    [DbContext(typeof(SqlServerMigrationsDbContext))]
    [Migration("20260308173000_AddUserProfileEmailSqlServer")]
    public partial class AddUserProfileEmailSqlServer : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF COL_LENGTH(N'dbo.UserProfiles', N'Email') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[UserProfiles]
                    ADD [Email] nvarchar(320) NULL;
                END
                """);

            migrationBuilder.Sql(
                """
                UPDATE [dbo].[UserProfiles]
                SET [Email] = CASE
                    WHEN CHARINDEX('@', [DisplayName]) > 0 THEN [DisplayName]
                    WHEN CHARINDEX('@', [UserId]) > 0 THEN [UserId]
                    ELSE NULL
                END
                WHERE [Email] IS NULL;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF COL_LENGTH(N'dbo.UserProfiles', N'Email') IS NOT NULL
                BEGIN
                    ALTER TABLE [dbo].[UserProfiles]
                    DROP COLUMN [Email];
                END
                """);
        }
    }
}
