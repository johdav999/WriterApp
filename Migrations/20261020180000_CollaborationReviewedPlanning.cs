using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WriterApp.Data;
using WriterApp.Data.Collaboration;
namespace BlazorApp.Migrations;
[DbContext(typeof(AppDbContext)), Migration("20261020180000_CollaborationReviewedPlanning")]
public sealed class CollaborationReviewedPlanning : Migration
{
    protected override void Up(MigrationBuilder b) { foreach(var sql in CollaborationPlanningSchema.Install(false)) b.Sql(sql); }
    protected override void Down(MigrationBuilder b) { foreach(var sql in CollaborationPlanningSchema.Uninstall(false)) b.Sql(sql); }
}
