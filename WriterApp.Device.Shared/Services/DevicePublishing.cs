using System.IO.Compression;
using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using WriterApp.Application.Exporting;
using WriterApp.Application.Importing;
using WriterApp.Device.Shared.Storage;
using WriterApp.Domain.Documents;

namespace WriterApp.Device.Shared.Services;

public sealed record LocalPublishingOptions(DeviceExportFormat Format = DeviceExportFormat.Html,
    string Scope = "document", Guid? SectionId = null, string Template = "manuscript",
    bool TitlePage = true, bool IncludeCover = false);
public sealed record PublishingSnapshot(Guid DocumentId, long Revision, LocalPublishingOptions Options, DevicePreparedExport File, string PreviewHtml,
    long AccountGeneration = -1);

public static class DevicePublishing
{
    public const string ConversionNotice = "DOCX import preserves supported paragraphs, headings, basic emphasis, lists and links. Tables, images, comments, tracked changes, headers, footers and advanced layout may be omitted or simplified. Keep your original file. Markdown loses layout and some inline styling. DOCX/EPUB pagination and fonts vary by reader. Preview shows HTML layout, not a guarantee of Word or ebook pagination.";

    public static DevicePreparedImport ImportDocx(DeviceImportFile file)
    {
        if (file.Content.Length is 0 or > DeviceDocumentFormats.MaxImportBytes) throw new InvalidDataException("DOCX must be nonempty and at most 5 MB.");
        using (var zip = new ZipArchive(new MemoryStream(file.Content), ZipArchiveMode.Read))
        {
            if (zip.Entries.Count > 2000 || zip.Entries.Sum(e => e.Length) > 25 * 1024 * 1024
                || zip.Entries.Any(e => e.Length > 10 * 1024 * 1024)) throw new InvalidDataException("DOCX expands beyond the safe import limit.");
            if (zip.GetEntry("word/document.xml") is null) throw new InvalidDataException("The file is not a Word DOCX document.");
        }
        try
        {
            var result = new SectionImportService().ConvertAsync(file.FileName, file.Content, new(false, true), CancellationToken.None).GetAwaiter().GetResult();
            string html = DeviceDocumentFormats.SanitizeHtml(result.Html);
            DeviceContentCompatibility.RequireEditable(html, LocalContentFormat.Html);
            if (string.IsNullOrWhiteSpace(WriterApp.Application.State.PlainTextMapper.ToPlainText(html))) throw new InvalidDataException("DOCX contains no supported writing.");
            return new(Path.GetFileNameWithoutExtension(DeviceDocumentFormats.SafeFileName(Path.GetFileNameWithoutExtension(file.FileName), ".html")), html, result.Warnings);
        }
        catch (Exception e) when (e is System.Xml.XmlException or DocumentFormat.OpenXml.Packaging.OpenXmlPackageException)
        { throw new InvalidDataException("DOCX could not be read safely.", e); }
    }

    public static LocalDocument Select(LocalDocument document, LocalPublishingOptions options)
    {
        if (document.DeletedAtUtc is not null) throw new InvalidOperationException("Restore the document before exporting.");
        if (options.Scope is not ("document" or "section")) throw new InvalidDataException("Choose whole manuscript or one section.");
        var active = document.Project is { } project
            ? document.Sections.Where(s => project.Nodes.Any(n => n.NodeType == "scene" && n.SectionId == s.SectionId && n.DeletionId is null))
            : document.Sections;
        if (options.Scope == "section") active = active.Where(s => s.SectionId == options.SectionId);
        var sections = active.OrderBy(s => s.OrderIndex).ToArray();
        if (sections.Length == 0) throw new InvalidDataException("The selected export scope is no longer available.");
        return document with { Sections = sections };
    }

    public static async Task<PublishingSnapshot> PrepareAsync(LocalDocument document, LocalPublishingOptions options, LocalCover? cover = null)
    {
        if(options.IncludeCover) cover=LocalCover.FromProject(document) ?? cover;
        if(options.IncludeCover && document.Project?.CoverImageUrl is { } remote && remote.StartsWith("https://",StringComparison.OrdinalIgnoreCase)
            && (cover?.RemoteReference!=remote || cover.ContentHash!=WriterApp.Shared.CoverAssetContract.Hash(cover.Png) || cover.AccountScope is not {Length:64}))
            throw new InvalidDataException("Cache the exact owned project cover in Cover studio before offline publishing. The remote reference is preserved.");
        if (!Enum.IsDefined(options.Format) || options.Format == DeviceExportFormat.SourceBackup) throw new InvalidDataException("Choose a publishing format.");
        var selected = Select(document, options);
        foreach (var page in selected.Sections.SelectMany(s => s.Pages)) DeviceContentCompatibility.RequireEditable(page.Content, page.ContentFormat);
        if (options.IncludeCover && (cover is null || cover.DocumentId != document.DocumentId || cover.ProjectId != document.Project?.ProjectId))
            throw new InvalidDataException("Choose a local cover for this manuscript first.");
        if (options.IncludeCover && options.Format is DeviceExportFormat.Text or DeviceExportFormat.Markdown)
            throw new InvalidDataException("This format cannot embed a cover. Turn off Include cover or choose HTML, DOCX, EPUB or PDF.");
        if (options.IncludeCover) LocalCover.Validate(cover!.Png);
        var template = ExportTemplatePresets.GetByKey(options.Template) ?? throw new InvalidDataException("Unknown layout template.");
        var domain = new Document { DocumentId=document.DocumentId,
            Metadata=new() { Title=document.Title, Author=document.Project?.AuthorName ?? "", Language=document.LanguageCode ?? "en", ModifiedUtc=document.UpdatedAtUtc.UtcDateTime },
            Chapters=[new() { Sections=selected.Sections.Select(s => new Section { SectionId=s.SectionId, Title=s.Title, Order=s.OrderIndex,
                NumberingStyle=SectionNumberingStyle.None, Content=new() { Format="html", Value=string.Join("\n",s.Pages.OrderBy(p=>p.OrderIndex).Select(p=>LocalDocumentPreview.Render(p).Html)) } }).ToList() }] };
        var exportOptions = new ExportOptions(IncludeTitlePage:options.TitlePage, IncludeToc:false,
            Template:ExportTemplateDefaults.CreateFromPreset("local",DateTimeOffset.UtcNow,template), IncludeCover:options.IncludeCover,
            CoverImageUrl:options.IncludeCover ? cover!.DataUri : null);
        var preview = await new TemplatedHtmlExportRenderer().RenderAsync(domain,exportOptions);
        string html = Encoding.UTF8.GetString(preview.Content).Replace("<head>","<head><meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; img-src data:; style-src 'unsafe-inline'\">");
        DevicePreparedExport file;
        if (options.Format == DeviceExportFormat.Text) file = DeviceDocumentFormats.Export(selected,DeviceExportFormat.Text);
        else if (options.Format is DeviceExportFormat.Html or DeviceExportFormat.Pdf) file = new(preview.FileName,".html",Encoding.UTF8.GetBytes(html));
        else
        {
            IExportRenderer renderer = options.Format switch {
                DeviceExportFormat.Markdown => new MarkdownExportRenderer(),
                DeviceExportFormat.Epub => new EpubExportRenderer(),
                DeviceExportFormat.Docx => new DocxExportRenderer(NullLogger<DocxExportRenderer>.Instance, new ConfigurationBuilder().Build(), new NoNetworkClients()),
                _ => throw new InvalidDataException("Unsupported format.") };
            var result = await renderer.RenderAsync(domain,exportOptions);
            file = new(result.FileName,Path.GetExtension(result.FileName),result.Content);
        }
        file=file with { SuggestedFileName=Path.ChangeExtension(DeviceDocumentFormats.SafeFileName(document.Title,".html"),file.Extension) };
        return new(document.DocumentId,document.LocalRevision,options,file,html);
    }

    // The portable DOCX converter is explicitly configured not to fetch remote images.
    private sealed class NoNetworkClients : IHttpClientFactory
    { public HttpClient CreateClient(string name) => throw new InvalidOperationException("Offline publishing cannot fetch remote assets."); }
}
