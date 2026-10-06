using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WriterApp.Application.Documents;
using WriterApp.Application.State;
using WriterApp.Data;
using WriterApp.Data.Documents;
using Xunit;

namespace WriterApp.Tests;

public sealed class QualityCacheRevisionTests
{
    [Fact]
    public async Task CorrectedRuleOffsetsInvalidateLegacyCacheThenReuseCurrentResults()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite("Filename=:memory:").Options);
        await db.Database.OpenConnectionAsync(); await db.Database.EnsureCreatedAsync();
        var now = DateTimeOffset.UtcNow; var project = Guid.NewGuid(); var document = Guid.NewGuid(); var section = Guid.NewGuid();
        db.Projects.Add(new ProjectRecord { Id = project, OwnerUserId = "writer", Title = "Book", CreatedUtc = now, UpdatedUtc = now });
        db.Documents.Add(new DocumentRecord { Id = document, ProjectId = project, OwnerUserId = "writer", Title = "Draft", CreatedAt = now, UpdatedAt = now });
        db.Sections.Add(new SectionRecord { Id = section, DocumentId = document, Title = "Scene", CreatedAt = now, UpdatedAt = now });
        var page = new PageRecord { Id = Guid.NewGuid(), DocumentId = document, SectionId = section, Title = "Page", Content = "<p>First scene.</p><p>The door was opened by Anna.</p>", CreatedAt = now, UpdatedAt = now };
        db.Pages.Add(page); await db.SaveChangesAsync();
        string plain = PlainTextMapper.ToPlainText(page.Content);
        string legacyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plain))).ToLowerInvariant();
        db.PageQualityIssues.Add(new PageQualityIssueRecord { Id = Guid.NewGuid(), DocumentId = document, PageId = page.Id, Scope = "page",
            ContentHash = legacyHash, IssueKey = "legacy", RuleId = "style.passive_voice", Kind = "passive-voice", Severity = "info", Message = "Legacy wrong offsets", AnchorText = "was opened", StartOffset = 0, EndOffset = 10, CreatedAt = now });
        await db.SaveChangesAsync();
        var service = new QualityCheckService(db, NullLogger<QualityCheckService>.Instance);
        var computed = await service.RunChecksAsync("writer", page, new("page", null, false), default);
        Assert.False(computed.FromCache); Assert.NotEqual(legacyHash, computed.ContentHash);
        var issue = Assert.Single(computed.Issues, i => i.RuleId == "style.passive_voice");
        Assert.Equal("was opened", plain[issue.StartOffset..issue.EndOffset]);
        var cached = await service.RunChecksAsync("writer", page, new("page", null, false), default);
        Assert.True(cached.FromCache); Assert.Equal(computed.ContentHash, cached.ContentHash);
        Assert.Equal(computed.Issues.Select(i => i.IssueKey), cached.Issues.Select(i => i.IssueKey));
        Assert.DoesNotContain(await db.PageQualityIssues.ToListAsync(), i => i.IssueKey == "legacy");
    }
}
