using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WriterApp.AI.Abstractions;
using WriterApp.Application.Commands;
using WriterApp.Application.State;
using WriterApp.Domain.Documents;

namespace WriterApp.AI.Actions
{
    public abstract class ContinuityActionBase : IAiAction
    {
        protected ContinuityActionBase(string actionId, string displayName)
        {
            ActionIdValue = actionId;
            DisplayNameValue = displayName;
        }

        public string ActionId => ActionIdValue;

        public string DisplayName => DisplayNameValue;

        public AiModality[] Modalities => new[] { AiModality.Text };

        public bool RequiresSelection => false;

        protected string ActionIdValue { get; }

        protected string DisplayNameValue { get; }

        public abstract AiRequest BuildRequest(AiActionInput input);

        protected static string GetOption(Dictionary<string, object?>? options, string key)
        {
            if (options is null || !options.TryGetValue(key, out object? value) || value is null)
            {
                return string.Empty;
            }

            return value.ToString() ?? string.Empty;
        }

        protected static bool HasOption(Dictionary<string, object?>? options, string key)
            => !string.IsNullOrWhiteSpace(GetOption(options, key));

        protected static string BuildSectionContext(Document document, int maxSectionChars = 2200, int maxSections = 40)
        {
            IEnumerable<Section> sections = document.Chapters
                .SelectMany(chapter => chapter.Sections)
                .OrderBy(section => section.Order)
                .Take(maxSections);

            StringBuilder builder = new();
            foreach (Section section in sections)
            {
                string plain = PlainTextMapper.ToPlainText(section.Content.Value ?? string.Empty).Trim();
                if (plain.Length > maxSectionChars)
                {
                    plain = plain.Substring(0, maxSectionChars);
                }

                builder.Append("SectionId: ");
                builder.Append(section.SectionId);
                builder.Append(" | Title: ");
                builder.AppendLine(string.IsNullOrWhiteSpace(section.Title) ? "Untitled" : section.Title.Trim());
                builder.AppendLine(plain);
                builder.AppendLine();
            }

            return builder.ToString().Trim();
        }
    }

    public sealed class ExtractCharacterBibleAction : ContinuityActionBase
    {
        public new const string ActionIdValue = "continuity.extract_character_bible";

        public ExtractCharacterBibleAction()
            : base(ActionIdValue, "Extract character bible")
        {
        }

        public override AiRequest BuildRequest(AiActionInput input)
        {
            if (input is null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            string context = BuildSectionContext(input.Document);
            bool repairMode = HasOption(input.Options, "repair_invalid_json");
            string repairPayload = GetOption(input.Options, "invalid_json_payload");
            string repairFailureReason = GetOption(input.Options, "invalid_json_failure_reason");
            Dictionary<string, object> inputs = new()
            {
                ["instruction"] = repairMode
                    ? "Re-emit the previous character bible output as one valid JSON object only."
                    : "Extract a character bible from manuscript context. Return valid JSON only with no prose, markdown, or commentary.",
                ["context"] = context,
                ["output_contract"] = "Return strict JSON only: {\"schemaVersion\":\"1.0\",\"characters\":[{\"name\":\"...\",\"facts\":[{\"fact\":\"...\",\"evidence\":{\"sectionId\":\"<guid>\",\"quote\":\"...\"}}],\"traits\":[\"...\"]}]}"
            };
            if (repairMode)
            {
                inputs["invalid_json_payload"] = repairPayload;
                inputs["invalid_json_failure_reason"] = repairFailureReason;
            }

            return new AiRequest(
                Guid.NewGuid(),
                ActionId,
                Modalities,
                new AiRequestContext(
                    input.Document.DocumentId,
                    input.ActiveSectionId,
                    new TextRange(0, 0),
                    context,
                    input.Document.Metadata.Title,
                    null,
                    null,
                    input.Document.Metadata.Language,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null),
                inputs,
                new Dictionary<string, object>(),
                new Dictionary<string, object>());
        }
    }

    public sealed class ExtractPlaceBibleAction : ContinuityActionBase
    {
        public new const string ActionIdValue = "continuity.extract_place_bible";

        public ExtractPlaceBibleAction()
            : base(ActionIdValue, "Extract place bible")
        {
        }

        public override AiRequest BuildRequest(AiActionInput input)
        {
            if (input is null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            string context = BuildSectionContext(input.Document);
            Dictionary<string, object> inputs = new()
            {
                ["instruction"] = "Extract a place bible from manuscript context.",
                ["context"] = context,
                ["output_contract"] = "Return strict JSON only: {\"schemaVersion\":\"1.0\",\"places\":[{\"name\":\"...\",\"facts\":[{\"fact\":\"...\",\"evidence\":{\"sectionId\":\"<guid>\",\"quote\":\"...\"}}]}]}"
            };

            return new AiRequest(
                Guid.NewGuid(),
                ActionId,
                Modalities,
                new AiRequestContext(
                    input.Document.DocumentId,
                    input.ActiveSectionId,
                    new TextRange(0, 0),
                    context,
                    input.Document.Metadata.Title,
                    null,
                    null,
                    input.Document.Metadata.Language,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null),
                inputs,
                new Dictionary<string, object>(),
                new Dictionary<string, object>());
        }
    }

    public sealed class ExtractTimelineBibleAction : ContinuityActionBase
    {
        public new const string ActionIdValue = "continuity.extract_timeline_bible";

        public ExtractTimelineBibleAction()
            : base(ActionIdValue, "Extract timeline bible")
        {
        }

        public override AiRequest BuildRequest(AiActionInput input)
        {
            if (input is null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            string context = BuildSectionContext(input.Document);
            Dictionary<string, object> inputs = new()
            {
                ["instruction"] = "Extract a timeline bible from manuscript context.",
                ["context"] = context,
                ["output_contract"] = "Return strict JSON only: {\"schemaVersion\":\"1.0\",\"events\":[{\"id\":\"evt_...\",\"title\":\"...\",\"timeRef\":\"...\",\"order\":1,\"locationId\":\"\",\"participants\":[\"chr_...\"],\"summary\":\"...\",\"evidence\":[{\"sectionId\":\"<guid>\",\"quote\":\"...\"}],\"constraints\":[\"...\"],\"lastUpdatedUtc\":\"...\"}]}"
            };

            return new AiRequest(
                Guid.NewGuid(),
                ActionId,
                Modalities,
                new AiRequestContext(
                    input.Document.DocumentId,
                    input.ActiveSectionId,
                    new TextRange(0, 0),
                    context,
                    input.Document.Metadata.Title,
                    null,
                    null,
                    input.Document.Metadata.Language,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null),
                inputs,
                new Dictionary<string, object>(),
                new Dictionary<string, object>());
        }
    }

    public sealed class RefreshCharacterBibleAction : ContinuityActionBase
    {
        public new const string ActionIdValue = "continuity.refresh_character_bible";

        public RefreshCharacterBibleAction()
            : base(ActionIdValue, "Refresh character bible")
        {
        }

        public override AiRequest BuildRequest(AiActionInput input)
        {
            if (input is null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            string existingJson = GetOption(input.Options, "existing_bible_json");
            string deltaJson = GetOption(input.Options, "delta_sections_json");
            string fullRebuild = GetOption(input.Options, "full_rebuild");
            bool repairMode = HasOption(input.Options, "repair_invalid_json");
            string repairPayload = GetOption(input.Options, "invalid_json_payload");
            string repairFailureReason = GetOption(input.Options, "invalid_json_failure_reason");

            Dictionary<string, object> inputs = new()
            {
                ["instruction"] = repairMode
                    ? "Re-emit the previous character bible refresh result as one valid JSON object only."
                    : "Update character bible incrementally from changed sections. Return valid JSON only with no prose, markdown, or commentary.",
                ["existing_bible_json"] = existingJson,
                ["delta_sections_json"] = deltaJson,
                ["full_rebuild"] = fullRebuild,
                ["output_contract"] = "Return strict JSON patch only: {\"bibleType\":\"Character\",\"schemaVersion\":1,\"ops\":[{\"op\":\"upsertCharacter\",\"id\":\"chr_...\",\"data\":{...}},{\"op\":\"mergeCharacterFacts\",\"id\":\"chr_...\",\"addFacts\":[...]},{\"op\":\"flagReview\",\"target\":{\"type\":\"character\",\"id\":\"chr_...\"},\"reason\":\"...\"}],\"stats\":{\"updatedEntries\":0,\"newEntries\":0,\"flags\":0}}"
            };
            if (repairMode)
            {
                inputs["invalid_json_payload"] = repairPayload;
                inputs["invalid_json_failure_reason"] = repairFailureReason;
            }

            return new AiRequest(
                Guid.NewGuid(),
                ActionId,
                Modalities,
                new AiRequestContext(
                    input.Document.DocumentId,
                    input.ActiveSectionId,
                    new TextRange(0, 0),
                    deltaJson,
                    input.Document.Metadata.Title,
                    null,
                    null,
                    input.Document.Metadata.Language,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null),
                inputs,
                new Dictionary<string, object>(),
                new Dictionary<string, object>());
        }
    }

    public sealed class RefreshPlaceBibleAction : ContinuityActionBase
    {
        public new const string ActionIdValue = "continuity.refresh_place_bible";

        public RefreshPlaceBibleAction()
            : base(ActionIdValue, "Refresh place bible")
        {
        }

        public override AiRequest BuildRequest(AiActionInput input)
        {
            if (input is null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            string existingJson = GetOption(input.Options, "existing_bible_json");
            string deltaJson = GetOption(input.Options, "delta_sections_json");
            string fullRebuild = GetOption(input.Options, "full_rebuild");

            Dictionary<string, object> inputs = new()
            {
                ["instruction"] = "Update place bible incrementally from changed sections.",
                ["existing_bible_json"] = existingJson,
                ["delta_sections_json"] = deltaJson,
                ["full_rebuild"] = fullRebuild,
                ["output_contract"] = "Return strict JSON patch only: {\"bibleType\":\"Place\",\"schemaVersion\":1,\"ops\":[{\"op\":\"upsertPlace\",\"id\":\"plc_...\",\"data\":{...}},{\"op\":\"mergePlaceFacts\",\"id\":\"plc_...\",\"addFacts\":[...]},{\"op\":\"flagReview\",\"target\":{\"type\":\"place\",\"id\":\"plc_...\"},\"reason\":\"...\"}],\"stats\":{\"updatedEntries\":0,\"newEntries\":0,\"flags\":0}}"
            };

            return new AiRequest(
                Guid.NewGuid(),
                ActionId,
                Modalities,
                new AiRequestContext(
                    input.Document.DocumentId,
                    input.ActiveSectionId,
                    new TextRange(0, 0),
                    deltaJson,
                    input.Document.Metadata.Title,
                    null,
                    null,
                    input.Document.Metadata.Language,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null),
                inputs,
                new Dictionary<string, object>(),
                new Dictionary<string, object>());
        }
    }

    public sealed class RefreshTimelineBibleAction : ContinuityActionBase
    {
        public new const string ActionIdValue = "continuity.refresh_timeline_bible";

        public RefreshTimelineBibleAction()
            : base(ActionIdValue, "Refresh timeline bible")
        {
        }

        public override AiRequest BuildRequest(AiActionInput input)
        {
            if (input is null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            string existingJson = GetOption(input.Options, "existing_bible_json");
            string deltaJson = GetOption(input.Options, "delta_sections_json");
            string fullRebuild = GetOption(input.Options, "full_rebuild");

            Dictionary<string, object> inputs = new()
            {
                ["instruction"] = "Update timeline bible incrementally from changed sections.",
                ["existing_bible_json"] = existingJson,
                ["delta_sections_json"] = deltaJson,
                ["full_rebuild"] = fullRebuild,
                ["output_contract"] = "Return strict JSON patch only: {\"bibleType\":\"Timeline\",\"schemaVersion\":1,\"ops\":[{\"op\":\"upsertTimelineEvent\",\"id\":\"evt_...\",\"data\":{...}},{\"op\":\"flagReview\",\"target\":{\"type\":\"timeline\",\"id\":\"evt_...\"},\"reason\":\"...\"}],\"stats\":{\"updatedEntries\":0,\"newEntries\":0,\"flags\":0}}"
            };

            return new AiRequest(
                Guid.NewGuid(),
                ActionId,
                Modalities,
                new AiRequestContext(
                    input.Document.DocumentId,
                    input.ActiveSectionId,
                    new TextRange(0, 0),
                    deltaJson,
                    input.Document.Metadata.Title,
                    null,
                    null,
                    input.Document.Metadata.Language,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null),
                inputs,
                new Dictionary<string, object>(),
                new Dictionary<string, object>());
        }
    }

    public sealed class ContinuityCheckAction : ContinuityActionBase
    {
        public new const string ActionIdValue = "continuity.check_section";

        public ContinuityCheckAction()
            : base(ActionIdValue, "Check continuity")
        {
        }

        public override AiRequest BuildRequest(AiActionInput input)
        {
            if (input is null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            string sectionText = ResolveSectionText(input.Document, input.ActiveSectionId);
            string characterBibleJson = GetOption(input.Options, "character_bible_json");
            string placeBibleJson = GetOption(input.Options, "place_bible_json");
            string timelineBibleJson = GetOption(input.Options, "timeline_bible_json");

            Dictionary<string, object> inputs = new()
            {
                ["instruction"] = "Run continuity consistency checks for this section and propose minimal rewrite fixes for anchored spans.",
                ["section_text"] = sectionText,
                ["character_bible_json"] = characterBibleJson,
                ["place_bible_json"] = placeBibleJson,
                ["timeline_bible_json"] = timelineBibleJson,
                ["output_contract"] = "Return strict JSON only: {\"schemaVersion\":\"1.0\",\"issues\":[{\"severity\":\"low|medium|high|critical\",\"type\":\"character|place|timeline\",\"message\":\"...\",\"evidence\":{\"sectionId\":\"<guid>\",\"quote\":\"...\"},\"suggestedFix\":\"<revised narrative prose for the anchored span only>\",\"anchor\":{\"plainTextStart\":0,\"plainTextLength\":10}}]}"
            };

            return new AiRequest(
                Guid.NewGuid(),
                ActionId,
                Modalities,
                new AiRequestContext(
                    input.Document.DocumentId,
                    input.ActiveSectionId,
                    new TextRange(0, sectionText.Length),
                    sectionText,
                    input.Document.Metadata.Title,
                    null,
                    null,
                    input.Document.Metadata.Language,
                    sectionText,
                    0,
                    sectionText.Length,
                    null,
                    null,
                    null),
                inputs,
                new Dictionary<string, object>(),
                new Dictionary<string, object>());
        }

        private static string ResolveSectionText(Document document, Guid sectionId)
        {
            foreach (Chapter chapter in document.Chapters)
            {
                foreach (Section section in chapter.Sections)
                {
                    if (section.SectionId != sectionId)
                    {
                        continue;
                    }

                    return PlainTextMapper.ToPlainText(section.Content.Value ?? string.Empty);
                }
            }

            return string.Empty;
        }

    }

    public sealed class ApplyContinuityFixAction : ContinuityActionBase
    {
        public new const string ActionIdValue = "continuity.apply_fix";

        public ApplyContinuityFixAction()
            : base(ActionIdValue, "Apply continuity fix")
        {
        }

        public override AiRequest BuildRequest(AiActionInput input)
        {
            if (input is null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            string suggestedFix = GetOption(input.Options, "suggested_fix");
            string anchorStart = GetOption(input.Options, "anchor_start");
            string anchorLength = GetOption(input.Options, "anchor_length");

            Dictionary<string, object> inputs = new()
            {
                ["instruction"] = "Apply the provided continuity rewrite at the anchored range. The fix text must be revised narrative prose only.",
                ["suggested_fix"] = suggestedFix,
                ["anchor_start"] = anchorStart,
                ["anchor_length"] = anchorLength
            };

            return new AiRequest(
                Guid.NewGuid(),
                ActionId,
                Modalities,
                new AiRequestContext(
                    input.Document.DocumentId,
                    input.ActiveSectionId,
                    input.SelectionRange,
                    input.SelectedText ?? string.Empty,
                    input.Document.Metadata.Title,
                    null,
                    null,
                    input.Document.Metadata.Language,
                    input.SelectedText,
                    input.SelectionRange.Start,
                    input.SelectionRange.Length,
                    null,
                    null,
                    null),
                inputs,
                new Dictionary<string, object>(),
                new Dictionary<string, object>());
        }

    }
}
