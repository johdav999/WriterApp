using System.Reflection;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WriterApp.Application.Diagnostics;
using Xunit;

namespace WriterApp.Tests;

public sealed class SqlServerStartupProbeTests
{
    [Fact]
    public async Task ResumingDatabaseRetriesWithBackoffThenBecomesReady()
    {
        int calls = 0;
        var delays = new List<TimeSpan>();
        bool ready = await Wait(_ => ++calls < 6 ? Task.FromException(new TimeoutException()) : Task.CompletedTask,
            delay: (duration, _) => { delays.Add(duration); return Task.CompletedTask; });
        Assert.True(ready);
        Assert.Equal(6, calls);
        Assert.Equal(new[] { 5d, 10d, 20d, 30d, 30d }, delays.Select(d => d.TotalSeconds));
    }

    [Fact]
    public async Task SuccessfulFirstProbeDoesNotDelay()
    {
        Assert.True(await Wait(_ => Task.CompletedTask,
            delay: (_, _) => throw new InvalidOperationException("Unexpected retry")));
    }

    [Fact]
    public async Task NonTransientFailureDoesNotRetryOrClaimReadiness()
    {
        int calls = 0;
        Assert.False(await Wait(_ => { calls++; return Task.FromException(new InvalidOperationException("Bad configuration")); }));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task DeadlineBoundsAnInFlightProbe()
    {
        Assert.False(await Wait(token => Task.Delay(Timeout.InfiniteTimeSpan, token), timeout: TimeSpan.FromMilliseconds(30)));
    }

    [Fact]
    public async Task DeadlineBoundsBackoff()
    {
        int calls = 0;
        Assert.False(await Wait(_ => { calls++; return Task.FromException(new TimeoutException()); }, timeout: TimeSpan.FromMilliseconds(30)));
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShutdownCancellationPropagatesDuringProbeAndBackoff(bool duringBackoff)
    {
        using var shutdown = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Wait(token =>
        {
            if (duringBackoff) return Task.FromException(new TimeoutException());
            shutdown.Cancel();
            return Task.Delay(Timeout.InfiniteTimeSpan, token);
        }, ct: shutdown.Token, delay: (_, token) =>
        {
            shutdown.Cancel();
            return Task.Delay(Timeout.InfiniteTimeSpan, token);
        }));
    }

    [Theory]
    [InlineData(40613, true)]
    [InlineData(40197, true)]
    [InlineData(40501, true)]
    [InlineData(18456, false)]
    [InlineData(208, false)]
    public void SqlProviderClassifiesAvailabilityButNotLoginOrSchemaErrors(int number, bool expected)
    {
        using var db = new DbContext(new DbContextOptionsBuilder<DbContext>()
            .UseSqlServer("Server=unused;Database=unused;Integrated Security=true").Options);
        Assert.Equal(expected, new SqlServerStartupProbe.TransientErrors(db).IsTransient(SqlError(number)));
    }

    private static Task<bool> Wait(Func<CancellationToken, Task> probe, TimeSpan? timeout = null,
        CancellationToken ct = default, Func<TimeSpan, CancellationToken, Task>? delay = null) =>
        SqlServerStartupProbe.WaitAsync(probe, ex => ex is TimeoutException,
            NullLogger.Instance, timeout ?? TimeSpan.FromSeconds(2), ct, delay);

    // SqlClient exposes no public exception factory. Construct a provider error solely
    // to validate its real classifier without contacting a database.
    private static SqlException SqlError(int number)
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var constructor = typeof(SqlError).GetConstructors(flags).OrderBy(c => c.GetParameters().Length).First();
        var arguments = constructor.GetParameters().Select((p, i) => i == 0 ? (object)number :
            p.ParameterType == typeof(string) ? "test" : p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null).ToArray();
        var error = (SqlError)constructor.Invoke(arguments);
        var collection = (SqlErrorCollection)Activator.CreateInstance(typeof(SqlErrorCollection), nonPublic: true)!;
        typeof(SqlErrorCollection).GetMethod("Add", flags)!.Invoke(collection, [error]);
        var factory = typeof(SqlException).GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .Single(m => m.Name == "CreateException" && m.GetParameters().Length == 2);
        return (SqlException)factory.Invoke(null, [collection, "test"])!;
    }
}
