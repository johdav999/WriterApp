using System;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WriterApp.Application.Exporting;
using WriterApp.Data;
using WriterApp.Data.Documents;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class ExportPresetTests
    {
        [Fact]
        public async Task CreateUpdateDeletePreset()
        {
            await using SqliteConnection connection = new("DataSource=:memory:");
            await connection.OpenAsync();
            AppDbContext dbContext = BuildDbContext(connection);
            ExportPresetService service = new(dbContext);

            ExportPresetSettingsDto settings = BuildSettings("html");
            ExportPresetCreateRequest createRequest = new("My preset", false, settings);
            var created = await service.CreateAsync("user-1", createRequest, default);

            var list = await service.ListAsync("user-1", default);
            Assert.Single(list);

            ExportPresetUpdateRequest updateRequest = new("My preset updated", true, BuildSettings("markdown"));
            var updated = await service.UpdateAsync("user-1", created.Id, updateRequest, default);
            Assert.NotNull(updated);
            Assert.True(updated!.IsGlobalDefault);

            bool removed = await service.DeleteAsync("user-1", created.Id, default);
            Assert.True(removed);

            list = await service.ListAsync("user-1", default);
            Assert.Empty(list);
        }

        [Fact]
        public async Task ResolveDefaultPresetPrefersProjectDefault()
        {
            await using SqliteConnection connection = new("DataSource=:memory:");
            await connection.OpenAsync();
            AppDbContext dbContext = BuildDbContext(connection);
            ExportPresetService service = new(dbContext);

            DocumentRecord document = new()
            {
                Id = Guid.NewGuid(),
                OwnerUserId = "user-1",
                Title = "Draft",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            dbContext.Documents.Add(document);
            await dbContext.SaveChangesAsync();

            var globalPreset = await service.CreateAsync(
                "user-1",
                new ExportPresetCreateRequest("Global", true, BuildSettings("html")),
                default);

            var projectPreset = await service.CreateAsync(
                "user-1",
                new ExportPresetCreateRequest("Project", false, BuildSettings("markdown")),
                default);

            await service.SetProjectSettingsAsync(
                "user-1",
                document.Id,
                new ProjectExportSettingsUpdateRequest(projectPreset.Id, null),
                default);

            Guid? resolved = await service.ResolveDefaultPresetIdAsync("user-1", document.Id, default);

            Assert.Equal(projectPreset.Id, resolved);
            Assert.NotEqual(globalPreset.Id, resolved);
        }

        [Fact]
        public async Task ResolveDefaultPresetFallsBackToGlobalDefault()
        {
            await using SqliteConnection connection = new("DataSource=:memory:");
            await connection.OpenAsync();
            AppDbContext dbContext = BuildDbContext(connection);
            ExportPresetService service = new(dbContext);

            DocumentRecord document = new()
            {
                Id = Guid.NewGuid(),
                OwnerUserId = "user-1",
                Title = "Draft",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            dbContext.Documents.Add(document);
            await dbContext.SaveChangesAsync();

            var globalPreset = await service.CreateAsync(
                "user-1",
                new ExportPresetCreateRequest("Global", true, BuildSettings("html")),
                default);

            Guid? resolved = await service.ResolveDefaultPresetIdAsync("user-1", document.Id, default);

            Assert.Equal(globalPreset.Id, resolved);
        }

        private static ExportPresetSettingsDto BuildSettings(string format)
        {
            return new ExportPresetSettingsDto(
                format,
                null,
                "document",
                false,
                0,
                false,
                false,
                null,
                null,
                null,
                false,
                null,
                null,
                null,
                null,
                null,
                null);
        }

        private static AppDbContext BuildDbContext(SqliteConnection connection)
        {
            DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

            AppDbContext context = new(options);
            context.Database.EnsureCreated();
            return context;
        }
    }
}
