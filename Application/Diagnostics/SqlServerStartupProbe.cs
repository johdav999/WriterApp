using System.Runtime.CompilerServices;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

[assembly: InternalsVisibleTo("WriterApp.Tests")]

namespace WriterApp.Application.Diagnostics;

/// <summary>Allows an auto-paused Azure SQL database to resume before startup work runs.</summary>
internal static class SqlServerStartupProbe
{
    private static readonly TimeSpan ResumeTimeout = TimeSpan.FromMinutes(3);

    public static Task<bool> WaitAsync(DbContext db, ILogger logger, CancellationToken ct)
    {
        var classifier = new TransientErrors(db);
        string connectionString = db.Database.GetDbConnection().ConnectionString;
        return WaitAsync(async token =>
        {
            // A fresh, read-only connection per attempt avoids EF logging an expected resume
            // failure as a failed application command. Request-level EF retries stay unchanged.
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(token);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1;";
            await command.ExecuteScalarAsync(token);
        }, classifier.IsTransient, logger, ResumeTimeout, ct);
    }

    internal static async Task<bool> WaitAsync(
        Func<CancellationToken, Task> probe,
        Func<Exception, bool> isTransient,
        ILogger logger,
        TimeSpan timeout,
        CancellationToken ct,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(timeout);
        delay ??= (duration, token) => Task.Delay(duration, token);
        int attempt = 0;

        try
        {
            while (true)
            {
                deadline.Token.ThrowIfCancellationRequested();
                try
                {
                    await probe(deadline.Token);
                    logger.LogInformation("SQL startup probe succeeded after {Attempts} attempt(s).", attempt + 1);
                    return true;
                }
                catch (Exception ex) when (!deadline.IsCancellationRequested && isTransient(ex))
                {
                    // Start at five seconds, then back off to thirty seconds. Only this
                    // read-only startup probe gets a longer budget than normal requests.
                    var retryDelay = TimeSpan.FromSeconds(Math.Min(30, 5 * Math.Pow(2, Math.Min(attempt, 3))));
                    attempt++;
                    logger.LogWarning(
                        "SQL database is temporarily unavailable (error {ErrorNumber}). Startup retry {Attempt} in {DelaySeconds}s; waiting up to {TimeoutSeconds}s for database availability.",
                        ex is SqlException sql ? sql.Number : (int?)null, attempt, retryDelay.TotalSeconds, timeout.TotalSeconds);
                    await delay(retryDelay, deadline.Token);
                }
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested && deadline.IsCancellationRequested)
        {
            logger.LogWarning("SQL database did not become available within {TimeoutSeconds}s. Optional startup database work will be skipped; /healthz and /warmup report database readiness.", timeout.TotalSeconds);
            return false;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // Authentication and configuration errors are not retried. Retain their
            // diagnostics so a real outage is not mistaken for a successful resume.
            logger.LogError(ex, "SQL startup probe failed. Check database availability and connection configuration. Optional startup database work will be skipped.");
            return false;
        }
    }

    internal sealed class TransientErrors(DbContext db) : SqlServerRetryingExecutionStrategy(db)
    {
        // Use the provider's maintained transient-error classifier, including 40613.
        public bool IsTransient(Exception error) => ShouldRetryOn(error);
    }
}
