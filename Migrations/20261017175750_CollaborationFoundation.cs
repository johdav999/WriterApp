using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorApp.Migrations
{
    /// <inheritdoc />
    public partial class CollaborationFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Inline named constraints avoid EF rebuilding Documents and dropping its existing sync triggers.
            migrationBuilder.Sql("ALTER TABLE Documents ADD COLUMN CollaborationMigrationState INTEGER NOT NULL DEFAULT 0 CONSTRAINT CK_Documents_CollaborationMigrationState CHECK (CollaborationMigrationState BETWEEN 0 AND 4);");
            migrationBuilder.Sql("ALTER TABLE Documents ADD COLUMN CollaborationMode INTEGER NOT NULL DEFAULT 0 CONSTRAINT CK_Documents_CollaborationMode CHECK (CollaborationMode IN (0,1,2));");

            migrationBuilder.CreateTable(
                name: "CollaborationMigrationJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DocumentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceVersion = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    RecoverySnapshot = table.Column<byte[]>(type: "BLOB", nullable: false),
                    SourceSha256 = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CreatedAtUnixMilliseconds = table.Column<long>(type: "INTEGER", nullable: false),
                    CompletedAtUnixMilliseconds = table.Column<long>(type: "INTEGER", nullable: true),
                    FailureCode = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true)
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
                    PageId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Generation = table.Column<Guid>(type: "TEXT", nullable: false),
                    DocumentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ContentContractVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    ProtocolVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    SchemaFingerprint = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    LeaseEpoch = table.Column<long>(type: "INTEGER", nullable: false),
                    LeaseExpiresAtUnixMilliseconds = table.Column<long>(type: "INTEGER", nullable: false),
                    LeaseOwner = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    CommittedSequence = table.Column<long>(type: "INTEGER", nullable: false),
                    SnapshotSequence = table.Column<long>(type: "INTEGER", nullable: false),
                    ProjectionSequence = table.Column<long>(type: "INTEGER", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
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
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PageId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Generation = table.Column<Guid>(type: "TEXT", nullable: false),
                    CommitSequence = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAtUnixMilliseconds = table.Column<long>(type: "INTEGER", nullable: false),
                    CompletedAtUnixMilliseconds = table.Column<long>(type: "INTEGER", nullable: true),
                    Attempts = table.Column<int>(type: "INTEGER", nullable: false)
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
                    PageId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Generation = table.Column<Guid>(type: "TEXT", nullable: false),
                    InstallationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UpdateId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Sha256 = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CommitSequence = table.Column<long>(type: "INTEGER", nullable: false),
                    CommittedAtUnixMilliseconds = table.Column<long>(type: "INTEGER", nullable: false)
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
                    PageId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Generation = table.Column<Guid>(type: "TEXT", nullable: false),
                    Sequence = table.Column<long>(type: "INTEGER", nullable: false),
                    State = table.Column<byte[]>(type: "BLOB", nullable: false),
                    Sha256 = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CreatedAtUnixMilliseconds = table.Column<long>(type: "INTEGER", nullable: false)
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
                    PageId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Generation = table.Column<Guid>(type: "TEXT", nullable: false),
                    Sequence = table.Column<long>(type: "INTEGER", nullable: false),
                    InstallationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UpdateId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AuthorUserId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Sha256 = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Update = table.Column<byte[]>(type: "BLOB", nullable: false),
                    CommittedAtUnixMilliseconds = table.Column<long>(type: "INTEGER", nullable: false)
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
            foreach (string sql in WriterApp.Data.Collaboration.CollaborationWriteFenceSchema.Install(false)) migrationBuilder.Sql(sql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE TEMP TABLE CollaborationRollbackGuard (Blocked INTEGER CONSTRAINT collaboration_downgrade_blocked CHECK (Blocked=0)); INSERT INTO CollaborationRollbackGuard SELECT (SELECT COUNT(*) FROM Documents WHERE CollaborationMode<>0)+(SELECT COUNT(*) FROM CollaborationPages)+(SELECT COUNT(*) FROM CollaborationMigrationJobs); DROP TABLE CollaborationRollbackGuard;");
            foreach (string sql in WriterApp.Data.Collaboration.CollaborationWriteFenceSchema.Uninstall(false)) migrationBuilder.Sql(sql);
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

            migrationBuilder.Sql("ALTER TABLE Documents DROP COLUMN CollaborationMigrationState;");
            migrationBuilder.Sql("ALTER TABLE Documents DROP COLUMN CollaborationMode;");
        }
    }
}
