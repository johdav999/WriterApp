using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace BlazorApp.Migrations;
public partial class WebScopedPlanningRecovery : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.AddColumn<string>(name: "RecoveryJson", table: "WebAiHistoryOperations", type: "TEXT", nullable: true);
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropColumn(name: "RecoveryJson", table: "WebAiHistoryOperations");
}
