using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WriterApp.Data;
using WriterApp.Data.Documents;

namespace WriterApp.Application.Commands
{
    public sealed class UpdateSceneCardCommand : IStructureUndoCommand
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        public UpdateSceneCardCommand(
            string userId,
            Guid documentId,
            Guid sectionId,
            string beforeJson,
            string afterJson)
        {
            UserId = userId ?? throw new ArgumentNullException(nameof(userId));
            DocumentId = documentId;
            SectionId = sectionId;
            BeforeJson = beforeJson ?? "{}";
            AfterJson = afterJson ?? "{}";
        }

        public string UserId { get; }

        public Guid DocumentId { get; }

        public Guid SectionId { get; }

        public string BeforeJson { get; }

        public string AfterJson { get; }

        public async Task ExecuteAsync(AppDbContext dbContext, CancellationToken ct)
        {
            await ApplyAsync(dbContext, AfterJson, ct);
        }

        public async Task UndoAsync(AppDbContext dbContext, CancellationToken ct)
        {
            await ApplyAsync(dbContext, BeforeJson, ct);
        }

        private async Task ApplyAsync(AppDbContext dbContext, string json, CancellationToken ct)
        {
            SceneCardState state = JsonSerializer.Deserialize<SceneCardState>(json, JsonOptions) ?? new SceneCardState();
            SectionSceneCardRecord? record = await dbContext.SectionSceneCards.FindAsync(new object?[] { SectionId }, ct);
            if (record is null)
            {
                record = new SectionSceneCardRecord { SectionId = SectionId };
                dbContext.SectionSceneCards.Add(record);
            }

            record.NarrativePurpose = state.NarrativePurpose ?? string.Empty;
            record.EmotionalBeat = state.EmotionalBeat ?? string.Empty;
            record.KeyEvents = state.KeyEvents ?? string.Empty;
            record.OpenQuestions = state.OpenQuestions ?? string.Empty;
            record.Summary = state.Summary;
            record.Status = state.Status ?? "Draft";
            record.PovCharacterId = state.PovCharacterId;
            record.PlaceId = state.PlaceId;
            record.TimelineEventId = state.TimelineEventId;
            record.TimeRef = state.TimeRef;
            record.TagsJson = state.TagsJson;
            record.SubplotTagsJson = state.SubplotTagsJson;
            record.ReferencesJson = state.ReferencesJson;
            record.UpdatedUtc = DateTimeOffset.UtcNow;
        }

        public sealed class SceneCardState
        {
            public string? NarrativePurpose { get; set; }
            public string? EmotionalBeat { get; set; }
            public string? KeyEvents { get; set; }
            public string? OpenQuestions { get; set; }
            public string? Summary { get; set; }
            public string? Status { get; set; }
            public string? PovCharacterId { get; set; }
            public string? PlaceId { get; set; }
            public string? TimelineEventId { get; set; }
            public string? TimeRef { get; set; }
            public string? TagsJson { get; set; }
            public string? SubplotTagsJson { get; set; }
            public string? ReferencesJson { get; set; }
        }
    }
}
