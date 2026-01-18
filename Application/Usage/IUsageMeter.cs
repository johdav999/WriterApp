using System;
using System.Threading.Tasks;
using WriterApp.Data.Usage;

namespace WriterApp.Application.Usage
{
    public interface IUsageMeter
    {
        Task RecordAsync(UsageEvent usageEvent);
        Task<UsageSnapshot> GetCurrentPeriodAsync(string userId, string kind);
        Task<UsageSnapshot> GetRangeAsync(string userId, string kind, DateTime startUtc, DateTime endUtc);
    }
}
