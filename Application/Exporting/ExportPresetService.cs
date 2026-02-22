using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WriterApp.Data;
using WriterApp.Data.Exporting;

namespace WriterApp.Application.Exporting
{
    public interface IExportPresetService
    {
        Task<IReadOnlyList<ExportPreset>> ListAsync(string userId, CancellationToken ct);
        Task<ExportPreset?> GetAsync(string userId, Guid presetId, CancellationToken ct);
        Task<ExportPreset> CreateAsync(string userId, ExportPresetCreateRequest request, CancellationToken ct);
        Task<ExportPreset?> UpdateAsync(string userId, Guid presetId, ExportPresetUpdateRequest request, CancellationToken ct);
        Task<bool> DeleteAsync(string userId, Guid presetId, CancellationToken ct);
        Task<ProjectExportSettings?> GetProjectSettingsAsync(string userId, Guid documentId, CancellationToken ct);
        Task<ProjectExportSettings> SetProjectSettingsAsync(string userId, Guid documentId, ProjectExportSettingsUpdateRequest request, CancellationToken ct);
        Task<Guid?> ResolveDefaultPresetIdAsync(string userId, Guid documentId, CancellationToken ct);
    }

    public sealed class ExportPresetService : IExportPresetService
    {
        private readonly AppDbContext _dbContext;
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        public ExportPresetService(AppDbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task<IReadOnlyList<ExportPreset>> ListAsync(string userId, CancellationToken ct)
        {
            return await _dbContext.ExportPresets
                .AsNoTracking()
                .Where(preset => preset.OwnerUserId == userId)
                .OrderBy(preset => preset.Name)
                .ToListAsync(ct);
        }

        public async Task<ExportPreset?> GetAsync(string userId, Guid presetId, CancellationToken ct)
        {
            return await _dbContext.ExportPresets
                .AsNoTracking()
                .FirstOrDefaultAsync(preset => preset.Id == presetId && preset.OwnerUserId == userId, ct);
        }

        public async Task<ExportPreset> CreateAsync(string userId, ExportPresetCreateRequest request, CancellationToken ct)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            ExportPreset preset = new()
            {
                Id = Guid.NewGuid(),
                OwnerUserId = userId,
                Name = request.Name.Trim(),
                IsGlobalDefault = request.IsGlobalDefault,
                SettingsJson = JsonSerializer.Serialize(request.Settings, JsonOptions),
                CreatedAt = now,
                UpdatedAt = now
            };

            if (preset.IsGlobalDefault)
            {
                await ClearGlobalDefaultAsync(userId, ct);
            }

            _dbContext.ExportPresets.Add(preset);
            await _dbContext.SaveChangesAsync(ct);
            return preset;
        }

        public async Task<ExportPreset?> UpdateAsync(string userId, Guid presetId, ExportPresetUpdateRequest request, CancellationToken ct)
        {
            ExportPreset? preset = await _dbContext.ExportPresets
                .FirstOrDefaultAsync(item => item.Id == presetId && item.OwnerUserId == userId, ct);

            if (preset is null)
            {
                return null;
            }

            preset.Name = request.Name.Trim();
            preset.IsGlobalDefault = request.IsGlobalDefault;
            preset.SettingsJson = JsonSerializer.Serialize(request.Settings, JsonOptions);
            preset.UpdatedAt = DateTimeOffset.UtcNow;

            if (preset.IsGlobalDefault)
            {
                await ClearGlobalDefaultAsync(userId, ct, preset.Id);
            }

            await _dbContext.SaveChangesAsync(ct);
            return preset;
        }

        public async Task<bool> DeleteAsync(string userId, Guid presetId, CancellationToken ct)
        {
            ExportPreset? preset = await _dbContext.ExportPresets
                .FirstOrDefaultAsync(item => item.Id == presetId && item.OwnerUserId == userId, ct);

            if (preset is null)
            {
                return false;
            }

            List<ProjectExportSettings> settings = await _dbContext.ProjectExportSettings
                .Where(item => item.UserId == userId && item.DefaultPresetId == presetId)
                .ToListAsync(ct);

            foreach (ProjectExportSettings item in settings)
            {
                item.DefaultPresetId = null;
                item.UpdatedAt = DateTimeOffset.UtcNow;
            }

            _dbContext.ExportPresets.Remove(preset);
            await _dbContext.SaveChangesAsync(ct);
            return true;
        }

        public async Task<ProjectExportSettings?> GetProjectSettingsAsync(string userId, Guid documentId, CancellationToken ct)
        {
            return await _dbContext.ProjectExportSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.DocumentId == documentId && item.UserId == userId, ct);
        }

        public async Task<ProjectExportSettings> SetProjectSettingsAsync(
            string userId,
            Guid documentId,
            ProjectExportSettingsUpdateRequest request,
            CancellationToken ct)
        {
            ProjectExportSettings? settings = await _dbContext.ProjectExportSettings
                .FirstOrDefaultAsync(item => item.DocumentId == documentId && item.UserId == userId, ct);

            if (settings is null)
            {
                settings = new ProjectExportSettings
                {
                    DocumentId = documentId,
                    UserId = userId
                };
                _dbContext.ProjectExportSettings.Add(settings);
            }

            settings.DefaultPresetId = request.DefaultPresetId;
            settings.OverridesJson = request.Overrides is null
                ? null
                : JsonSerializer.Serialize(request.Overrides, JsonOptions);
            settings.UpdatedAt = DateTimeOffset.UtcNow;

            await _dbContext.SaveChangesAsync(ct);
            return settings;
        }

        public async Task<Guid?> ResolveDefaultPresetIdAsync(string userId, Guid documentId, CancellationToken ct)
        {
            ProjectExportSettings? settings = await _dbContext.ProjectExportSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.DocumentId == documentId && item.UserId == userId, ct);

            if (settings?.DefaultPresetId is Guid projectPresetId)
            {
                return projectPresetId;
            }

            ExportPreset? globalDefault = await _dbContext.ExportPresets
                .AsNoTracking()
                .Where(preset => preset.OwnerUserId == userId && preset.IsGlobalDefault)
                .OrderByDescending(preset => preset.UpdatedAt)
                .FirstOrDefaultAsync(ct);

            return globalDefault?.Id;
        }

        private async Task ClearGlobalDefaultAsync(string userId, CancellationToken ct, Guid? ignorePresetId = null)
        {
            List<ExportPreset> defaults = await _dbContext.ExportPresets
                .Where(preset => preset.OwnerUserId == userId && preset.IsGlobalDefault)
                .ToListAsync(ct);

            foreach (ExportPreset preset in defaults)
            {
                if (ignorePresetId.HasValue && preset.Id == ignorePresetId.Value)
                {
                    continue;
                }

                preset.IsGlobalDefault = false;
            }
        }
    }
}
