using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WriterApp.Data;
using WriterApp.Data.Usage;

namespace WriterApp.Application.Usage
{
    public sealed class UsageMeter : IUsageMeter
    {
        private const string TotalKind = "ai.total";
        private readonly AppDbContext _dbContext;
        private readonly IClock _clock;

        public UsageMeter(AppDbContext dbContext, IClock clock)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public async Task RecordAsync(UsageEvent usageEvent)
        {
            if (usageEvent is null)
            {
                throw new ArgumentNullException(nameof(usageEvent));
            }

            if (usageEvent.Id == Guid.Empty)
            {
                usageEvent.Id = Guid.NewGuid();
            }

            if (usageEvent.TimestampUtc == default)
            {
                usageEvent.TimestampUtc = _clock.UtcNow;
            }

            (DateTime periodStartUtc, DateTime periodEndUtc) = GetPeriodBounds(usageEvent.TimestampUtc);
            UsageAggregate aggregate = await GetOrCreateAggregateAsync(
                usageEvent.UserId,
                usageEvent.Kind,
                periodStartUtc,
                periodEndUtc);

            ApplyUsage(aggregate, usageEvent);

            if (!string.Equals(usageEvent.Kind, TotalKind, StringComparison.OrdinalIgnoreCase))
            {
                UsageAggregate totalAggregate = await GetOrCreateAggregateAsync(
                    usageEvent.UserId,
                    TotalKind,
                    periodStartUtc,
                    periodEndUtc);

                ApplyUsage(totalAggregate, usageEvent);
            }

            _dbContext.UsageEvents.Add(usageEvent);
            await _dbContext.SaveChangesAsync();
        }

        public async Task<UsageSnapshot> GetCurrentPeriodAsync(string userId, string kind)
        {
            (DateTime periodStartUtc, DateTime periodEndUtc) = GetPeriodBounds(_clock.UtcNow);

            UsageAggregate? aggregate = await _dbContext.UsageAggregates
                .FirstOrDefaultAsync(existing =>
                    existing.UserId == userId
                    && existing.Kind == kind
                    && existing.PeriodStartUtc == periodStartUtc
                    && existing.PeriodEndUtc == periodEndUtc);

            if (aggregate is null)
            {
                return new UsageSnapshot(
                    userId,
                    periodStartUtc,
                    periodEndUtc,
                    kind,
                    0,
                    0,
                    0,
                    _clock.UtcNow);
            }

            return new UsageSnapshot(
                aggregate.UserId,
                aggregate.PeriodStartUtc,
                aggregate.PeriodEndUtc,
                aggregate.Kind,
                aggregate.TotalInputTokens,
                aggregate.TotalOutputTokens,
                aggregate.TotalCostMicros,
                aggregate.UpdatedUtc);
        }

        private static (DateTime StartUtc, DateTime EndUtc) GetPeriodBounds(DateTime timestampUtc)
        {
            DateTime startUtc = new(timestampUtc.Year, timestampUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            DateTime endUtc = startUtc.AddMonths(1);
            return (startUtc, endUtc);
        }

        private async Task<UsageAggregate> GetOrCreateAggregateAsync(
            string userId,
            string kind,
            DateTime periodStartUtc,
            DateTime periodEndUtc)
        {
            UsageAggregate? aggregate = await _dbContext.UsageAggregates
                .FirstOrDefaultAsync(existing =>
                    existing.UserId == userId
                    && existing.Kind == kind
                    && existing.PeriodStartUtc == periodStartUtc
                    && existing.PeriodEndUtc == periodEndUtc);

            if (aggregate is not null)
            {
                return aggregate;
            }

            aggregate = new UsageAggregate
            {
                UserId = userId,
                Kind = kind,
                PeriodStartUtc = periodStartUtc,
                PeriodEndUtc = periodEndUtc,
                TotalInputTokens = 0,
                TotalOutputTokens = 0,
                TotalCostMicros = 0,
                UpdatedUtc = DateTime.UtcNow
            };

            _dbContext.UsageAggregates.Add(aggregate);
            return aggregate;
        }

        private void ApplyUsage(UsageAggregate aggregate, UsageEvent usageEvent)
        {
            aggregate.TotalInputTokens += usageEvent.InputTokens;
            aggregate.TotalOutputTokens += usageEvent.OutputTokens;
            if (usageEvent.CostMicros.HasValue)
            {
                aggregate.TotalCostMicros += usageEvent.CostMicros.Value;
            }

            aggregate.UpdatedUtc = _clock.UtcNow;
        }
    }
}
