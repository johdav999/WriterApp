using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WriterApp.Data;
namespace BlazorApp.Migrations.SqlServerMigrationsDb;
[DbContext(typeof(SqlServerMigrationsDbContext)), Migration("20261020210001_CollaborationBrainstormDraftsSqlServer")]
public sealed class CollaborationBrainstormDraftsSqlServer : Migration
{
    protected override void Up(MigrationBuilder b) {
        b.CreateTable("CollaborationBrainstormDrafts", columns: t => new {
            Id = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
            DocumentId = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
            BackendKey = t.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
            AuthorId = t.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
            CreatedUnixMilliseconds = t.Column<long>(type: "bigint", nullable: false),
            PlainText = t.Column<string>(type: "nvarchar(max)", maxLength: 500000, nullable: false)
        }, constraints: t => t.PrimaryKey("PK_CollaborationBrainstormDrafts", x => x.Id));
        b.CreateIndex("IX_CollaborationBrainstormDrafts_DocumentId_BackendKey_CreatedUnixMilliseconds", "CollaborationBrainstormDrafts", new[]{"DocumentId","BackendKey","CreatedUnixMilliseconds"});
    }
    protected override void Down(MigrationBuilder b) => b.DropTable("CollaborationBrainstormDrafts");
}
