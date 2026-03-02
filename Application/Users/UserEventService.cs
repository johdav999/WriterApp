using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WriterApp.Data;
using WriterApp.Data.Usage;

namespace WriterApp.Application.Users
{
    public sealed class UserEventService
    {
        private readonly AppDbContext _dbContext;
        private readonly ILogger<UserEventService> _logger;

        public UserEventService(AppDbContext dbContext, ILogger<UserEventService> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task TrackAsync(
            string userId,
            string eventName,
            object? metadata = null,
            CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            string normalizedUserId = string.IsNullOrWhiteSpace(userId) ? "unknown" : userId.Trim();
            string normalizedEventName = string.IsNullOrWhiteSpace(eventName) ? "unknown" : eventName.Trim();
            string? metadataJson = metadata is null ? null : JsonSerializer.Serialize(metadata);

            UserEvent userEvent = new()
            {
                UserId = normalizedUserId,
                EventName = normalizedEventName,
                MetadataJson = metadataJson,
                CreatedUtc = DateTimeOffset.UtcNow
            };

            try
            {
                _dbContext.UserEvents.Add(userEvent);
                await _dbContext.SaveChangesAsync(ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DbUpdateException ex)
            {
                if (ct.IsCancellationRequested)
                {
                    throw new OperationCanceledException(ct);
                }

                LogWriteFailure(ex, normalizedUserId, normalizedEventName, metadataJson);
            }
            catch (SqliteException ex)
            {
                if (ct.IsCancellationRequested)
                {
                    throw new OperationCanceledException(ct);
                }

                LogWriteFailure(ex, normalizedUserId, normalizedEventName, metadataJson);
            }
            catch (Exception ex)
            {
                if (ct.IsCancellationRequested)
                {
                    throw new OperationCanceledException(ct);
                }

                LogWriteFailure(ex, normalizedUserId, normalizedEventName, metadataJson);
            }
        }

        private void LogWriteFailure(Exception ex, string userId, string eventName, string? metadataJson)
        {
            string? traceId = Activity.Current?.TraceId.ToString();
            int metadataBytes = metadataJson?.Length ?? 0;

            _logger.LogWarning(
                ex,
                "User event write failed (best-effort). userId={UserId} eventName={EventName} traceId={TraceId} metadataBytes={MetadataBytes}",
                userId,
                eventName,
                traceId,
                metadataBytes);
        }
    }
}
