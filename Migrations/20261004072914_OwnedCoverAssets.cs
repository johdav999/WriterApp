using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorApp.Migrations
{
    /// <inheritdoc />
    public partial class OwnedCoverAssets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CoverAssets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    OwnerUserId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceDocumentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceMetadataRevision = table.Column<long>(type: "INTEGER", nullable: false),
                    SourceDocumentVersion = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ReferenceHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    RemoteReference = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: false),
                    ContentHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    MediaType = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Bytes = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoverAssets", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CoverAssets_OwnerUserId_ProjectId_ReferenceHash",
                table: "CoverAssets",
                columns: new[] { "OwnerUserId", "ProjectId", "ReferenceHash" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CoverAssets");
        }
    }
}
