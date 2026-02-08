using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WriterApp.Application.Continuity
{
    public enum BibleType
    {
        Character,
        Place,
        Timeline
    }

    public sealed record BibleRefreshCursor(
        Dictionary<Guid, string> SectionHashes,
        DateTimeOffset LastProcessedUtc,
        string SourceStrategy);

    public sealed record BibleRefreshStats(
        int ChangedSections,
        int NewSections,
        int DeletedSections,
        int NewEntries,
        int UpdatedEntries,
        int Flags);

    public sealed record BibleSnapshotState(
        Guid Id,
        Guid DocumentId,
        BibleType BibleType,
        int SchemaVersion,
        string ContentJson,
        DateTimeOffset CreatedUtc,
        DateTimeOffset UpdatedUtc,
        DateTimeOffset? LastRefreshUtc,
        string LastRefreshSourceHash,
        BibleRefreshStats Stats,
        BibleRefreshCursor Cursor);

    public sealed record TimelineEvidence(Guid? SectionId, string Quote);

    public sealed record TimelineEventEntry(
        string Id,
        string Title,
        string TimeRef,
        int Order,
        string? LocationId,
        IReadOnlyList<string> Participants,
        string Summary,
        IReadOnlyList<TimelineEvidence> Evidence,
        IReadOnlyList<string> Constraints,
        DateTimeOffset? LastUpdatedUtc);

    public sealed record TimelineBible(string SchemaVersion, IReadOnlyList<TimelineEventEntry> Events);

    public static class BibleJson
    {
        public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public static BibleRefreshCursor EmptyCursor() =>
            new(new Dictionary<Guid, string>(), DateTimeOffset.MinValue, "bySectionHash-v1");

        public static BibleRefreshStats EmptyStats() => new(0, 0, 0, 0, 0, 0);

        public static string EmptyBibleContent(BibleType type)
        {
            return type switch
            {
                BibleType.Character => "{\"schemaVersion\":\"1.0\",\"characters\":[]}",
                BibleType.Place => "{\"schemaVersion\":\"1.0\",\"places\":[]}",
                BibleType.Timeline => "{\"schemaVersion\":\"1.0\",\"events\":[]}",
                _ => "{}"
            };
        }

        public static bool TryParseCursor(string? json, out BibleRefreshCursor cursor)
        {
            cursor = EmptyCursor();
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                BibleRefreshCursor? parsed = JsonSerializer.Deserialize<BibleRefreshCursor>(json, JsonOptions);
                if (parsed is null)
                {
                    return false;
                }

                cursor = parsed with
                {
                    SectionHashes = parsed.SectionHashes ?? new Dictionary<Guid, string>(),
                    SourceStrategy = string.IsNullOrWhiteSpace(parsed.SourceStrategy) ? "bySectionHash-v1" : parsed.SourceStrategy
                };
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        public static bool TryParseStats(string? json, out BibleRefreshStats stats)
        {
            stats = EmptyStats();
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                BibleRefreshStats? parsed = JsonSerializer.Deserialize<BibleRefreshStats>(json, JsonOptions);
                if (parsed is null)
                {
                    return false;
                }

                stats = parsed;
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        public static bool TryParseTimelineBible(string? json, out TimelineBible? bible)
        {
            bible = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                TimelineBible? parsed = JsonSerializer.Deserialize<TimelineBible>(json, JsonOptions);
                if (parsed is null)
                {
                    return false;
                }

                List<TimelineEventEntry> events = parsed.Events?.ToList() ?? new List<TimelineEventEntry>();
                bible = parsed with
                {
                    SchemaVersion = string.IsNullOrWhiteSpace(parsed.SchemaVersion) ? "1.0" : parsed.SchemaVersion.Trim(),
                    Events = events
                };
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }
    }
}
