using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace BlazorApp.Migrations.SqlServerMigrationsDb;
public partial class WebScopedPlanningRecoverySqlServer : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.AddColumn<string>(name: "RecoveryJson", table: "WebAiHistoryOperations", type: "nvarchar(max)", nullable: true);
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropColumn(name: "RecoveryJson", table: "WebAiHistoryOperations");
}
