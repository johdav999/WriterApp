using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WriterApp.Application.Documents;
using WriterApp.Data;
using WriterApp.Data.Documents;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class ProjectWordCountServiceTests
    {
        [Fact]
        public async Task RefreshProject_ComputesSceneAndAggregateCaches()
        {
            await using SqliteConnection connection = new("Filename=:memory:");
            await connection.OpenAsync();

            DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

            await using AppDbContext db = new(options);
            await db.Database.EnsureCreatedAsync();

            string userId = "user-1";
            DocumentRecord document = new()
            {
                Id = Guid.NewGuid(),
                OwnerUserId = userId,
                Title = "Doc",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            db.Documents.Add(document);

            SectionRecord section = new()
            {
                Id = Guid.NewGuid(),
                DocumentId = document.Id,
                Title = "Scene 1",
                OrderIndex = 0,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            db.Sections.Add(section);

            db.Pages.Add(new PageRecord
            {
                Id = Guid.NewGuid(),
                DocumentId = document.Id,
                SectionId = section.Id,
                Title = "Page 1",
                Content = "<p>One two three four.</p>",
                OrderIndex = 0,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

            ProjectRecord project = new()
            {
                Id = Guid.NewGuid(),
                OwnerUserId = userId,
                Title = "Project",
                CreatedUtc = DateTimeOffset.UtcNow,
                UpdatedUtc = DateTimeOffset.UtcNow
            };
            db.Projects.Add(project);

            ProjectNodeRecord chapter = new()
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                ParentId = null,
                NodeType = ProjectNodeType.Chapter,
                Title = "Chapter 1",
                OrderIndex = 0,
                UpdatedUtc = DateTimeOffset.UtcNow
            };
            ProjectNodeRecord scene = new()
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                ParentId = chapter.Id,
                NodeType = ProjectNodeType.Scene,
                Title = "Scene 1",
                LinkedSectionId = section.Id,
                OrderIndex = 0,
                UpdatedUtc = DateTimeOffset.UtcNow
            };
            db.ProjectNodes.Add(chapter);
            db.ProjectNodes.Add(scene);
            await db.SaveChangesAsync();

            ProjectWordCountService service = new(db);
            await service.RefreshProjectAsync(project.Id, CancellationToken.None);

            ProjectNodeRecord refreshedScene = await db.ProjectNodes.FirstAsync(node => node.Id == scene.Id);
            ProjectNodeRecord refreshedChapter = await db.ProjectNodes.FirstAsync(node => node.Id == chapter.Id);

            Assert.Equal(4, refreshedScene.WordCountCache);
            Assert.Equal(4, refreshedChapter.WordCountCache);
        }

        [Fact]
        public async Task RefreshForSection_UpdatesAffectedProjects()
        {
            await using SqliteConnection connection = new("Filename=:memory:");
            await connection.OpenAsync();

            DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

            await using AppDbContext db = new(options);
            await db.Database.EnsureCreatedAsync();

            string userId = "user-2";
            DocumentRecord document = new()
            {
                Id = Guid.NewGuid(),
                OwnerUserId = userId,
                Title = "Doc",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            db.Documents.Add(document);

            SectionRecord section = new()
            {
                Id = Guid.NewGuid(),
                DocumentId = document.Id,
                Title = "Scene",
                OrderIndex = 0,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            db.Sections.Add(section);

            PageRecord page = new()
            {
                Id = Guid.NewGuid(),
                DocumentId = document.Id,
                SectionId = section.Id,
                Title = "Page 1",
                Content = "<p>Alpha beta.</p>",
                OrderIndex = 0,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            db.Pages.Add(page);

            ProjectRecord project = new()
            {
                Id = Guid.NewGuid(),
                OwnerUserId = userId,
                Title = "Project",
                CreatedUtc = DateTimeOffset.UtcNow,
                UpdatedUtc = DateTimeOffset.UtcNow
            };
            db.Projects.Add(project);

            ProjectNodeRecord scene = new()
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                ParentId = null,
                NodeType = ProjectNodeType.Scene,
                Title = "Scene",
                LinkedSectionId = section.Id,
                OrderIndex = 0,
                UpdatedUtc = DateTimeOffset.UtcNow
            };
            db.ProjectNodes.Add(scene);
            await db.SaveChangesAsync();

            ProjectWordCountService service = new(db);
            await service.RefreshProjectAsync(project.Id, CancellationToken.None);
            Assert.Equal(2, (await db.ProjectNodes.FirstAsync(node => node.Id == scene.Id)).WordCountCache);

            page.Content = "<p>Alpha beta gamma delta.</p>";
            await db.SaveChangesAsync();

            await service.RefreshForSectionAsync(section.Id, CancellationToken.None);
            Assert.Equal(4, (await db.ProjectNodes.FirstAsync(node => node.Id == scene.Id)).WordCountCache);
        }
    }
}
