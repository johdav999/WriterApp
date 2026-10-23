using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WriterApp.Data;
namespace BlazorApp.Migrations;
[DbContext(typeof(AppDbContext)), Migration("20261020211500_CollaborationBrainstormOwnerBinding")]
public sealed class CollaborationBrainstormOwnerBinding : Migration
{
    protected override void Up(MigrationBuilder b) => b.AddColumn<string>("ResourceOwnerId", "CollaborationBrainstormDrafts", type: "TEXT", maxLength: 128, nullable: false, defaultValue: "");
    // No keys or indexes depend on this column. The bundled SQLite supports
    // native DROP COLUMN; no model-dependent table rebuild is needed.
    protected override void Down(MigrationBuilder b) => b.Sql("ALTER TABLE CollaborationBrainstormDrafts DROP COLUMN ResourceOwnerId;");
}
