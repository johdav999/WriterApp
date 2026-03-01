using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WriterApp.Data;
using WriterApp.Data.Exporting;

namespace WriterApp.Application.Exporting
{
    public interface IExportTemplateResolver
    {
        Task<ExportTemplate> ResolveAsync(string ownerUserId, Guid? templateId, CancellationToken ct);
    }

    public sealed class ExportTemplateResolver : IExportTemplateResolver
    {
        private readonly AppDbContext _dbContext;
        private readonly IExportTemplateSeeder _seeder;
        private readonly ILogger<ExportTemplateResolver> _logger;

        public ExportTemplateResolver(
            AppDbContext dbContext,
            IExportTemplateSeeder seeder,
            ILogger<ExportTemplateResolver> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _seeder = seeder ?? throw new ArgumentNullException(nameof(seeder));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ExportTemplate> ResolveAsync(string ownerUserId, Guid? templateId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(ownerUserId))
            {
                throw new ArgumentException("Owner user id is required.", nameof(ownerUserId));
            }

            await _seeder.EnsureDefaultsAsync(ownerUserId, ct);

            if (templateId.HasValue)
            {
                ExportTemplate? byId = await _dbContext.ExportTemplates
                    .AsNoTracking()
                    .FirstOrDefaultAsync(template => template.Id == templateId.Value && template.OwnerUserId == ownerUserId, ct);
                if (byId is null)
                {
                    throw new ExportTemplateNotFoundException(templateId.Value, ownerUserId);
                }

                return byId;
            }

            ExportTemplate? manuscript = await _dbContext.ExportTemplates
                .AsNoTracking()
                .FirstOrDefaultAsync(template => template.OwnerUserId == ownerUserId && template.PresetKey == "manuscript", ct);

            if (manuscript is null)
            {
                _logger.LogWarning("Default manuscript export template missing for user {UserId}; using fallback defaults.", ownerUserId);
                return ExportTemplateDefaults.CreateManuscript(ownerUserId, DateTimeOffset.UtcNow);
            }

            return manuscript;
        }
    }

    public sealed class ExportTemplateNotFoundException : Exception
    {
        public ExportTemplateNotFoundException(Guid templateId, string ownerUserId)
            : base($"Export template '{templateId}' not found for user '{ownerUserId}'.")
        {
            TemplateId = templateId;
            OwnerUserId = ownerUserId;
        }

        public Guid TemplateId { get; }
        public string OwnerUserId { get; }
    }
}
