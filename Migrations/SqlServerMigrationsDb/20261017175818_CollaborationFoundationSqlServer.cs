using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorApp.Migrations.SqlServerMigrationsDb
{
    /// <inheritdoc />
    public partial class CollaborationFoundationSqlServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CollaborationMigrationState",
                table: "Documents",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CollaborationMode",
                table: "Documents",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "CollaborationMigrationJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceVersion = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RecoverySnapshot = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    SourceSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAtUnixMilliseconds = table.Column<long>(type: "bigint", nullable: false),
                    CompletedAtUnixMilliseconds = table.Column<long>(type: "bigint", nullable: true),
                    FailureCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollaborationMigrationJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CollaborationMigrationJobs_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CollaborationPages",
                columns: table => new
                {
                    PageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Generation = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContentContractVersion = table.Column<int>(type: "int", nullable: false),
                    ProtocolVersion = table.Column<int>(type: "int", nullable: false),
                    SchemaFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    LeaseEpoch = table.Column<long>(type: "bigint", nullable: false),
                    LeaseExpiresAtUnixMilliseconds = table.Column<long>(type: "bigint", nullable: false),
                    LeaseOwner = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CommittedSequence = table.Column<long>(type: "bigint", nullable: false),
                    SnapshotSequence = table.Column<long>(type: "bigint", nullable: false),
                    ProjectionSequence = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollaborationPages", x => new { x.PageId, x.Generation });
                    table.CheckConstraint("CK_CollaborationPages_Sequences", "CommittedSequence >= 0 AND SnapshotSequence >= 0 AND ProjectionSequence >= 0 AND SnapshotSequence <= CommittedSequence AND ProjectionSequence <= CommittedSequence AND LeaseEpoch >= 0");
                    table.ForeignKey(
                        name: "FK_CollaborationPages_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CollaborationPages_Pages_PageId",
                        column: x => x.PageId,
                        principalTable: "Pages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CollaborationOutboxEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Generation = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommitSequence = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUnixMilliseconds = table.Column<long>(type: "bigint", nullable: false),
                    CompletedAtUnixMilliseconds = table.Column<long>(type: "bigint", nullable: true),
                    Attempts = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollaborationOutboxEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CollaborationOutboxEvents_CollaborationPages_PageId_Generation",
                        columns: x => new { x.PageId, x.Generation },
                        principalTable: "CollaborationPages",
                        principalColumns: new[] { "PageId", "Generation" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CollaborationReceipts",
                columns: table => new
                {
                    PageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Generation = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstallationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CommitSequence = table.Column<long>(type: "bigint", nullable: false),
                    CommittedAtUnixMilliseconds = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollaborationReceipts", x => new { x.PageId, x.Generation, x.InstallationId, x.UpdateId });
                    table.CheckConstraint("CK_CollaborationReceipts_Sequence", "CommitSequence > 0");
                    table.ForeignKey(
                        name: "FK_CollaborationReceipts_CollaborationPages_PageId_Generation",
                        columns: x => new { x.PageId, x.Generation },
                        principalTable: "CollaborationPages",
                        principalColumns: new[] { "PageId", "Generation" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CollaborationSnapshots",
                columns: table => new
                {
                    PageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Generation = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    State = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAtUnixMilliseconds = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollaborationSnapshots", x => new { x.PageId, x.Generation, x.Sequence });
                    table.CheckConstraint("CK_CollaborationSnapshots_Sequence", "Sequence >= 0");
                    table.ForeignKey(
                        name: "FK_CollaborationSnapshots_CollaborationPages_PageId_Generation",
                        columns: x => new { x.PageId, x.Generation },
                        principalTable: "CollaborationPages",
                        principalColumns: new[] { "PageId", "Generation" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CollaborationUpdates",
                columns: table => new
                {
                    PageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Generation = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    InstallationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Update = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    CommittedAtUnixMilliseconds = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollaborationUpdates", x => new { x.PageId, x.Generation, x.Sequence });
                    table.CheckConstraint("CK_CollaborationUpdates_Sequence", "Sequence > 0");
                    table.ForeignKey(
                        name: "FK_CollaborationUpdates_CollaborationPages_PageId_Generation",
                        columns: x => new { x.PageId, x.Generation },
                        principalTable: "CollaborationPages",
                        principalColumns: new[] { "PageId", "Generation" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Documents_CollaborationMigrationState",
                table: "Documents",
                sql: "CollaborationMigrationState BETWEEN 0 AND 4");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Documents_CollaborationMode",
                table: "Documents",
                sql: "CollaborationMode IN (0,1,2)");

            migrationBuilder.CreateIndex(
                name: "IX_CollaborationMigrationJobs_DocumentId",
                table: "CollaborationMigrationJobs",
                column: "DocumentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CollaborationOutboxEvents_CompletedAtUnixMilliseconds",
                table: "CollaborationOutboxEvents",
                column: "CompletedAtUnixMilliseconds");

            migrationBuilder.CreateIndex(
                name: "IX_CollaborationOutboxEvents_PageId_Generation_CommitSequence",
                table: "CollaborationOutboxEvents",
                columns: new[] { "PageId", "Generation", "CommitSequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CollaborationPages_DocumentId_PageId_Generation",
                table: "CollaborationPages",
                columns: new[] { "DocumentId", "PageId", "Generation" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CollaborationReceipts_PageId_Generation_CommitSequence",
                table: "CollaborationReceipts",
                columns: new[] { "PageId", "Generation", "CommitSequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CollaborationUpdates_PageId_Generation_InstallationId_UpdateId",
                table: "CollaborationUpdates",
                columns: new[] { "PageId", "Generation", "InstallationId", "UpdateId" },
                unique: true);
            foreach (string sql in WriterApp.Data.Collaboration.CollaborationWriteFenceSchema.Install(true)) migrationBuilder.Sql(sql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS(SELECT 1 FROM Documents WHERE CollaborationMode<>0) OR EXISTS(SELECT 1 FROM CollaborationPages) OR EXISTS(SELECT 1 FROM CollaborationMigrationJobs) THROW 51072, 'collaboration_downgrade_blocked', 1;");
            foreach (string sql in WriterApp.Data.Collaboration.CollaborationWriteFenceSchema.Uninstall(true)) migrationBuilder.Sql(sql);
            migrationBuilder.DropTable(
                name: "CollaborationMigrationJobs");

            migrationBuilder.DropTable(
                name: "CollaborationOutboxEvents");

            migrationBuilder.DropTable(
                name: "CollaborationReceipts");

            migrationBuilder.DropTable(
                name: "CollaborationSnapshots");

            migrationBuilder.DropTable(
                name: "CollaborationUpdates");

            migrationBuilder.DropTable(
                name: "CollaborationPages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Documents_CollaborationMigrationState",
                table: "Documents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Documents_CollaborationMode",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "CollaborationMigrationState",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "CollaborationMode",
                table: "Documents");
        }
    }
}
