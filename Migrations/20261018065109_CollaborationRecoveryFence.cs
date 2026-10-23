using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorApp.Migrations
{
    /// <inheritdoc />
    public partial class CollaborationRecoveryFence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CollaborationRecoveryStates",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    OperationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RequiredEnvironment = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    SealedAtUnixMilliseconds = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollaborationRecoveryStates", x => x.Id);
                    table.CheckConstraint("CK_CollaborationRecoveryStates_Singleton", "Id = 'database'");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE TEMP TABLE CollaborationRecoveryRollbackGuard (Value INTEGER CHECK(Value=0)); INSERT INTO CollaborationRecoveryRollbackGuard SELECT COUNT(*) FROM CollaborationRecoveryStates; DROP TABLE CollaborationRecoveryRollbackGuard;");
            migrationBuilder.DropTable(
                name: "CollaborationRecoveryStates");
        }
    }
}
