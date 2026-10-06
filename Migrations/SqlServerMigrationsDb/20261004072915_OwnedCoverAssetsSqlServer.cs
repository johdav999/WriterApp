using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorApp.Migrations.SqlServerMigrationsDb
{
    /// <inheritdoc />
    public partial class OwnedCoverAssetsSqlServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CoverAssets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    OwnerUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceMetadataRevision = table.Column<long>(type: "bigint", nullable: false),
                    SourceDocumentVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ReferenceHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RemoteReference = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: false),
                    ContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    MediaType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Bytes = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
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
