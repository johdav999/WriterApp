using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WriterApp.Data;

namespace WriterApp.Application.Collaboration;

public sealed record CollaborationSupportingPage(Guid Id, Guid SectionId, string Title, int Order, string Content);
public sealed record CollaborationSupportingSnapshot(Guid DocumentId, string Title, string SourceSha256, CollaborationSupportingPage[] Pages, string Text, JsonElement ExtraContent);
public static class CollaborationSupportingReader
{
    public static string PlainText(string content)
    {
        if (!content.TrimStart().StartsWith('{')) return WriterApp.Application.State.PlainTextMapper.ToPlainText(content);
        try
        {
            using var doc = JsonDocument.Parse(content, new JsonDocumentOptions { MaxDepth = 128 });
            var text = new System.Text.StringBuilder(); int count = 0;
            void Visit(JsonElement element)
            {
                if (++count > 100000) throw new InvalidDataException();
                if (element.TryGetProperty("text", out var value) && value.ValueKind == JsonValueKind.String) text.Append(value.GetString());
                if (element.TryGetProperty("content", out var children) && children.ValueKind == JsonValueKind.Array) foreach (var child in children.EnumerateArray()) Visit(child);
                if (element.TryGetProperty("type", out var type) && type.GetString() is "paragraph" or "heading" or "hardBreak") text.AppendLine();
            }
            Visit(doc.RootElement); return text.ToString();
        }
        catch { throw new CollaborationCommitException("collaboration_supporting_format_unsupported"); }
    }
    public static Task<CollaborationSupportingSnapshot> Read(AppDbContext db, CollaborationAccessService access, string actor, Guid manuscript,
        Guid resource, CollaborationOperation operation, CancellationToken ct) => db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
            await access.RequireSupporting(actor, manuscript, resource, operation, ct);
            var doc = await db.Documents.AsNoTracking().SingleAsync(d => d.Id == resource, ct);
            if (await db.Pages.Where(p => p.DocumentId == resource).SumAsync(p => (long)p.Content.Length, ct) > 2 * 1024 * 1024) throw new CollaborationCommitException("collaboration_supporting_limit");
            var pages = await db.Pages.AsNoTracking().Where(p => p.DocumentId == resource).OrderBy(p => p.SectionId).ThenBy(p => p.OrderIndex).ThenBy(p => p.Id)
                .Select(p => new CollaborationSupportingPage(p.Id, p.SectionId, p.Title, p.OrderIndex, p.Content)).Take(1001).ToArrayAsync(ct);
            if (pages.Length > 1000) throw new CollaborationCommitException("collaboration_supporting_limit");
            var synopsis = await db.DocumentSynopses.AsNoTracking().Where(s => s.DocumentId == resource).Select(s => new
                { s.Logline, s.Premise, s.Theme, s.ProtagonistArc, s.CentralConflict, s.Stakes, s.Setting, s.EndingIntent, s.OpenQuestions, s.Notes }).SingleOrDefaultAsync(ct);
            var outline = await db.DocumentOutlines.AsNoTracking().Where(o => o.DocumentId == resource).Select(o => o.Outline).SingleOrDefaultAsync(ct);
            var nodes = await db.DocumentOutlineNodes.AsNoTracking().Where(n => n.DocumentId == resource).OrderBy(n => n.Order).ThenBy(n => n.Id)
                .Select(n => new { n.Id, n.ParentId, n.Order, n.Title, n.Notes, n.MetadataJson }).Take(1001).ToArrayAsync(ct);
            if (nodes.Length > 1000) throw new CollaborationCommitException("collaboration_supporting_limit");
            var extra = JsonSerializer.SerializeToElement(new { synopsis, outline, nodes });
            var source = JsonSerializer.SerializeToUtf8Bytes(new { doc.Title, pages, extra });
            if (source.Length > 2 * 1024 * 1024) throw new CollaborationCommitException("collaboration_supporting_limit");
            string text = string.Join("\n", pages.Select(p => PlainText(p.Content))) + "\n" + PlainText(outline ?? "")
                + "\n" + string.Join("\n", nodes.Select(n => n.Title + "\n" + n.Notes));
            if (synopsis is not null) text += "\n" + string.Join("\n", JsonSerializer.SerializeToElement(synopsis).EnumerateObject().Select(p => p.Name + ": " + p.Value.GetString()));
            var result = new CollaborationSupportingSnapshot(resource, doc.Title, CollaborationMigrationService.Hash(source), pages, text, extra);
            if (tx is not null) await tx.CommitAsync(ct); return result;
        });
}
