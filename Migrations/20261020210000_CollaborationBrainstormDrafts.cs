using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WriterApp.Data;
namespace BlazorApp.Migrations;
[DbContext(typeof(AppDbContext)), Migration("20261020210000_CollaborationBrainstormDrafts")]
public sealed class CollaborationBrainstormDrafts : Migration
{
    protected override void Up(MigrationBuilder b) {
        b.CreateTable("CollaborationBrainstormDrafts", columns: t => new {
            Id = t.Column<Guid>(type: "TEXT", nullable: false),
            DocumentId = t.Column<Guid>(type: "TEXT", nullable: false),
            BackendKey = t.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
            AuthorId = t.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
            CreatedUnixMilliseconds = t.Column<long>(type: "INTEGER", nullable: false),
            PlainText = t.Column<string>(type: "TEXT", maxLength: 500000, nullable: false)
        }, constraints: t => t.PrimaryKey("PK_CollaborationBrainstormDrafts", x => x.Id));
        b.CreateIndex("IX_CollaborationBrainstormDrafts_DocumentId_BackendKey_CreatedUnixMilliseconds", "CollaborationBrainstormDrafts", new[]{"DocumentId","BackendKey","CreatedUnixMilliseconds"});
    }
    protected override void Down(MigrationBuilder b) => b.DropTable("CollaborationBrainstormDrafts");
}
