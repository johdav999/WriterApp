using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WriterApp.Data;
using WriterApp.Data.Exporting;

namespace WriterApp.Application.Exporting
{
    public interface IExportTemplateSeeder
    {
        Task EnsureDefaultsAsync(string ownerUserId, CancellationToken ct);
    }

    public sealed class ExportTemplateSeeder : IExportTemplateSeeder
    {
        private readonly AppDbContext _dbContext;
        private readonly ILogger<ExportTemplateSeeder> _logger;

        public ExportTemplateSeeder(AppDbContext dbContext, ILogger<ExportTemplateSeeder> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task EnsureDefaultsAsync(string ownerUserId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(ownerUserId))
            {
                return;
            }

            List<string?> existingKeys = await _dbContext.ExportTemplates
                .Where(template => template.OwnerUserId == ownerUserId && template.PresetKey != null)
                .Select(template => template.PresetKey)
                .ToListAsync(ct);

            HashSet<string> keySet = new(existingKeys
                    .Where(key => !string.IsNullOrWhiteSpace(key))
                    .Select(key => key!),
                StringComparer.OrdinalIgnoreCase);
            DateTimeOffset now = DateTimeOffset.UtcNow;

            List<ExportTemplate> defaults = new();
            foreach (ExportTemplate template in ExportTemplateDefaults.BuildDefaults(ownerUserId, now))
            {
                if (template.PresetKey is null || keySet.Contains(template.PresetKey))
                {
                    continue;
                }

                defaults.Add(template);
            }

            if (defaults.Count == 0)
            {
                return;
            }

            _dbContext.ExportTemplates.AddRange(defaults);
            int saved = await _dbContext.SaveChangesAsync(ct);
            _logger.LogInformation(
                "Seeded {Count} export templates for user {UserId} (saved {Saved}).",
                defaults.Count,
                ownerUserId,
                saved);
        }
    }
}
