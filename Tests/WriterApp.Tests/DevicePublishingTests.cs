using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using Xunit;
namespace WriterApp.Tests;

public sealed class DevicePublishingTests : IDisposable
{
    private readonly string _root=Path.Combine(Path.GetTempPath(),"WriterApp.Publishing",Guid.NewGuid().ToString("N"));
    private const string Png="iVBORw0KGgoAAAANSUhEUgAAAFAAAAB4CAIAAADqjOKhAAAA0ElEQVR4nOXOMQEAMAyAMIbySp+L9iAK8mAokRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJ8Tqw7QPmGwFwSBMZLwAAAABJRU5ErkJggg==";
    private async Task<LocalDocument> Book()
    {
        var store=new FileLocalDocumentStore(Path.Combine(_root,"documents"));
        var doc=await store.CreateAsync("Unicode book");
        doc=LocalDocumentStructure.Apply(doc,new(LocalStructureAction.CreateSection,Title:"Second scene"),DateTimeOffset.UtcNow);
        // Publishing accepts standalone section order too; avoid inventing project-node links for this fixture.
        doc=doc with { Project=null, Sections=doc.Sections.Select((s,i)=>s with { OrderIndex=i, Title=i==0?"First scene":"Second scene",
            Pages=s.Pages.Select(p=>p with { ContentFormat=LocalContentFormat.Html, Content=i==0
                ? "<h2>Räksmörgås 日本語</h2><p>A <strong>bold</strong> <em>word</em> and <a href=\"https://example.com/story\">safe link</a>.</p><ol><li>One</li><li>Two</li></ol>"
                : "<p>Second scene ending.</p>" }).ToArray() }).ToArray() };
        return await store.SaveAsync(doc);
    }
    [Theory]
    [InlineData(DeviceExportFormat.Html)]
    [InlineData(DeviceExportFormat.Markdown)]
    [InlineData(DeviceExportFormat.Docx)]
    [InlineData(DeviceExportFormat.Epub)]
    [InlineData(DeviceExportFormat.Text)]
    public async Task SnapshotPreservesWritingAndSelectedScope(DeviceExportFormat format)
    {
        var doc=await Book(); byte[] before=LocalDocumentCodec.Encode(doc);
        var result=await DevicePublishing.PrepareAsync(doc,new(format,"section",doc.Sections[0].SectionId));
        Assert.Equal(before,LocalDocumentCodec.Encode(doc));
        Assert.Equal(doc.LocalRevision,result.Revision);
        Assert.DoesNotContain("Second scene ending.",result.PreviewHtml);
        string text=Read(result.File);
        Assert.Contains("Räksmörgås",System.Net.WebUtility.HtmlDecode(text));
        Assert.DoesNotContain("Second scene ending.",text);
        if(format is DeviceExportFormat.Markdown or DeviceExportFormat.Docx or DeviceExportFormat.Epub or DeviceExportFormat.Html)
            Assert.Contains("https://example.com/story",text);
    }
    private static string Read(DevicePreparedExport file)
    {
        if(file.Extension==".docx") {
            using var doc=WordprocessingDocument.Open(new MemoryStream(file.Content),false);
            Assert.Contains(doc.MainDocumentPart!.Document!.Descendants<Paragraph>(),p=>p.ParagraphProperties?.NumberingProperties is not null);
            Assert.Contains(doc.MainDocumentPart!.Document!.Descendants<Bold>(),_=>true);
            return doc.MainDocumentPart.Document.InnerText+" "+string.Join(" ",doc.MainDocumentPart.HyperlinkRelationships.Select(r=>r.Uri.ToString()));
        }
        if(file.Extension==".epub") {
            using var zip=new ZipArchive(new MemoryStream(file.Content));
            Assert.Equal("application/epub+zip",new StreamReader(zip.GetEntry("mimetype")!.Open()).ReadToEnd());
            var xml=zip.Entries.Where(e=>e.FullName.EndsWith(".xhtml")||e.FullName.EndsWith(".opf")).Select(e=>new StreamReader(e.Open()).ReadToEnd()).ToArray();
            foreach(var item in xml) XDocument.Parse(item);
            return string.Join("\n",xml);
        }
        return Encoding.UTF8.GetString(file.Content);
    }
    [Fact]
    public async Task DocxImportRetainsSupportedFormattingAndLinks()
    {
        var doc=await Book();
        var file=(await DevicePublishing.PrepareAsync(doc,new(DeviceExportFormat.Docx))).File;
        var imported=DeviceDocumentFormats.ParseImport(new(file.SuggestedFileName,file.Content));
        Assert.Contains("Räksmörgås",System.Net.WebUtility.HtmlDecode(imported.Html));
        Assert.Contains("<strong>bold</strong>",imported.Html);
        Assert.Contains("href=\"https://example.com/story\"",imported.Html);
        Assert.Contains("<ol>",imported.Html);
    }
    [Fact]
    public async Task PresetsAndCoverSurviveRestartAndRejectStaleUpdates()
    {
        var doc=await Book();var store=new LocalPublishingStore(Path.Combine(_root,"publishing"));
        var initial=await store.LoadAsync(doc);
        var cover=LocalCover.Create(doc,new("cover.png",Convert.FromBase64String(Png)));
        var preset=new LocalExportPreset(Guid.NewGuid(),"Paperback",new(DeviceExportFormat.Epub,IncludeCover:true));
        var saved=await store.SaveAsync(doc,initial with { Cover=cover,Presets=[preset],Options=preset.Options });
        var reopened=await new LocalPublishingStore(Path.Combine(_root,"publishing")).LoadAsync(doc);
        Assert.Equal(saved.Revision,reopened.Revision);
        Assert.Equal(preset,reopened.Presets.Single());
        Assert.Equal(cover.Png,reopened.Cover!.Png);
        await Assert.ThrowsAsync<IOException>(()=>store.SaveAsync(doc,initial));
        var epub=await DevicePublishing.PrepareAsync(doc,reopened.Options,reopened.Cover);
        using var zip=new ZipArchive(new MemoryStream(epub.File.Content));
        Assert.Contains(zip.Entries,e=>e.FullName.EndsWith("cover.png"));
        await Assert.ThrowsAsync<InvalidDataException>(()=>DevicePublishing.PrepareAsync(doc,new(DeviceExportFormat.Epub,IncludeCover:true),cover with { DocumentId=Guid.NewGuid() }));
    }
    [Fact]
    public async Task UnknownSettingsAndInvalidScopeFailWithoutChangingWriting()
    {
        var doc=await Book();var store=new LocalPublishingStore(Path.Combine(_root,"publishing"));
        var saved=await store.SaveAsync(doc,await store.LoadAsync(doc));
        var path=Path.Combine(_root,"publishing",doc.DocumentId.ToString("N")+".json");
        string future=(await File.ReadAllTextAsync(path)).Replace("\"Version\":1","\"Version\":99");
        await File.WriteAllTextAsync(path,future);
        await Assert.ThrowsAsync<InvalidDataException>(()=>store.LoadAsync(doc));
        Assert.Equal(future,await File.ReadAllTextAsync(path));
        await Assert.ThrowsAsync<InvalidDataException>(()=>DevicePublishing.PrepareAsync(doc,new(Scope:"section",SectionId:Guid.NewGuid())));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>DevicePublishing.PrepareAsync(doc with { DeletedAtUtc=DateTimeOffset.UtcNow },new()));
    }
    [Fact]
    public async Task WholeManuscriptUsesReorderedScenesAndOmitsTrash()
    {
        var doc=await Book();
        doc=LocalProjectStructure.Attach(doc,"Project");
        var second=doc.Project!.Nodes.Single(n=>n.SectionId==doc.Sections[1].SectionId);
        doc=LocalProjectStructure.Apply(doc,new(LocalProjectAction.Up,second.NodeId),DateTimeOffset.UtcNow);
        foreach(var format in new[]{DeviceExportFormat.Html,DeviceExportFormat.Markdown,DeviceExportFormat.Docx,DeviceExportFormat.Epub,DeviceExportFormat.Text})
        {
            var text=Read((await DevicePublishing.PrepareAsync(doc,new(format))).File);
            Assert.True(text.IndexOf("Second scene ending.",StringComparison.Ordinal)<text.IndexOf("Räksmörgås",StringComparison.Ordinal));
        }
        doc=LocalProjectStructure.Apply(doc,new(LocalProjectAction.Delete,second.NodeId),DateTimeOffset.UtcNow);
        Assert.DoesNotContain("Second scene ending.",(await DevicePublishing.PrepareAsync(doc,new())).PreviewHtml);
    }
    [Fact]
    public async Task CoverIsEmbeddedLocallyAndTextFormatsRejectIt()
    {
        var doc=await Book();var cover=LocalCover.Create(doc,new("cover.png",Convert.FromBase64String(Png)));
        var html=await DevicePublishing.PrepareAsync(doc,new(IncludeCover:true),cover);
        Assert.Contains(cover.DataUri,html.PreviewHtml);
        var file=(await DevicePublishing.PrepareAsync(doc,new(DeviceExportFormat.Docx,IncludeCover:true),cover)).File;
        using var word=WordprocessingDocument.Open(new MemoryStream(file.Content),false);
        Assert.Single(word.MainDocumentPart!.ImageParts);
        await Assert.ThrowsAsync<InvalidDataException>(()=>DevicePublishing.PrepareAsync(doc,new(DeviceExportFormat.Markdown,IncludeCover:true),cover));
        await Assert.ThrowsAsync<InvalidDataException>(()=>DevicePublishing.PrepareAsync(doc,new(IncludeCover:true),cover with { Png=[1,2,3] }));
    }
    [Fact]
    public void MalformedAndExpandedDocxAndCoverAreRejected()
    {
        Assert.Throws<InvalidDataException>(()=>DevicePublishing.ImportDocx(new("bad.docx",[1,2,3])));
        using var bytes=new MemoryStream();
        using(var zip=new ZipArchive(bytes,ZipArchiveMode.Create,true)) {
            using var entry=zip.CreateEntry("word/document.xml",CompressionLevel.SmallestSize).Open();
            entry.Write(new byte[11*1024*1024]);
        }
        Assert.Throws<InvalidDataException>(()=>DevicePublishing.ImportDocx(new("bomb.docx",bytes.ToArray())));
        byte[] png=Convert.FromBase64String(Png);png[35]^=1;
        Assert.Throws<InvalidDataException>(()=>LocalCover.Validate(png));
        Assert.Throws<InvalidDataException>(()=>LocalCover.Validate(Encoding.UTF8.GetBytes("<svg/>")));
    }
    public void Dispose() { if(Directory.Exists(_root)) Directory.Delete(_root,true); }
}
