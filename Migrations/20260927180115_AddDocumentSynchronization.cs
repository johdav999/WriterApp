using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorApp.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentSynchronization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentSyncClocks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Sequence = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentSyncClocks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DocumentSyncOperations",
                columns: table => new
                {
                    OwnerUserId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    OperationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RequestHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ResultJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentSyncOperations", x => new { x.OwnerUserId, x.OperationId });
                });

            migrationBuilder.CreateTable(
                name: "DocumentSyncRecords",
                columns: table => new
                {
                    DocumentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OwnerUserId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Sequence = table.Column<long>(type: "INTEGER", nullable: false),
                    Version = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsTrashed = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentSyncRecords", x => x.DocumentId);
                });

            migrationBuilder.InsertData(
                table: "DocumentSyncClocks",
                columns: new[] { "Id", "Sequence" },
                values: new object[] { 1, 0L });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentSyncRecords_OwnerUserId_Sequence",
                table: "DocumentSyncRecords",
                columns: new[] { "OwnerUserId", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentSyncRecords_Sequence",
                table: "DocumentSyncRecords",
                column: "Sequence",
                unique: true);
            foreach (string sql in WriterApp.Data.Documents.DocumentSyncSchemaV1.Install(false)) migrationBuilder.Sql(sql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (string sql in WriterApp.Data.Documents.DocumentSyncSchemaV1.Uninstall(false)) migrationBuilder.Sql(sql);
            migrationBuilder.DropTable(
                name: "DocumentSyncClocks");

            migrationBuilder.DropTable(
                name: "DocumentSyncOperations");

            migrationBuilder.DropTable(
                name: "DocumentSyncRecords");
        }
    }
}
