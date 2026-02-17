using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorApp.Migrations
{
    /// <inheritdoc />
    public partial class SyncSnapshotAfterWritingSessionUtcRefactor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider == "Microsoft.EntityFrameworkCore.Sqlite")
            {
                migrationBuilder.Sql(
                    """
                    -- Azure clean-db safety:
                    -- Some databases can hit this migration with partial history or a different SQLite file.
                    -- Do not assume Projects exists when bootstrapping WritingSessions for SQLite.
                    -- We create/rebuild WritingSessions without FK dependencies so clean-db startup cannot fail.
                    CREATE TABLE IF NOT EXISTS "WritingSessions" (
                        "Id" TEXT NOT NULL CONSTRAINT "PK_WritingSessions" PRIMARY KEY,
                        "ProjectId" TEXT NOT NULL,
                        "StartedUtc" TEXT NOT NULL,
                        "EndedUtc" TEXT NULL,
                        "DurationSeconds" INTEGER NOT NULL,
                        "WordsDelta" INTEGER NOT NULL,
                        "StartWordCount" INTEGER NOT NULL,
                        "Notes" TEXT NULL
                    );

                    CREATE TABLE "ef_temp_WritingSessions" (
                        "Id" TEXT NOT NULL CONSTRAINT "PK_WritingSessions" PRIMARY KEY,
                        "ProjectId" TEXT NOT NULL,
                        "StartedUtc" TEXT NOT NULL,
                        "EndedUtc" TEXT NULL,
                        "DurationSeconds" INTEGER NOT NULL,
                        "WordsDelta" INTEGER NOT NULL,
                        "StartWordCount" INTEGER NOT NULL,
                        "Notes" TEXT NULL
                    );

                    INSERT INTO "ef_temp_WritingSessions" (
                        "Id",
                        "ProjectId",
                        "StartedUtc",
                        "EndedUtc",
                        "DurationSeconds",
                        "WordsDelta",
                        "StartWordCount",
                        "Notes"
                    )
                    SELECT
                        "Id",
                        "ProjectId",
                        "StartedUtc",
                        "EndedUtc",
                        "DurationSeconds",
                        "WordsDelta",
                        "StartWordCount",
                        "Notes"
                    FROM "WritingSessions";

                    DROP TABLE "WritingSessions";
                    ALTER TABLE "ef_temp_WritingSessions" RENAME TO "WritingSessions";
                    CREATE INDEX IF NOT EXISTS "IX_WritingSessions_ProjectId_StartedUtc"
                        ON "WritingSessions" ("ProjectId", "StartedUtc");
                    """);

                return;
            }

            migrationBuilder.AlterColumn<DateTime>(
                name: "StartedUtc",
                table: "WritingSessions",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<DateTime>(
                name: "EndedUtc",
                table: "WritingSessions",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "TEXT",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider == "Microsoft.EntityFrameworkCore.Sqlite")
            {
                migrationBuilder.Sql(
                    """
                    CREATE TABLE IF NOT EXISTS "WritingSessions" (
                        "Id" TEXT NOT NULL CONSTRAINT "PK_WritingSessions" PRIMARY KEY,
                        "ProjectId" TEXT NOT NULL,
                        "StartedUtc" TEXT NOT NULL,
                        "EndedUtc" TEXT NULL,
                        "DurationSeconds" INTEGER NOT NULL,
                        "WordsDelta" INTEGER NOT NULL,
                        "StartWordCount" INTEGER NOT NULL,
                        "Notes" TEXT NULL
                    );

                    CREATE TABLE "ef_temp_WritingSessions" (
                        "Id" TEXT NOT NULL CONSTRAINT "PK_WritingSessions" PRIMARY KEY,
                        "ProjectId" TEXT NOT NULL,
                        "StartedUtc" TEXT NOT NULL,
                        "EndedUtc" TEXT NULL,
                        "DurationSeconds" INTEGER NOT NULL,
                        "WordsDelta" INTEGER NOT NULL,
                        "StartWordCount" INTEGER NOT NULL,
                        "Notes" TEXT NULL
                    );

                    INSERT INTO "ef_temp_WritingSessions" (
                        "Id",
                        "ProjectId",
                        "StartedUtc",
                        "EndedUtc",
                        "DurationSeconds",
                        "WordsDelta",
                        "StartWordCount",
                        "Notes"
                    )
                    SELECT
                        "Id",
                        "ProjectId",
                        "StartedUtc",
                        "EndedUtc",
                        "DurationSeconds",
                        "WordsDelta",
                        "StartWordCount",
                        "Notes"
                    FROM "WritingSessions";

                    DROP TABLE "WritingSessions";
                    ALTER TABLE "ef_temp_WritingSessions" RENAME TO "WritingSessions";
                    CREATE INDEX IF NOT EXISTS "IX_WritingSessions_ProjectId_StartedUtc"
                        ON "WritingSessions" ("ProjectId", "StartedUtc");
                    """);

                return;
            }

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "StartedUtc",
                table: "WritingSessions",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "EndedUtc",
                table: "WritingSessions",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldNullable: true);
        }
    }
}
