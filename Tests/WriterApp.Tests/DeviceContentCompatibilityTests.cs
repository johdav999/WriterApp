using System.Text;
using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using Xunit;

namespace WriterApp.Tests;

public sealed class DeviceContentCompatibilityTests
{
    [Fact]
    public async Task RejectedEditorCallbackRemainsRetryableDuringFlush()
    {
        int attempts = 0;
        var editor = new WriterApp.Device.Shared.Components.DeviceTextEditor();
        var callback = Microsoft.AspNetCore.Components.EventCallback.Factory.Create<string>(new object(), (string _) =>
        {
            if (++attempts == 1) throw new InvalidDataException("Rejected content");
        });
        Microsoft.AspNetCore.Components.ParameterView.FromDictionary(new Dictionary<string, object?>
            { ["ContentChanged"] = callback }).SetParameterProperties(editor);
        await Assert.ThrowsAsync<InvalidDataException>(() => editor.OnContentChanged("<p>Retry me</p>", 1));
        await editor.OnContentChanged("<p>Retry me</p>", 1);
        await editor.OnContentChanged("<p>Retry me</p>", 1);
        Assert.Equal(2, attempts);
    }
    public static TheoryData<string> Supported => new()
    {
        "<p>Räksmörgås 日本語 café 😀</p>",
        "<h1>One</h1><h2>Two</h2><h3>Three</h3><h4>Four</h4><h5>Five</h5><h6>Six</h6>",
        "<ol start=\"7\"><li><p>Seven</p><ul><li><p>Nested</p></li></ul></li></ol>",
        "<p><strong>Bold</strong><em>Italic</em><u>Underline</u><s>Strike</s><code>Code</code><br>Line</p><hr>",
        "<blockquote><p>Quote</p></blockquote><pre><code class=\"language-csharp\">a &lt; b</code></pre>",
        "<p><a href=\"https://example.com\" target=\"_blank\" rel=\"noopener noreferrer nofollow\" class=\"reference\">Safe</a></p>",
        "<table><tbody><tr><td colspan=\"2\" rowspan=\"1\" colwidth=\"100,100\"><p>Cell</p></td></tr></tbody></table>",
        "<p style=\"text-align:center;margin-left:4em\" data-indent-level=\"2\">Aligned</p>"
    };
    public static TheoryData<string> Unsupported => new()
    {
        "<table><tbody><tr><td onclick=\"bad()\"><p>Original cell</p></td></tr></tbody></table>",
        "<p><img src=\"javascript:bad()\" alt=\"Original\"></p>",
        "<p style=\"text-align:center;color:red\">Aligned</p>",
        "<img src=\"data:image/svg+xml;base64,PHN2Zz4=\">",
        "<p data-future=\"keep\">Metadata</p>",
        "<p><a href=\"javascript:alert(1)\">Unsafe</a></p>",
        "<script>alert(1)</script><p>Keep source</p>",
        "<p><span style=\"font-size:20px\">Styled</span></p>",
        "<ol start=\"seven\"><li><p>Invalid start</p></li></ol>",
        "<pre><strong>Cannot preserve</strong></pre>"
    };

    [Theory, MemberData(nameof(Supported))]
    public void CommonHtmlIsEditableAndExportKeepsAttributes(string html)
    {
        Assert.True(DeviceContentCompatibility.CanEdit(html, LocalContentFormat.Html));
        var doc = LocalDocumentCodec.NewDocument(Guid.NewGuid(), "Fixture", DateTimeOffset.UtcNow, html);
        var export = Encoding.UTF8.GetString(DeviceDocumentFormats.Export(doc, DeviceExportFormat.Html).Content);
        Assert.Contains(System.Net.WebUtility.HtmlDecode(html), System.Net.WebUtility.HtmlDecode(export));
        var imported = DeviceDocumentFormats.ParseImport(new("fixture.html", Encoding.UTF8.GetBytes(html)));
        Assert.Equal(System.Net.WebUtility.HtmlDecode(html), System.Net.WebUtility.HtmlDecode(imported.Html));
    }

    [Theory, MemberData(nameof(Unsupported))]
    public async Task UnsupportedOriginalSurvivesOpenSaveBackupAndRejectedReplacement(string html)
    {
        string root = Path.Combine(Path.GetTempPath(), "WriterApp.Compatibility", Guid.NewGuid().ToString("N"));
        try
        {
            var repo = new LocalDocumentRepository(new FileLocalDocumentStore(root));
            var doc = await repo.CreateImportedAsync("Original", html);
            var session = new LocalEditorSession(repo, doc);
            var page = doc.Sections[0].Pages[0];
            Assert.False(DeviceContentCompatibility.CanEdit(html, page.ContentFormat));
            Assert.Throws<InvalidDataException>(() => session.Edit(page.PageId, "<p>Lossy conversion</p>"));
            Assert.Throws<InvalidDataException>(() => DeviceDocumentFormats.MergePage(page, "<p>Replacement</p>", DeviceImportMode.Replace));
            Assert.Throws<InvalidDataException>(() => DeviceDocumentFormats.Export(doc, DeviceExportFormat.Html));
            Assert.Throws<InvalidDataException>(() => DeviceDocumentFormats.Export(doc, DeviceExportFormat.Text));
            await session.SaveAsync();
            var reopened = (await repo.LoadAsync(doc.DocumentId))!;
            Assert.Equal(doc.LocalRevision, reopened.LocalRevision);
            Assert.Equal(html, reopened.Sections[0].Pages[0].Content);
            var backup = DeviceDocumentFormats.Export(reopened, DeviceExportFormat.SourceBackup);
            var decoded = LocalDocumentCodec.Decode(backup.Content, doc.DocumentId, backup.SuggestedFileName).Document;
            Assert.Equal(html, decoded.Sections[0].Pages[0].Content);
            Assert.Equal(page.ContentFormat, decoded.Sections[0].Pages[0].ContentFormat);
            Assert.False(session.IsDirty);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void ImportSanitizationIsSeparateFromStoredContentAcceptance()
    {
        const string original = "<h6><u>日本語</u></h6><p onclick=\"bad()\"><a href=\"javascript:bad()\">Link</a><img src=x></p>";
        Assert.False(DeviceContentCompatibility.CanEdit(original, LocalContentFormat.Html));
        var import = DeviceDocumentFormats.ParseImport(new("untrusted.html", Encoding.UTF8.GetBytes(original)));
        Assert.Equal("<h6><u>日本語</u></h6><p>Link</p>", import.Html);
        Assert.True(DeviceContentCompatibility.CanEdit(import.Html, LocalContentFormat.Html));
    }

    [Theory]
    [InlineData("{\"type\":\"doc\",\"content\":[{\"type\":\"heading\",\"attrs\":{\"level\":6},\"content\":[{\"type\":\"text\",\"text\":\"六\"}]}]}", true)]
    [InlineData("{\"type\":\"doc\",\"content\":[{\"type\":\"image\",\"attrs\":{\"src\":\"original\"}}]}", false)]
    [InlineData("{\"type\":\"doc\",\"content\":[{\"type\":\"heading\",\"attrs\":{\"level\":7}}]}", false)]
    [InlineData("{\"type\":\"doc\",\"content\":[{\"type\":\"paragraph\",\"attrs\":{\"textAlign\":\"center\"}}]}", true)]
    [InlineData("{\"type\":\"doc\",\"content\":[{\"type\":\"image\",\"attrs\":{\"src\":\"https://example.com/image.png\"}}]}", true)]
    [InlineData("{\"type\":\"doc\",\"content\":[{\"type\":\"paragraph\",\"attrs\":{\"indentLevel\":9}}]}", false)]
    [InlineData("{\"type\":\"doc\",\"content\":[{\"type\":\"text\",\"text\":\"invalid nesting\"}]}", false)]
    [InlineData("not json", false)]
    public void LegacyJsonMustHavePreservableSchema(string content, bool editable)
    {
        Assert.Equal(editable, DeviceContentCompatibility.CanEdit(content, LocalContentFormat.LegacyJson));
        var doc = LocalDocumentCodec.NewDocument(Guid.NewGuid(), "Legacy", DateTimeOffset.UtcNow, content, LocalContentFormat.LegacyJson);
        var backup = DeviceDocumentFormats.Export(doc, DeviceExportFormat.SourceBackup);
        var restored = LocalDocumentCodec.Decode(backup.Content, doc.DocumentId, backup.SuggestedFileName).Document;
        Assert.Equal(content, restored.Sections[0].Pages[0].Content);
        Assert.Equal(LocalContentFormat.LegacyJson, restored.Sections[0].Pages[0].ContentFormat);
    }

    [Fact]
    public async Task RichWritingSurvivesLocalSavePreviewHtmlExportAndReopen()
    {
        const string html = "<p style=\"text-align: center; margin-left: 2em;\" data-indent-level=\"1\"><s>Draft</s></p><table style=\"min-width: 50px;\"><colgroup><col style=\"min-width: 25px;\"><col style=\"min-width: 25px;\"></colgroup><tbody><tr><th colspan=\"1\" rowspan=\"1\"><p>Header</p></th><td colspan=\"1\" rowspan=\"1\"><p>Cell</p></td></tr></tbody></table><img src=\"data:image/png;base64,iVBORw0KGgo=\" alt=\"Embedded\"><img src=\"https://example.com/photo.jpg\" alt=\"Remote\">";
        string root = Path.Combine(Path.GetTempPath(), "WriterApp.Toolbar", Guid.NewGuid().ToString("N"));
        try
        {
            var repo = new LocalDocumentRepository(new FileLocalDocumentStore(root));
            var doc = await repo.CreateAsync("Toolbar");
            var session = new LocalEditorSession(repo, doc);
            session.Edit(doc.Sections[0].Pages[0].PageId, html); await session.SaveAsync();
            var reopened = (await repo.LoadAsync(doc.DocumentId))!;
            Assert.Equal(html, reopened.Sections[0].Pages[0].Content);
            var preview = LocalDocumentPreview.Render(reopened.Sections[0].Pages[0]);
            Assert.Null(preview.Warning);
            var export = Encoding.UTF8.GetString(DeviceDocumentFormats.Export(reopened, DeviceExportFormat.Html).Content);
            foreach (string token in new[] { "<table", "<colgroup", "colspan=\"1\"", "<s>Draft</s>", "text-align: center", "margin-left: 2em", "data:image/png;base64,iVBORw0KGgo=", "https://example.com/photo.jpg" })
            { Assert.Contains(token, preview.Html); Assert.Contains(token, export); }
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
