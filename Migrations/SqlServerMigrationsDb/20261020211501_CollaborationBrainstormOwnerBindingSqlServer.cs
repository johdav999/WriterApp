using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WriterApp.Data;
namespace BlazorApp.Migrations.SqlServerMigrationsDb;
[DbContext(typeof(SqlServerMigrationsDbContext)), Migration("20261020211501_CollaborationBrainstormOwnerBindingSqlServer")]
public sealed class CollaborationBrainstormOwnerBindingSqlServer : Migration
{
    protected override void Up(MigrationBuilder b) => b.AddColumn<string>("ResourceOwnerId", "CollaborationBrainstormDrafts", type: "nvarchar(128)", maxLength: 128, nullable: false, defaultValue: "");
    protected override void Down(MigrationBuilder b) => b.DropColumn("ResourceOwnerId", "CollaborationBrainstormDrafts");
}
