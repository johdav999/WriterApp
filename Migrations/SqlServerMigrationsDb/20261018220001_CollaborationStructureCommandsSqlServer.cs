using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WriterApp.Data;
using WriterApp.Data.Collaboration;
namespace BlazorApp.Migrations.SqlServerMigrationsDb;
[DbContext(typeof(SqlServerMigrationsDbContext)), Migration("20261018220001_CollaborationStructureCommandsSqlServer")]
public sealed class CollaborationStructureCommandsSqlServer : Migration
{
    protected override void Up(MigrationBuilder b) { foreach(var sql in CollaborationStructureSchema.Install(true)) b.Sql(sql); }
    protected override void Down(MigrationBuilder b) { foreach(var sql in CollaborationStructureSchema.Uninstall(true)) b.Sql(sql); }
}
