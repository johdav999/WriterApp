using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text;
using System.Globalization;

namespace WriterApp.Application.Continuity
{
    public sealed class BiblePatchApplyResult
    {
        public BiblePatchApplyResult(string contentJson, BibleRefreshStats stats)
        {
            ContentJson = contentJson;
            Stats = stats;
        }

        public string ContentJson { get; }

        public BibleRefreshStats Stats { get; }
    }

    public sealed class BiblePatchApplier
    {
        public bool TryApply(BibleType bibleType, string existingJson, string patchOrContentJson, out BiblePatchApplyResult result)
            => TryApply(bibleType, existingJson, patchOrContentJson, out result, out _);

        public bool TryApply(
            BibleType bibleType,
            string existingJson,
            string patchOrContentJson,
            out BiblePatchApplyResult result,
            out string failureReason)
        {
            result = new BiblePatchApplyResult(existingJson, BibleJson.EmptyStats());
            failureReason = string.Empty;
            if (string.IsNullOrWhiteSpace(patchOrContentJson))
            {
                failureReason = "Payload was empty.";
                return false;
            }

            JsonObject root = ParseOrEmpty(existingJson, bibleType);

            try
            {
                if (!TryParseCandidateObject(patchOrContentJson, out JsonObject? candidate, out string parseFailureReason) || candidate is null)
                {
                    failureReason = parseFailureReason;
                    return false;
                }

                NormalizeCandidate(candidate, bibleType);

                if (candidate.TryGetPropertyValue("ops", out JsonNode? opsNode) && opsNode is JsonArray ops)
                {
                    BibleRefreshStats stats = ApplyOps(bibleType, root, ops);
                    result = new BiblePatchApplyResult(root.ToJsonString(BibleJson.JsonOptions), stats);
                    return true;
                }

                if (IsFullBiblePayload(candidate, bibleType))
                {
                    result = new BiblePatchApplyResult(candidate.ToJsonString(BibleJson.JsonOptions), new BibleRefreshStats(0, 0, 0, 0, 0, 0));
                    return true;
                }

                failureReason = BuildShapeFailureReason(candidate);
                return false;
            }
            catch (JsonException ex)
            {
                failureReason = BuildJsonExceptionReason("Patch application failed while reading JSON.", ex);
                return false;
            }
        }

        private static bool TryParseCandidateObject(string payload, out JsonObject? candidate, out string failureReason)
        {
            candidate = null;
            failureReason = string.Empty;
            if (string.IsNullOrWhiteSpace(payload))
            {
                failureReason = "Payload was empty.";
                return false;
            }

            JsonNode? parsed = TryParseNode(payload, out JsonException? parseException);
            if (parsed is JsonObject parsedObject)
            {
                candidate = parsedObject;
                return true;
            }

            // Some providers still wrap strict JSON in markdown fences or extra text.
            string trimmed = payload.Trim();
            int start = trimmed.IndexOf('{');
            int end = trimmed.LastIndexOf('}');
            if (start >= 0 && end > start)
            {
                string objectSlice = trimmed.Substring(start, end - start + 1);
                JsonNode? sliced = TryParseNode(objectSlice, out JsonException? slicedException);
                if (sliced is JsonObject slicedObject)
                {
                    candidate = slicedObject;
                    return true;
                }

                parseException ??= slicedException;
            }

            failureReason = parseException is not null
                ? BuildJsonExceptionReason("JSON could not be parsed into an object.", parseException)
                : $"Payload was not a JSON object. {DescribeTopLevelKeys(null)}";
            return false;
        }

        private static JsonNode? TryParseNode(string json, out JsonException? failure)
        {
            failure = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            JsonDocumentOptions options = new()
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            };

            try
            {
                return JsonNode.Parse(json, documentOptions: options);
            }
            catch (JsonException ex)
            {
                failure = ex;
                // Retry once with lightweight cleanup for common model formatting mistakes.
                string normalized = NormalizeJsonCandidate(json);
                if (string.Equals(normalized, json, StringComparison.Ordinal))
                {
                    return null;
                }

                try
                {
                    return JsonNode.Parse(normalized, documentOptions: options);
                }
                catch (JsonException retryEx)
                {
                    failure = retryEx;
                    return null;
                }
            }
        }

        private static string NormalizeJsonCandidate(string json)
        {
            string normalized = json
                .Replace('\u201C', '"')
                .Replace('\u201D', '"')
                .Replace('\u2018', '\'')
                .Replace('\u2019', '\'');

            return RemoveTrailingCommas(normalized);
        }

        private static string RemoveTrailingCommas(string value)
        {
            StringBuilder builder = new(value.Length);
            bool inString = false;
            bool escaping = false;

            for (int i = 0; i < value.Length; i++)
            {
                char ch = value[i];

                if (inString)
                {
                    builder.Append(ch);
                    if (escaping)
                    {
                        escaping = false;
                    }
                    else if (ch == '\\')
                    {
                        escaping = true;
                    }
                    else if (ch == '"')
                    {
                        inString = false;
                    }

                    continue;
                }

                if (ch == '"')
                {
                    inString = true;
                    builder.Append(ch);
                    continue;
                }

                if (ch != ',')
                {
                    builder.Append(ch);
                    continue;
                }

                int lookAhead = i + 1;
                while (lookAhead < value.Length && char.IsWhiteSpace(value[lookAhead]))
                {
                    lookAhead++;
                }

                if (lookAhead < value.Length && (value[lookAhead] == '}' || value[lookAhead] == ']'))
                {
                    continue;
                }

                builder.Append(ch);
            }

            return builder.ToString();
        }

        private static void NormalizeCandidate(JsonObject candidate, BibleType bibleType)
        {
            if (candidate["ops"] is null && candidate["operations"] is JsonArray operations)
            {
                candidate["ops"] = operations.DeepClone();
            }

            string collectionKey = ResolveCollectionKey(bibleType);
            if (candidate[collectionKey] is JsonArray)
            {
                return;
            }

            switch (bibleType)
            {
                case BibleType.Character:
                    PromoteCollectionAlias(candidate, collectionKey, "people");
                    PromoteCollectionAlias(candidate, collectionKey, "characterEntries");
                    break;
                case BibleType.Place:
                    PromoteCollectionAlias(candidate, collectionKey, "locations");
                    PromoteCollectionAlias(candidate, collectionKey, "placeEntries");
                    break;
                case BibleType.Timeline:
                    PromoteCollectionAlias(candidate, collectionKey, "timelineEvents");
                    if (candidate[collectionKey] is not JsonArray
                        && candidate["timeline"] is JsonObject timeline
                        && timeline["events"] is JsonArray nestedEvents)
                    {
                        candidate[collectionKey] = nestedEvents.DeepClone();
                    }

                    break;
            }
        }

        private static void PromoteCollectionAlias(JsonObject candidate, string collectionKey, string aliasKey)
        {
            if (candidate[collectionKey] is JsonArray)
            {
                return;
            }

            if (candidate[aliasKey] is JsonArray aliasArray)
            {
                candidate[collectionKey] = aliasArray.DeepClone();
            }
        }

        private static BibleRefreshStats ApplyOps(BibleType bibleType, JsonObject root, JsonArray ops)
        {
            string collectionKey = ResolveCollectionKey(bibleType);
            JsonArray collection = root[collectionKey] as JsonArray ?? new JsonArray();
            root[collectionKey] = collection;
            int newEntries = 0;
            int updatedEntries = 0;
            int flags = 0;

            foreach (JsonNode? node in ops)
            {
                if (node is not JsonObject op)
                {
                    continue;
                }

                string opName = op["op"]?.GetValue<string>() ?? string.Empty;
                if (opName.StartsWith("upsert", StringComparison.OrdinalIgnoreCase))
                {
                    JsonObject? data = op["data"] as JsonObject;
                    if (data is null)
                    {
                        continue;
                    }

                    string prefix = bibleType switch
                    {
                        BibleType.Character => "chr_",
                        BibleType.Place => "plc_",
                        _ => "evt_"
                    };

                    string id = EnsureEntryId(data, prefix);
                    int index = FindById(collection, id);
                    if (index >= 0)
                    {
                        collection[index] = data.DeepClone();
                        updatedEntries++;
                    }
                    else
                    {
                        collection.Add(data.DeepClone());
                        newEntries++;
                    }
                }
                else if (string.Equals(opName, "mergeCharacterFacts", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(opName, "mergePlaceFacts", StringComparison.OrdinalIgnoreCase))
                {
                    string id = op["id"]?.GetValue<string>() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(id))
                    {
                        continue;
                    }

                    int index = FindById(collection, id);
                    if (index < 0 || collection[index] is not JsonObject entry)
                    {
                        continue;
                    }

                    JsonArray facts = entry["facts"] as JsonArray ?? new JsonArray();
                    entry["facts"] = facts;
                    JsonArray? addFacts = op["addFacts"] as JsonArray;
                    if (addFacts is null)
                    {
                        continue;
                    }

                    foreach (JsonNode? fact in addFacts)
                    {
                        if (fact is not null)
                        {
                            facts.Add(fact.DeepClone());
                        }
                    }

                    updatedEntries++;
                }
                else if (string.Equals(opName, "flagReview", StringComparison.OrdinalIgnoreCase))
                {
                    JsonArray flagArray = root["flags"] as JsonArray ?? new JsonArray();
                    root["flags"] = flagArray;
                    flagArray.Add(op.DeepClone());
                    flags++;
                }
            }

            return new BibleRefreshStats(0, 0, 0, newEntries, updatedEntries, flags);
        }

        private static JsonObject ParseOrEmpty(string? json, BibleType bibleType)
        {
            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    JsonObject? parsed = JsonNode.Parse(json) as JsonObject;
                    if (parsed is not null)
                    {
                        return parsed;
                    }
                }
                catch (JsonException)
                {
                }
            }

            return JsonNode.Parse(BibleJson.EmptyBibleContent(bibleType)) as JsonObject ?? new JsonObject();
        }

        private static bool IsFullBiblePayload(JsonObject payload, BibleType bibleType)
        {
            string key = ResolveCollectionKey(bibleType);
            return payload[key] is JsonArray;
        }

        private static string BuildShapeFailureReason(JsonObject? candidate)
        {
            return $"JSON object did not match a supported bible payload shape. {DescribeTopLevelKeys(candidate)} {DescribeNodeType(candidate, "ops", "$.ops")} {DescribeNodeType(candidate, "characters", "$.characters")} {DescribeNodeType(candidate, "places", "$.places")} {DescribeNodeType(candidate, "events", "$.events")}";
        }

        private static string DescribeTopLevelKeys(JsonObject? candidate)
        {
            if (candidate is null)
            {
                return "Top-level keys: <unavailable>.";
            }

            string[] keys = candidate.Select(property => property.Key).OrderBy(key => key, StringComparer.Ordinal).ToArray();
            return keys.Length == 0
                ? "Top-level keys: <none>."
                : $"Top-level keys: [{string.Join(", ", keys)}].";
        }

        private static string DescribeNodeType(JsonObject? candidate, string propertyName, string path)
        {
            if (candidate is null || !candidate.TryGetPropertyValue(propertyName, out JsonNode? node))
            {
                return $"{path} missing.";
            }

            return node switch
            {
                JsonArray => $"{path} is array.",
                JsonObject => $"{path} is object.",
                JsonValue value => $"{path} is value ({DescribeValue(value)}).",
                null => $"{path} is null.",
                _ => $"{path} is {node.GetType().Name}."
            };
        }

        private static string DescribeValue(JsonValue value)
        {
            JsonElement element = value.GetValue<JsonElement>();
            return element.ValueKind switch
            {
                JsonValueKind.String => $"string '{Truncate(element.GetString())}'",
                JsonValueKind.Number => $"number {element.GetRawText()}",
                JsonValueKind.True => "boolean true",
                JsonValueKind.False => "boolean false",
                JsonValueKind.Null => "null",
                _ => element.ValueKind.ToString()
            };
        }

        private static string BuildJsonExceptionReason(string prefix, JsonException ex)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{prefix} {ex.Message} Line={ex.LineNumber?.ToString(CultureInfo.InvariantCulture) ?? "?"} BytePositionInLine={ex.BytePositionInLine?.ToString(CultureInfo.InvariantCulture) ?? "?"}.");
        }

        private static string? Truncate(string? value, int maxLength = 80)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            string normalized = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
            return normalized.Length <= maxLength ? normalized : normalized[..maxLength] + "...";
        }

        private static string ResolveCollectionKey(BibleType bibleType)
        {
            return bibleType switch
            {
                BibleType.Character => "characters",
                BibleType.Place => "places",
                _ => "events"
            };
        }

        private static int FindById(JsonArray collection, string id)
        {
            for (int i = 0; i < collection.Count; i++)
            {
                if (collection[i] is JsonObject item
                    && string.Equals(item["id"]?.GetValue<string>(), id, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private static string EnsureEntryId(JsonObject entry, string prefix)
        {
            string id = entry["id"]?.GetValue<string>() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(id))
            {
                return id.Trim();
            }

            id = $"{prefix}{Guid.NewGuid():N}";
            entry["id"] = id;
            return id;
        }
    }
}
