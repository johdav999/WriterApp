using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace WriterApp.Data.Documents
{
    internal static class DatabaseRetryHelper
    {
        private const int MaxAttempts = 3;
        private const int BaseDelayMs = 60;

        internal static async Task<T> ExecuteAsync<T>(
            AppDbContext dbContext,
            Func<Task<T>> action,
            CancellationToken ct)
        {
            if (!dbContext.Database.IsSqlite())
            {
                return await action();
            }

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

        internal static async Task ExecuteAsync(
            AppDbContext dbContext,
            Func<Task> action,
            CancellationToken ct)
        {
            if (!dbContext.Database.IsSqlite())
            {
                await action();
                return;
            }

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
