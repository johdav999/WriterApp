using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WriterApp.Data;
using WriterApp.Data.Collaboration;
namespace BlazorApp.Migrations.SqlServerMigrationsDb;
[DbContext(typeof(SqlServerMigrationsDbContext)), Migration("20261020180001_CollaborationReviewedPlanningSqlServer")]
public sealed class CollaborationReviewedPlanningSqlServer : Migration
{
    protected override void Up(MigrationBuilder b) { foreach(var sql in CollaborationPlanningSchema.Install(true)) b.Sql(sql); }
    protected override void Down(MigrationBuilder b) { foreach(var sql in CollaborationPlanningSchema.Uninstall(true)) b.Sql(sql); }
}
