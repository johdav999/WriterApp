using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace WriterApp.Application.Search
{
    public sealed class SearchIndexBackfillHostedService : BackgroundService
    {
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> UserLocks = new(StringComparer.Ordinal);
        private readonly ISearchIndexBackfillQueue _queue;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<SearchIndexBackfillHostedService> _logger;

        public SearchIndexBackfillHostedService(
            ISearchIndexBackfillQueue queue,
            IServiceScopeFactory scopeFactory,
            ILogger<SearchIndexBackfillHostedService> logger)
        {
            _queue = queue ?? throw new ArgumentNullException(nameof(queue));
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                string userId;
                try
                {
                    userId = await _queue.DequeueAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                SemaphoreSlim userLock = UserLocks.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1));
                if (!await userLock.WaitAsync(0, stoppingToken))
                {
                    _logger.LogInformation("BACKFILL_ALREADY_RUNNING backfill already running for user {UserId}.", userId);
                    _queue.MarkCompleted(userId);
                    continue;
                }

                try
                {
                    using IServiceScope scope = _scopeFactory.CreateScope();
                    var worker = scope.ServiceProvider.GetRequiredService<ISearchIndexBackfillWorker>();

                    using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, timeoutCts.Token);

                    try
                    {
                        await worker.BackfillUserAsync(userId, linkedCts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        string reason = stoppingToken.IsCancellationRequested
                            ? "host"
                            : timeoutCts.IsCancellationRequested
                                ? "timeout"
                                : "unknown";

                        _logger.LogInformation(
                            "BACKFILL_CANCELLED backfill canceled. Reason={Reason} UserId={UserId}.",
                            reason,
                            userId);

                        if (stoppingToken.IsCancellationRequested)
                        {
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Search index backfill failed for user {UserId}.", userId);
                }
                finally
                {
                    userLock.Release();
                    _queue.MarkCompleted(userId);
                }
            }
        }
    }
}
