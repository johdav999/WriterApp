using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WriterApp.Data;
using WriterApp.Data.Collaboration;
namespace BlazorApp.Migrations;
[DbContext(typeof(AppDbContext)), Migration("20261018220000_CollaborationStructureCommands")]
public sealed class CollaborationStructureCommands : Migration
{
    protected override void Up(MigrationBuilder b) { foreach(var sql in CollaborationStructureSchema.Install(false)) b.Sql(sql); }
    protected override void Down(MigrationBuilder b) { foreach(var sql in CollaborationStructureSchema.Uninstall(false)) b.Sql(sql); }
}
