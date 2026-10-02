using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using Xunit;

namespace WriterApp.Tests;

public sealed class LocalAnnotationMarkupTests
{
    private static LocalDocument Book(params string[] contents)
    {
        var now = DateTimeOffset.UtcNow;
        var pages = contents.Select((content, index) => new LocalPage { PageId = Guid.NewGuid(), Title = "Page", OrderIndex = index,
            CreatedAtUtc = now, UpdatedAtUtc = now, ContentFormat = LocalContentFormat.Html, Content = content }).ToArray();
        var document = new LocalDocument { DocumentId = Guid.NewGuid(), Title = "Book", CreatedAtUtc = now, UpdatedAtUtc = now,
            LocalRevision = 1, Sections = [new LocalSection { SectionId = Guid.NewGuid(), Title = "Scene", OrderIndex = 0,
                CreatedAtUtc = now, UpdatedAtUtc = now, Pages = pages }] };
        return LocalProjectStructure.Attach(document, "Book");
    }

    [Fact]
    public void QuoteMarkupUsesTheRightPageAndPreservesAnnotationIdentityAndStatus()
    {
        var document = Book("<p>First page</p>", "<p>A <strong>café 日本語 😀</strong> passage.</p>");
        var scene = document.Project!.Nodes.Single(n => n.NodeType == "scene");
        document = LocalPlanning.AddAnnotation(document, scene.NodeId, "todo", "Review wording", "café 日本語 😀");
        var id = document.Project!.Nodes.Single(n => n.NodeId == scene.NodeId).Annotations.Single().LocalId;
        document = LocalPlanning.Resolve(document, scene.NodeId, id, true);
        var pages = document.Sections[0].Pages;

        Assert.Empty(LocalAnnotationMarkup.ForPage(document, pages[0].PageId));
        var markup = Assert.Single(LocalAnnotationMarkup.ForPage(document, pages[1].PageId));
        Assert.Equal(new LocalTextAnnotation(id, "café 日本語 😀", "todo", "resolved", "Review wording"), markup);
        Assert.Equal(pages[1].PageId, LocalAnnotationMarkup.PageFor(document, id));
        Assert.Equal("<p>A <strong>café 日本語 😀</strong> passage.</p>", pages[1].Content);
    }

    [Theory]
    [InlineData("<p>First<br>Second</p><p>Third</p>", "First\nSecond\nThird")]
    [InlineData("<ul><li><p>List first</p></li><li><p>List second</p></li></ul>", "List first\n\nList second")]
    public void MultilineQuotesMatchTheStoredBlockSeparators(string content, string quote)
    {
        var document = Book(content);
        var scene = document.Project!.Nodes.Single(n => n.NodeType == "scene");
        document = LocalPlanning.AddAnnotation(document, scene.NodeId, "comment", "Review passage", quote);
        Assert.Equal(quote, Assert.Single(LocalAnnotationMarkup.ForPage(document, document.Sections[0].Pages[0].PageId)).Quote);
    }

    [Theory]
    [InlineData("<p>Anchor Anchor</p>", "<p>Other</p>", "Anchor")]
    [InlineData("<p>Anchor</p>", "<p>Anchor</p>", "Anchor")]
    [InlineData("<p>Writing</p>", "<p>Other</p>", "Missing")]
    [InlineData("<p>Writing</p>", "<p>Other</p>", "")]
    public void MissingAmbiguousAndSceneLevelAnnotationsDoNotInventMarkup(string first, string second, string quote)
    {
        var document = Book(first, second);
        var scene = document.Project!.Nodes.Single(n => n.NodeType == "scene");
        document = LocalPlanning.AddAnnotation(document, scene.NodeId, "comment", "Comment", quote);
        var id = document.Project!.Nodes.Single(n => n.NodeId == scene.NodeId).Annotations.Single().LocalId;
        Assert.All(document.Sections[0].Pages, page => Assert.Empty(LocalAnnotationMarkup.ForPage(document, page.PageId)));
        Assert.Null(LocalAnnotationMarkup.PageFor(document, id));
    }

    [Fact]
    public void DetachedAndDeletedSceneAnnotationsStayUnmarkedEvenIfTheirQuoteExists()
    {
        var document = Book("<p>Anchor</p>");
        var scene = document.Project!.Nodes.Single(n => n.NodeType == "scene");
        document = LocalPlanning.AddAnnotation(document, scene.NodeId, "comment", "Comment", "Anchor");
        var annotated = document.Project!.Nodes.Single(n => n.NodeId == scene.NodeId);
        var annotation = annotated.Annotations.Single();
        foreach (var node in new[] {
            annotated with { DeletionId = Guid.NewGuid() },
            annotated with { Annotations = [annotation with { Value = annotation.Value with { AnchorDetached = true } }] }
        })
        {
            var updated = document with { Project = document.Project with { Nodes = document.Project.Nodes.Select(n => n.NodeId == node.NodeId ? node : n).ToArray() } };
            Assert.Empty(LocalAnnotationMarkup.ForPage(updated, document.Sections[0].Pages[0].PageId));
            Assert.Null(LocalAnnotationMarkup.PageFor(updated, annotation.LocalId));
        }
    }
}
