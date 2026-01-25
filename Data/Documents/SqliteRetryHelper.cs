using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace WriterApp.Data.Documents
{
    internal static class SqliteRetryHelper
    {
        private const int MaxAttempts = 3;
        private const int BaseDelayMs = 60;

        internal static async Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken ct)
        {
            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                try
                {
                    return await action();
                }
                catch (SqliteException ex) when (ex.SqliteErrorCode == 5 && attempt < MaxAttempts)
                {
                    int delay = BaseDelayMs * attempt;
                    await Task.Delay(delay, ct);
                }
            }

            return await action();
        }

        internal static async Task ExecuteAsync(Func<Task> action, CancellationToken ct)
        {
            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                try
                {
                    await action();
                    return;
                }
                catch (SqliteException ex) when (ex.SqliteErrorCode == 5 && attempt < MaxAttempts)
                {
                    int delay = BaseDelayMs * attempt;
                    await Task.Delay(delay, ct);
                }
            }

            await action();
        }
    }
}
