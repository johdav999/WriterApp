using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WriterApp.Migrations
{
    /// <inheritdoc />
    public partial class BrainstormingRecoveryHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RecoveryJson",
                table: "BrainstormingSessions",
                type: "TEXT",
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
