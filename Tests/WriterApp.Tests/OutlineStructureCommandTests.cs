using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WriterApp.Application.Commands;
using WriterApp.Application.Documents;
using WriterApp.Data;
using WriterApp.Data.Documents;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class OutlineStructureCommandTests
    {
        [Fact]
        public async Task UpdateSceneCardCommand_ExecuteAndUndo_RoundTripsAllFields()
        {
            await using SqliteConnection connection = new("Filename=:memory:");
            await connection.OpenAsync();
            DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

            await using AppDbContext db = new(options);
            await db.Database.EnsureCreatedAsync();

            DocumentRecord doc = new()
            {
                Id = Guid.NewGuid(),
                OwnerUserId = "user",
                Title = "Doc",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            SectionRecord section = new()
            {
                Id = Guid.NewGuid(),
                DocumentId = doc.Id,
                Title = "Section",
                OrderIndex = 0,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            db.Documents.Add(doc);
            db.Sections.Add(section);
            await db.SaveChangesAsync();

            UpdateSceneCardCommand.SceneCardState before = new();
            UpdateSceneCardCommand.SceneCardState after = new()
            {
                NarrativePurpose = "Purpose",
                EmotionalBeat = "Beat",
                KeyEvents = "Event A",
                OpenQuestions = "Why now?",
                PovCharacterId = "chr_1",
                PlaceId = "plc_1",
                TimelineEventId = "evt_1",
                TimeRef = "Day 3",
                TagsJson = "[\"reveal\"]",
                ReferencesJson = "[{\"kind\":\"character\",\"targetId\":\"chr_1\"}]"
            };

            UpdateSceneCardCommand command = new(
                "user",
                doc.Id,
                section.Id,
                JsonSerializer.Serialize(before),
                JsonSerializer.Serialize(after));

            await command.ExecuteAsync(db, CancellationToken.None);
            await db.SaveChangesAsync();

            SectionSceneCardRecord? created = await db.SectionSceneCards.FindAsync(section.Id);
            Assert.NotNull(created);
            Assert.Equal("chr_1", created!.PovCharacterId);
            Assert.Equal("plc_1", created.PlaceId);
            Assert.Equal("evt_1", created.TimelineEventId);
            Assert.Equal("Day 3", created.TimeRef);
            Assert.Equal("[\"reveal\"]", created.TagsJson);

            await command.UndoAsync(db, CancellationToken.None);
            await db.SaveChangesAsync();

            SectionSceneCardRecord? undone = await db.SectionSceneCards.FindAsync(section.Id);
            Assert.NotNull(undone);
            Assert.Equal(string.Empty, undone!.NarrativePurpose);
            Assert.Null(undone.PovCharacterId);
            Assert.Null(undone.PlaceId);
            Assert.Null(undone.TimelineEventId);
            Assert.Null(undone.TimeRef);
        }

        [Fact]
        public async Task UpdateOutlineNodeMetadataCommand_ExecuteAndUndo_RestoresMetadata()
        {
            await using SqliteConnection connection = new("Filename=:memory:");
            await connection.OpenAsync();
            DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

            await using AppDbContext db = new(options);
            await db.Database.EnsureCreatedAsync();

            DocumentRecord doc = new()
            {
                Id = Guid.NewGuid(),
                OwnerUserId = "user",
                Title = "Doc",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            DocumentOutlineNodeRecord node = new()
            {
                Id = Guid.NewGuid(),
                DocumentId = doc.Id,
                ParentId = null,
                Order = 0,
                Title = "Node",
                MetadataJson = "{\"purpose\":\"old\"}"
            };
            db.Documents.Add(doc);
            db.DocumentOutlineNodes.Add(node);
            await db.SaveChangesAsync();

            UpdateOutlineNodeMetadataCommand command = new(
                "user",
                doc.Id,
                node.Id,
                node.MetadataJson,
                "{\"purpose\":\"new\"}");

            await command.ExecuteAsync(db, CancellationToken.None);
            await db.SaveChangesAsync();
            Assert.Equal("{\"purpose\":\"new\"}", (await db.DocumentOutlineNodes.FindAsync(node.Id))!.MetadataJson);

            await command.UndoAsync(db, CancellationToken.None);
            await db.SaveChangesAsync();
            Assert.Equal("{\"purpose\":\"old\"}", (await db.DocumentOutlineNodes.FindAsync(node.Id))!.MetadataJson);
        }

        [Fact]
        public async Task ApplyOutlineTemplateCommand_ExecuteAndUndo_RemovesCreatedNodesAndSections()
        {
            await using SqliteConnection connection = new("Filename=:memory:");
            await connection.OpenAsync();
            DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

            await using AppDbContext db = new(options);
            await db.Database.EnsureCreatedAsync();

            DocumentRecord doc = new()
            {
                Id = Guid.NewGuid(),
                OwnerUserId = "user",
                Title = "Doc",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            db.Documents.Add(doc);
            await db.SaveChangesAsync();

            SectionRecord section = new()
            {
                Id = Guid.NewGuid(),
                DocumentId = doc.Id,
                Title = "Scene 1",
                OrderIndex = 0,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            PageRecord page = new()
            {
                Id = Guid.NewGuid(),
                DocumentId = doc.Id,
                SectionId = section.Id,
                Title = "Page 1",
                Content = string.Empty,
                OrderIndex = 0,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            DocumentOutlineNodeRecord node = new()
            {
                Id = Guid.NewGuid(),
                DocumentId = doc.Id,
                ParentId = null,
                Order = 0,
                Title = "Scene 1",
                LinkedSectionId = section.Id,
                MetadataJson = "{\"purpose\":\"test\"}"
            };

            ApplyOutlineTemplateCommand command = new(
                "user",
                doc.Id,
                new List<DocumentOutlineNodeRecord> { node },
                new List<SectionRecord> { section },
                new List<PageRecord> { page });

            await command.ExecuteAsync(db, CancellationToken.None);
            await db.SaveChangesAsync();

            Assert.Equal(1, await db.Sections.CountAsync());
            Assert.Equal(1, await db.Pages.CountAsync());
            Assert.Equal(1, await db.DocumentOutlineNodes.CountAsync());

            await command.UndoAsync(db, CancellationToken.None);
            await db.SaveChangesAsync();

            Assert.Equal(0, await db.Sections.CountAsync());
            Assert.Equal(0, await db.Pages.CountAsync());
            Assert.Equal(0, await db.DocumentOutlineNodes.CountAsync());
        }

        [Fact]
        public void SectionSceneCardDto_SerializesNewFields()
        {
            SectionSceneCardDto dto = new(
                Guid.NewGuid(),
                "Purpose",
                "Beat",
                "Events",
                "Open",
                DateTimeOffset.UtcNow,
                "chr_1",
                "plc_1",
                "evt_1",
                "Day 3",
                new[] { "reveal", "fight" },
                new[] { new SceneCardReferenceDto("character", "chr_1", "Track arc") });

            string json = JsonSerializer.Serialize(dto);
            Assert.Contains("\"povCharacterId\":\"chr_1\"", json, StringComparison.Ordinal);
            Assert.Contains("\"placeId\":\"plc_1\"", json, StringComparison.Ordinal);
            Assert.Contains("\"timelineEventId\":\"evt_1\"", json, StringComparison.Ordinal);
            Assert.Contains("\"tags\":[\"reveal\",\"fight\"]", json, StringComparison.Ordinal);
        }

        [Fact]
        public async Task StructureCommandProcessor_UndoRedo_WorksForOutlineMetadata()
        {
            await using SqliteConnection connection = new("Filename=:memory:");
            await connection.OpenAsync();

            ServiceCollection services = new();
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(connection));
            services.AddSingleton<IStructureCommandProcessor, StructureCommandProcessor>();
            ServiceProvider provider = services.BuildServiceProvider();

            await using (AsyncServiceScope seedScope = provider.CreateAsyncScope())
            {
                AppDbContext db = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
                await db.Database.EnsureCreatedAsync();
                DocumentRecord doc = new()
                {
                    Id = Guid.NewGuid(),
                    OwnerUserId = "user",
                    Title = "Doc",
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
                DocumentOutlineNodeRecord node = new()
                {
                    Id = Guid.NewGuid(),
                    DocumentId = doc.Id,
                    ParentId = null,
                    Order = 0,
                    Title = "Node",
                    MetadataJson = "{\"purpose\":\"old\"}"
                };
                db.Documents.Add(doc);
                db.DocumentOutlineNodes.Add(node);
                await db.SaveChangesAsync();
            }

            Guid docId;
            Guid nodeId;
            await using (AsyncServiceScope readScope = provider.CreateAsyncScope())
            {
                AppDbContext db = readScope.ServiceProvider.GetRequiredService<AppDbContext>();
                DocumentRecord doc = await db.Documents.FirstAsync();
                DocumentOutlineNodeRecord node = await db.DocumentOutlineNodes.FirstAsync();
                docId = doc.Id;
                nodeId = node.Id;
            }

            IStructureCommandProcessor processor = provider.GetRequiredService<IStructureCommandProcessor>();
            await processor.ExecuteAsync(
                new UpdateOutlineNodeMetadataCommand(
                    "user",
                    docId,
                    nodeId,
                    "{\"purpose\":\"old\"}",
                    "{\"purpose\":\"new\"}"),
                CancellationToken.None);

            await using (AsyncServiceScope checkScope = provider.CreateAsyncScope())
            {
                AppDbContext db = checkScope.ServiceProvider.GetRequiredService<AppDbContext>();
                Assert.Equal("{\"purpose\":\"new\"}", (await db.DocumentOutlineNodes.FirstAsync()).MetadataJson);
            }

            Assert.True(await processor.UndoAsync("user", docId, CancellationToken.None));
            await using (AsyncServiceScope checkScope = provider.CreateAsyncScope())
            {
                AppDbContext db = checkScope.ServiceProvider.GetRequiredService<AppDbContext>();
                Assert.Equal("{\"purpose\":\"old\"}", (await db.DocumentOutlineNodes.FirstAsync()).MetadataJson);
            }

            Assert.True(await processor.RedoAsync("user", docId, CancellationToken.None));
            await using (AsyncServiceScope checkScope = provider.CreateAsyncScope())
            {
                AppDbContext db = checkScope.ServiceProvider.GetRequiredService<AppDbContext>();
                Assert.Equal("{\"purpose\":\"new\"}", (await db.DocumentOutlineNodes.FirstAsync()).MetadataJson);
            }
        }
    }
}
