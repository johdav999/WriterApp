using System;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using WriterApp.Data;
using WriterApp.Data.Admin;
using WriterApp.Shared;

namespace WriterApp.Application.Users
{
    public sealed class AdminAuditService
    {
        private readonly AppDbContext _dbContext;
        private readonly ILogger<AdminAuditService> _logger;

        public AdminAuditService(AppDbContext dbContext)
            : this(dbContext, NullLogger<AdminAuditService>.Instance)
        {
        }

        public AdminAuditService(AppDbContext dbContext, ILogger<AdminAuditService> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task WriteAsync(
            string adminUserId,
            string? adminEmail,
            string action,
            string? targetUserId,
            string? targetEmail,
            object? details,
            CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            string? detailsJson = details is null ? null : JsonSerializer.Serialize(details);
            AdminAuditEvent audit = new()
            {
                OccurredAtUtc = DateTime.UtcNow,
                AdminUserId = string.IsNullOrWhiteSpace(adminUserId) ? "admin" : adminUserId,
                AdminEmail = adminEmail,
                Action = action,
                TargetUserId = targetUserId,
                TargetEmail = targetEmail,
                DetailsJson = detailsJson
            };

            try
            {
                _dbContext.AdminAuditEvents.Add(audit);
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

                LogAuditWriteFailure(ex, action, targetUserId, detailsJson, isSchemaMismatch: IsSchemaMismatch(ex));
            }
            catch (SqliteException ex)
            {
                if (ct.IsCancellationRequested)
                {
                    throw new OperationCanceledException(ct);
                }

                LogAuditWriteFailure(ex, action, targetUserId, detailsJson, isSchemaMismatch: IsSchemaMismatch(ex));
            }
            catch (Exception ex)
            {
                if (ct.IsCancellationRequested)
                {
                    throw new OperationCanceledException(ct);
                }

                LogAuditWriteFailure(ex, action, targetUserId, detailsJson, isSchemaMismatch: IsSchemaMismatch(ex));
            }
        }

        public async Task<AdminAuditListResponseDto> QueryAsync(
            AdminAuditQueryDto query,
            CancellationToken ct = default)
        {
            int page = query.Page <= 0 ? 1 : query.Page;
            int pageSize = query.PageSize <= 0 ? 50 : Math.Min(query.PageSize, 200);

            IQueryable<AdminAuditEvent> source = _dbContext.AdminAuditEvents.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(query.AdminUserId))
            {
                string adminUserId = query.AdminUserId.Trim();
                source = source.Where(item => item.AdminUserId == adminUserId);
            }

            if (!string.IsNullOrWhiteSpace(query.TargetUserId))
            {
                string targetUserId = query.TargetUserId.Trim();
                source = source.Where(item => item.TargetUserId == targetUserId);
            }

            if (!string.IsNullOrWhiteSpace(query.Action))
            {
                string action = query.Action.Trim();
                source = source.Where(item => item.Action == action);
            }

            if (query.FromUtc.HasValue)
            {
                DateTime from = query.FromUtc.Value;
                source = source.Where(item => item.OccurredAtUtc >= from);
            }

            if (query.ToUtc.HasValue)
            {
                DateTime to = query.ToUtc.Value;
                source = source.Where(item => item.OccurredAtUtc <= to);
            }

            source = source.OrderByDescending(item => item.OccurredAtUtc).ThenByDescending(item => item.Id);
            int totalCount = await source.CountAsync(ct);
            AdminAuditEventDto[] items = await source
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(item => new AdminAuditEventDto(
                    item.Id,
                    item.OccurredAtUtc,
                    item.AdminUserId,
                    item.AdminEmail,
                    item.Action,
                    item.TargetUserId,
                    item.TargetEmail,
                    item.DetailsJson))
                .ToArrayAsync(ct);

            return new AdminAuditListResponseDto(items, page, pageSize, totalCount);
        }

        private void LogAuditWriteFailure(
            Exception ex,
            string action,
            string? targetUserId,
            string? detailsJson,
            bool isSchemaMismatch)
        {
            string? traceId = Activity.Current?.TraceId.ToString();
            string? spanId = Activity.Current?.SpanId.ToString();
            int detailsBytes = detailsJson is null ? 0 : detailsJson.Length;

            if (isSchemaMismatch)
            {
                _logger.LogWarning(
                    ex,
                    "Admin audit write skipped due to schema mismatch. action={Action} targetUserId={TargetUserId} traceId={TraceId} spanId={SpanId} detailsBytes={DetailsBytes}",
                    action,
                    targetUserId,
                    traceId,
                    spanId,
                    detailsBytes);
                return;
            }

            _logger.LogWarning(
                ex,
                "Admin audit write failed (best-effort). action={Action} targetUserId={TargetUserId} traceId={TraceId} spanId={SpanId} detailsBytes={DetailsBytes}",
                action,
                targetUserId,
                traceId,
                spanId,
                detailsBytes);
        }

        private static bool IsSchemaMismatch(Exception ex)
        {
            Exception? current = ex;
            while (current is not null)
            {
                if (current is SqliteException sqliteEx
                    && sqliteEx.Message.Contains("no such table", StringComparison.OrdinalIgnoreCase)
                    && sqliteEx.Message.Contains("AdminAuditEvents", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (current.Message.Contains("no such table", StringComparison.OrdinalIgnoreCase)
                    && current.Message.Contains("AdminAuditEvents", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                current = current.InnerException;
            }

            return false;
        }
    }
}
