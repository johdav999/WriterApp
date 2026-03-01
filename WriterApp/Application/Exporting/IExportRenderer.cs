using System.Threading.Tasks;
using WriterApp.Domain.Documents;

namespace WriterApp.Application.Exporting
{
    public interface IExportRenderer
    {
        ExportFormat Format { get; }
        ExportKind Kind { get; }
        Task<ExportResult> RenderAsync(Document document, ExportOptions options);
    }
}
