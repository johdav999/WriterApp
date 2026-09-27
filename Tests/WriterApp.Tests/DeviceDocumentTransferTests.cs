using System.Text;
using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using Xunit;

namespace WriterApp.Tests;

public sealed class DeviceDocumentTransferTests
{
    [Fact]
    public void TextImportEscapesMarkupAndPreservesLineBreaks()
    {
        var result = DeviceDocumentFormats.ParseImport(new("chapter.txt",
            Encoding.UTF8.GetBytes("First <script>alert(1)</script>\nSecond & third")));
        Assert.Equal("chapter", result.Title);
        Assert.Contains("&lt;script&gt;", result.Html);
        Assert.Contains("<p>Second &amp; third</p>", result.Html);
        Assert.DoesNotContain("<script>", result.Html);
    }

    [Fact]
    public void HtmlImportKeepsSupportedFormattingAndDropsActiveContent()
    {
        var result = DeviceDocumentFormats.ParseImport(new("draft.html", Encoding.UTF8.GetBytes(
            "<p onclick='run()'>Hello <strong>world</strong><img src='https://host/x'>" +
            "<script>alert(1)</script><a href='javascript:alert(1)'>bad link</a></p>")));
        Assert.Equal("<p>Hello <strong>world</strong>bad link</p>", result.Html);
        Assert.DoesNotContain("javascript:", result.Html);
        Assert.DoesNotContain("https://host/x", result.Html);
    }

    [Theory]
    [InlineData("draft.pdf", "text")]
    [InlineData("draft.txt", "")]
    [InlineData("draft.html", "<script>alert(1)</script>")]
    public void UnsupportedOrEmptyImportIsRejected(string name, string content)
    {
        Assert.Throws<InvalidDataException>(() => DeviceDocumentFormats.ParseImport(new(name, Encoding.UTF8.GetBytes(content))));
    }

    [Fact]
    public void BinaryAndOversizedImportsAreRejectedBeforeDocumentCreation()
    {
        Assert.Throws<InvalidDataException>(() => DeviceDocumentFormats.ParseImport(new("bad.txt", [0xFF, 0xFE])));
        Assert.Throws<InvalidDataException>(() => DeviceDocumentFormats.ParseImport(new("bad.txt", new byte[DeviceDocumentFormats.MaxImportBytes + 1])));
    }

    [Fact]
    public void AppendAndReplaceHaveExplicitAndDifferentResults()
    {
        var page = Page("<p>Original</p>");
        Assert.Equal("<p>Original</p><p></p><p>Imported</p>",
            DeviceDocumentFormats.MergePage(page, "<p>Imported</p>", DeviceImportMode.Append));
        Assert.Equal("<p>Imported</p>",
            DeviceDocumentFormats.MergePage(page, "<p>Imported</p>", DeviceImportMode.Replace));
        Assert.Contains("<a href=\"https://example.com\" target=\"_blank\">linked</a>",
            DeviceDocumentFormats.MergePage(page with { Content = "<p><a href=\"https://example.com\" target=\"_blank\">linked</a></p>" },
                "<p>Imported</p>", DeviceImportMode.Append));
        Assert.Throws<InvalidDataException>(() => DeviceDocumentFormats.MergePage(
            page with { Content = "<img src=x>", ContentFormat = LocalContentFormat.Html },
            "<p>Imported</p>", DeviceImportMode.Append));
    }

    [Theory]
    [InlineData("con", "document.html")]
    [InlineData("Lpt1.txt", "document.html")]
    [InlineData("My: Draft/One?*.", "My DraftOne.html")]
    [InlineData("  A title.  ", "A title.html")]
    public void SuggestedNamesAreSafeForWindows(string title, string expected)
    {
        Assert.Equal(expected, DeviceDocumentFormats.SafeFileName(title, ".html"));
        Assert.Equal(expected, DeviceDocumentFormats.Export(Document("<p>Words</p>") with { Title = title },
            DeviceExportFormat.Html).SuggestedFileName);
    }

    [Fact]
    public void OfflineExportsIncludeAllPagesAndNeverEmitActiveMarkup()
    {
        var document = Document("<p>Alpha <strong>beta</strong><script>attack()</script></p>");
        LocalSection first = document.Sections[0];
        first = first with { Title = "First", Pages = [Page("<p>Later page</p>") with { OrderIndex = 1 }, first.Pages[0]] };
        LocalSection second = first with { SectionId = Guid.NewGuid(), Title = "Second", OrderIndex = 1,
            Pages = [Page("<p>Final page</p>")] };
        document = document with { Sections = [second, first] };
        DevicePreparedExport html = DeviceDocumentFormats.Export(document, DeviceExportFormat.Html);
        string rendered = Encoding.UTF8.GetString(html.Content);
        Assert.EndsWith(".html", html.SuggestedFileName);
        Assert.Contains("<strong>beta</strong>", rendered);
        Assert.Contains("Content-Security-Policy", rendered);
        Assert.DoesNotContain("<script>", rendered);
        Assert.DoesNotContain("attack()", rendered);
        Assert.True(rendered.IndexOf("Alpha", StringComparison.Ordinal) < rendered.IndexOf("Later page", StringComparison.Ordinal));
        Assert.True(rendered.IndexOf("Later page", StringComparison.Ordinal) < rendered.IndexOf("Final page", StringComparison.Ordinal));
        DevicePreparedExport text = DeviceDocumentFormats.Export(document, DeviceExportFormat.Text);
        Assert.Contains("Alpha beta", Encoding.UTF8.GetString(text.Content));
        Assert.Contains("Final page", Encoding.UTF8.GetString(text.Content));
        Assert.DoesNotContain("attack()", Encoding.UTF8.GetString(text.Content));
    }

    [Fact]
    public async Task CancelledPickerAndFailedParseLeaveLibraryUnchanged()
    {
        using var fixture = new Fixture();
        var dialog = new FakeDialog();
        var transfer = new DeviceDocumentTransfer(fixture.Repository, dialog);
        Assert.Null(await transfer.PickImportAsync());
        Assert.Empty((await fixture.Repository.ListAsync()).Documents);
        dialog.Import = new("unsafe.html", Encoding.UTF8.GetBytes("<img src=x>"));
        await Assert.ThrowsAsync<InvalidDataException>(() => transfer.PickImportAsync());
        Assert.Empty((await fixture.Repository.ListAsync()).Documents);
    }

    [Fact]
    public async Task NewImportIsWrittenAtomicallyAndExistingImportChecksRevision()
    {
        using var fixture = new Fixture();
        var prepared = DeviceDocumentFormats.ParseImport(new("story.txt", Encoding.UTF8.GetBytes("Imported")));
        LocalDocument created = await fixture.Repository.CreateImportedAsync(prepared.Title, prepared.Html);
        Assert.Equal("<p>Imported</p>", (await fixture.Repository.LoadAsync(created.DocumentId))!.Sections[0].Pages[0].Content);
        var transfer = new DeviceDocumentTransfer(fixture.Repository, new FakeDialog());
        Guid pageId = created.Sections[0].Pages[0].PageId;
        LocalDocument appended = await transfer.ImportPageAsync(created, pageId, prepared, DeviceImportMode.Append);
        Assert.Contains("<p>Imported</p><p></p><p>Imported</p>", appended.Sections[0].Pages[0].Content);
        await Assert.ThrowsAsync<LocalDocumentConflictException>(() =>
            transfer.ImportPageAsync(created, pageId, prepared, DeviceImportMode.Replace));
        Assert.Equal(appended.Sections[0].Pages[0].Content,
            (await fixture.Repository.LoadAsync(created.DocumentId))!.Sections[0].Pages[0].Content);
    }

    [Fact]
    public async Task ExportCancelOrWriteFailureDoesNotChangeTheLocalDocument()
    {
        using var fixture = new Fixture();
        LocalDocument document = await fixture.Repository.CreateAsync("Draft");
        var dialog = new FakeDialog();
        var transfer = new DeviceDocumentTransfer(fixture.Repository, dialog);
        Assert.False(await transfer.ExportAsync(document, DeviceExportFormat.Html));
        Assert.EndsWith("Draft.html", dialog.SuggestedName);
        dialog.ThrowOnSave = true;
        await Assert.ThrowsAsync<IOException>(() => transfer.ExportAsync(document, DeviceExportFormat.Text));
        LocalDocument stored = (await fixture.Repository.LoadAsync(document.DocumentId))!;
        Assert.Equal(document.LocalRevision, stored.LocalRevision);
        Assert.Equal(document.Sections[0].Pages[0].Content, stored.Sections[0].Pages[0].Content);
    }

    private static LocalPage Page(string html) => new()
    { PageId = Guid.NewGuid(), Title = "Page", OrderIndex = 0, CreatedAtUtc = DateTimeOffset.UtcNow,
      UpdatedAtUtc = DateTimeOffset.UtcNow, ContentFormat = LocalContentFormat.Html, Content = html };
    private static LocalDocument Document(string html)
    {
        var now = DateTimeOffset.UtcNow;
        return LocalDocumentCodec.NewDocument(Guid.NewGuid(), "Draft", now, html);
    }
    private sealed class FakeDialog : IDeviceFileDialog
    {
        public bool IsAvailable => true;
        public DeviceImportFile? Import { get; set; }
        public bool ThrowOnSave { get; set; }
        public string? SuggestedName { get; private set; }
        public Task<DeviceImportFile?> PickImportAsync(CancellationToken cancellationToken = default) => Task.FromResult(Import);
        public Task<bool> SaveAsync(string suggestedFileName, string extension, byte[] content, CancellationToken cancellationToken = default)
        {
            SuggestedName = suggestedFileName;
            if (ThrowOnSave) throw new IOException("Simulated export failure.");
            return Task.FromResult(false);
        }
    }
    private sealed class Fixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "WriterApp_TransferTests_" + Guid.NewGuid().ToString("N"));
        public LocalDocumentRepository Repository { get; }
        public Fixture() => Repository = new(new FileLocalDocumentStore(_root));
        public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
    }
}
