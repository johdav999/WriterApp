using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WriterApp.Data;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class MigrationSafetyTests
    {
        private const string BeforeUtcRefactorMigration = "20260208123000_AddOutlineMetadataAndTemplates";
        private const string BeforeDocumentProjectRelationshipMigration = "20260208150000_AddProjectGoalsProgress";
        private const string DocumentProjectRelationshipMigration = "20260208163000_AddDocumentProjectRelationship";

        [Fact]
        public async Task CleanDatabase_Migrate_CompletesAndCreatesWritingSessionsTable()
        {
            string dbPath = Path.Combine(Path.GetTempPath(), $"writerapp-migrate-clean-{Guid.NewGuid():N}.db");
            string connectionString = $"Data Source={dbPath}";

            try
            {
                await using AppDbContext context = new(BuildOptions(connectionString));
                await context.Database.MigrateAsync();

                int tableCount = await ExecuteScalarIntAsync(
                    connectionString,
                    "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='WritingSessions';");

                Assert.Equal(1, tableCount);

                int projectsCount = await ExecuteScalarIntAsync(
                    connectionString,
                    "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Projects';");
                Assert.Equal(1, projectsCount);

                int deletedUsersCount = await ExecuteScalarIntAsync(
                    connectionString,
                    "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='DeletedUserIdentities';");
                Assert.Equal(1, deletedUsersCount);

                int deletedUsersIndexCount = await ExecuteScalarIntAsync(
                    connectionString,
                    "SELECT COUNT(*) FROM sqlite_master WHERE type='index' AND name='IX_DeletedUserIdentities_DeletedAtUtc';");
                Assert.Equal(1, deletedUsersIndexCount);
            }
            finally
            {
                TryDelete(dbPath);
            }
        }

        [Fact]
        public async Task ExistingWritingSessionsData_IsPreserved_WhenMigratingForward()
        {
            string dbPath = Path.Combine(Path.GetTempPath(), $"writerapp-migrate-upgrade-{Guid.NewGuid():N}.db");
            string connectionString = $"Data Source={dbPath}";
            Guid projectId = Guid.NewGuid();
            Guid sessionId = Guid.NewGuid();
            string startedUtc = "2026-02-08T13:37:27.0000000+00:00";
            string endedUtc = "2026-02-08T14:00:00.0000000+00:00";

            try
            {
                await using (AppDbContext context = new(BuildOptions(connectionString)))
                {
                    IMigrator migrator = context.Database.GetService<IMigrator>();
                    await migrator.MigrateAsync(BeforeUtcRefactorMigration);
                }

                await using (SqliteConnection connection = new(connectionString))
                {
                    await connection.OpenAsync();

                    await ExecuteNonQueryAsync(connection,
                        """
                        INSERT INTO "Projects" (
                            "Id",
                            "OwnerUserId",
                            "Title",
                            "Subtitle",
                            "AuthorName",
                            "Language",
                            "Genre",
                            "DefaultExportSettingsJson",
                            "CreatedUtc",
                            "UpdatedUtc"
                        )
                        VALUES (
                            $projectId,
                            'migration-test-user',
                            'Migration Test Project',
                            NULL,
                            NULL,
                            NULL,
                            NULL,
                            NULL,
                            '2026-02-08T13:30:00.0000000+00:00',
                            '2026-02-08T13:30:00.0000000+00:00'
                        );
                        """,
                        ("$projectId", projectId.ToString()));

                    await ExecuteNonQueryAsync(connection,
                        """
                        CREATE TABLE IF NOT EXISTS "WritingSessions" (
                            "Id" TEXT NOT NULL CONSTRAINT "PK_WritingSessions" PRIMARY KEY,
                            "ProjectId" TEXT NOT NULL,
                            "StartedUtc" TEXT NOT NULL,
                            "EndedUtc" TEXT NULL,
                            "DurationSeconds" INTEGER NOT NULL,
                            "WordsDelta" INTEGER NOT NULL,
                            "StartWordCount" INTEGER NOT NULL,
                            "Notes" TEXT NULL,
                            CONSTRAINT "FK_WritingSessions_Projects_ProjectId"
                                FOREIGN KEY ("ProjectId")
                                REFERENCES "Projects" ("Id")
                                ON DELETE CASCADE
                        );
                        """);

                    await ExecuteNonQueryAsync(connection,
                        """
                        INSERT INTO "WritingSessions" (
                            "Id",
                            "ProjectId",
                            "StartedUtc",
                            "EndedUtc",
                            "DurationSeconds",
                            "WordsDelta",
                            "StartWordCount",
                            "Notes"
                        )
                        VALUES (
                            $sessionId,
                            $projectId,
                            $startedUtc,
                            $endedUtc,
                            1353,
                            420,
                            1024,
                            'seeded before migration'
                        );
                        """,
                        ("$sessionId", sessionId.ToString()),
                        ("$projectId", projectId.ToString()),
                        ("$startedUtc", startedUtc),
                        ("$endedUtc", endedUtc));
                }

                await using (AppDbContext context = new(BuildOptions(connectionString)))
                {
                    await context.Database.MigrateAsync();
                }

                int rowCount = await ExecuteScalarIntAsync(
                    connectionString,
                    "SELECT COUNT(*) FROM \"WritingSessions\" WHERE \"Id\" = $id;",
                    ("$id", sessionId.ToString()));
                Assert.Equal(1, rowCount);

                int projectsCount = await ExecuteScalarIntAsync(
                    connectionString,
                    "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Projects';");
                Assert.Equal(1, projectsCount);

                string? persistedStarted = await ExecuteScalarStringAsync(
                    connectionString,
                    "SELECT \"StartedUtc\" FROM \"WritingSessions\" WHERE \"Id\" = $id;",
                    ("$id", sessionId.ToString()));
                Assert.Equal(startedUtc, persistedStarted);
            }
            finally
            {
                TryDelete(dbPath);
            }
        }

        [Fact]
        public async Task AddDocumentProjectRelationship_SelfHeals_WhenProjectsTableIsMissing()
        {
            string dbPath = Path.Combine(Path.GetTempPath(), $"writerapp-migrate-projects-missing-{Guid.NewGuid():N}.db");
            string connectionString = $"Data Source={dbPath}";
            Guid documentId = Guid.NewGuid();

            try
            {
                await using (AppDbContext context = new(BuildOptions(connectionString)))
                {
                    IMigrator migrator = context.Database.GetService<IMigrator>();
                    await migrator.MigrateAsync(BeforeDocumentProjectRelationshipMigration);
                }

                await using (SqliteConnection connection = new(connectionString))
                {
                    await connection.OpenAsync();

                    await ExecuteNonQueryAsync(connection, "DROP TABLE IF EXISTS \"Projects\";");

                    await ExecuteNonQueryAsync(connection,
                        """
                        INSERT INTO "Documents" (
                            "Id",
                            "OwnerUserId",
                            "Title",
                            "CreatedAt",
                            "UpdatedAt",
                            "LanguageCode",
                            "TranslationGroupId"
                        )
                        VALUES (
                            $documentId,
                            'migration-test-user',
                            'Migrated Document',
                            '2026-02-08T13:30:00.0000000+00:00',
                            '2026-02-08T13:30:00.0000000+00:00',
                            NULL,
                            NULL
                        );
                        """,
                        ("$documentId", documentId.ToString()));
                }

                await EnsureMigrationsHistoryUpToAsync(connectionString, BeforeDocumentProjectRelationshipMigration);

                await using (AppDbContext context = new(BuildOptions(connectionString)))
                {
                    IMigrator migrator = context.Database.GetService<IMigrator>();
                    await migrator.MigrateAsync(DocumentProjectRelationshipMigration);
                }

                int projectsCount = await ExecuteScalarIntAsync(
                    connectionString,
                    "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Projects';");
                Assert.Equal(1, projectsCount);

                int projectRows = await ExecuteScalarIntAsync(
                    connectionString,
                    "SELECT COUNT(*) FROM \"Projects\";");
                Assert.True(projectRows >= 1);

                string? projectId = await ExecuteScalarStringAsync(
                    connectionString,
                    "SELECT \"ProjectId\" FROM \"Documents\" WHERE \"Id\" = $id;",
                    ("$id", documentId.ToString()));
                Assert.False(string.IsNullOrWhiteSpace(projectId));
            }
            finally
            {
                TryDelete(dbPath);
            }
        }

        private static DbContextOptions<AppDbContext> BuildOptions(string connectionString)
        {
            return new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connectionString)
                .Options;
        }

        private static async Task<int> ExecuteScalarIntAsync(
            string connectionString,
            string sql,
            params (string Name, string Value)[] parameters)
        {
            await using SqliteConnection connection = new(connectionString);
            await connection.OpenAsync();
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = sql;
            foreach ((string name, string value) in parameters)
            {
                command.Parameters.AddWithValue(name, value);
            }

            object? scalar = await command.ExecuteScalarAsync();
            return scalar is null
                ? 0
                : Convert.ToInt32(scalar, CultureInfo.InvariantCulture);
        }

        private static async Task<string?> ExecuteScalarStringAsync(
            string connectionString,
            string sql,
            params (string Name, string Value)[] parameters)
        {
            await using SqliteConnection connection = new(connectionString);
            await connection.OpenAsync();
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = sql;
            foreach ((string name, string value) in parameters)
            {
                command.Parameters.AddWithValue(name, value);
            }

            object? scalar = await command.ExecuteScalarAsync();
            return scalar?.ToString();
        }

        private static async Task ExecuteNonQueryAsync(
            SqliteConnection connection,
            string sql,
            params (string Name, string Value)[] parameters)
        {
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = sql;
            foreach ((string name, string value) in parameters)
            {
                command.Parameters.AddWithValue(name, value);
            }

            await command.ExecuteNonQueryAsync();
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
            }
        }

        private static async Task EnsureMigrationsHistoryUpToAsync(string connectionString, string targetMigrationId)
        {
            await using AppDbContext context = new(BuildOptions(connectionString));
            IReadOnlyList<string> migrations = context.Database.GetMigrations().ToList();
            List<string> applied = migrations
                .Where(id => string.CompareOrdinal(id, targetMigrationId) <= 0)
                .ToList();

            if (applied.Count == 0)
            {
                return;
            }

            await using SqliteConnection connection = new(connectionString);
            await connection.OpenAsync();

            await ExecuteNonQueryAsync(connection,
                """
                CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                    "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
                    "ProductVersion" TEXT NOT NULL
                );
                """);

            foreach (string migration in applied)
            {
                await ExecuteNonQueryAsync(connection,
                    """
                    INSERT OR IGNORE INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                    VALUES ($migrationId, $productVersion);
                    """,
                    ("$migrationId", migration),
                    ("$productVersion", "9.0.1"));
            }
        }
    }
}
