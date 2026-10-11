using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WriterApp.Migrations.SqlServerMigrationsDb
{
    /// <inheritdoc />
    public partial class BrainstormingRecoveryHistorySqlServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RecoveryJson",
                table: "BrainstormingSessions",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RecoveryJson",
                table: "BrainstormingSessions");
        }
    }
}
