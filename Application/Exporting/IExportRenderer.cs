using System.Threading.Tasks;
using WriterApp.Domain.Documents;

namespace WriterApp.Application.Exporting
{
    public interface IExportRenderer
    {
        ExportFormat Format { get; }
        Task<ExportResult> RenderAsync(Document document, ExportOptions options);
    }
}
