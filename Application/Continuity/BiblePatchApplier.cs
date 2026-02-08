using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

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
        {
            result = new BiblePatchApplyResult(existingJson, BibleJson.EmptyStats());
            if (string.IsNullOrWhiteSpace(patchOrContentJson))
            {
                return false;
            }

            JsonObject root = ParseOrEmpty(existingJson, bibleType);

            try
            {
                JsonNode? candidateNode = JsonNode.Parse(patchOrContentJson);
                if (candidateNode is not JsonObject candidate)
                {
                    return false;
                }

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

                return false;
            }
            catch (JsonException)
            {
                return false;
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
