using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorApp.Migrations.SqlServerMigrationsDb
{
    /// <inheritdoc />
    public partial class CollaborationRecoveryFenceSqlServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CollaborationRecoveryStates",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequiredEnvironment = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SealedAtUnixMilliseconds = table.Column<long>(type: "bigint", nullable: false)
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
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM CollaborationRecoveryStates) THROW 51073,'A recovery fence cannot be discarded by rollback.',1;");
            migrationBuilder.DropTable(
                name: "CollaborationRecoveryStates");
        }
    }
}
