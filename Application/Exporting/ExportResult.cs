namespace WriterApp.Application.Exporting
{
    public sealed record ExportResult(
        byte[] Content,
        string MimeType,
        string FileName);
}
