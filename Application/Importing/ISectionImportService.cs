using System.Threading;
using System.Threading.Tasks;

namespace WriterApp.Application.Importing
{
    public interface ISectionImportService
    {
        Task<SectionImportResult> ConvertAsync(
            string fileName,
            byte[] fileBytes,
            SectionImportOptions options,
            CancellationToken ct);
    }
}
