namespace WriterApp.Shared;
        public static class PromptStrategyResolver
        {
        public static string NormalizeIntent(string? raw)
        {
            string normalized = (raw ?? string.Empty).Trim().ToLowerInvariant();
            return normalized switch
            {
                "novel" => "Novel",
                "short story" => "ShortStory",
                "shortstory" => "ShortStory",
                "non-fiction" => "NonFiction",
                "non fiction" => "NonFiction",
                "nonfiction" => "NonFiction",
                "blog" => "Blog",
                _ => "Other"
            };
        }

            private const string WritingToolsCategory = "WritingTools";
            private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> IntentToolOrder =
                new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Novel"] = new[] { "novel.continue_scene", "novel.deepen_character", "novel.raise_stakes" },
                    ["ShortStory"] = new[] { "short_story.tighten_prose", "short_story.sharpen_ending", "short_story.heighten_theme" },
                    ["NonFiction"] = new[] { "non_fiction.clarify_simplify", "non_fiction.strengthen_argument", "non_fiction.add_structure" },
                    ["Blog"] = new[] { "blog.improve_hook", "blog.improve_readability", "blog.generate_headlines" },
                    ["Other"] = new[] { "other.improve_flow", "other.expand_idea", "other.summarize_clearly" }
                };

            private static readonly IReadOnlyDictionary<string, WritingToolDefinition> Registry =
                new Dictionary<string, WritingToolDefinition>(StringComparer.OrdinalIgnoreCase)
                {
                    ["novel.continue_scene"] = Create("novel.continue_scene", "Continue Scene", "Continue the scene while preserving POV and momentum.", "You are a fiction writing assistant focused on scene-level craft and continuity.", "Write ONLY the next paragraph that should follow this scene context. Do NOT repeat, paraphrase, or recap any existing text from context. Do NOT include any preamble, labels, or explanation. Return exactly one new paragraph only.\n\nContext:\n{context}"),
                    ["novel.deepen_character"] = Create("novel.deepen_character", "Deepen Character", "Increase character motivation and internal conflict signals.", "You are a fiction writing assistant focused on character depth and emotional clarity.", "Revise this section to deepen the main character's motivation and inner conflict using concrete cues. Context:\n{context}"),
                    ["novel.raise_stakes"] = Create("novel.raise_stakes", "Raise Stakes", "Increase urgency and consequences while preserving events.", "You are a fiction writing assistant focused on narrative stakes and tension.", "Revise this section to raise narrative stakes with clearer consequences and urgency while preserving events. Context:\n{context}"),
                    ["short_story.tighten_prose"] = Create("short_story.tighten_prose", "Tighten Prose", "Compress language while keeping tone and intent.", "You are a short-story writing assistant focused on economy and precision.", "Tighten this section by removing filler, sharpening verbs, and keeping the same meaning and tone. Context:\n{context}"),
                    ["short_story.sharpen_ending"] = Create("short_story.sharpen_ending", "Sharpen Ending", "Strengthen the final beat and emotional impact.", "You are a short-story writing assistant focused on strong endings and resonance.", "Revise this section to sharpen ending momentum and leave a stronger final emotional beat. Context:\n{context}"),
                    ["short_story.heighten_theme"] = Create("short_story.heighten_theme", "Heighten Theme", "Make thematic through-lines clearer in concrete prose.", "You are a short-story writing assistant focused on thematic clarity through scene detail.", "Revise this section to make the core theme more visible through concrete phrasing, not exposition. Context:\n{context}"),
                    ["non_fiction.clarify_simplify"] = Create("non_fiction.clarify_simplify", "Clarify & Simplify", "Improve clarity with concise, plain language.", "You are a non-fiction writing assistant focused on clarity and reader comprehension.", "Rewrite this section for clarity and simplicity with short precise sentences and plain language. Context:\n{context}"),
                    ["non_fiction.strengthen_argument"] = Create("non_fiction.strengthen_argument", "Strengthen Argument", "Improve logical flow and evidence framing.", "You are a non-fiction writing assistant focused on argument quality and structure.", "Revise this section to strengthen logic with clearer claims, support, and transitions. Context:\n{context}"),
                    ["non_fiction.add_structure"] = Create("non_fiction.add_structure", "Add Structure", "Improve organization using clear signposting.", "You are a non-fiction writing assistant focused on structure and readability.", "Re-structure this section with a clearer flow using concise headings or signpost transitions. Context:\n{context}"),
                    ["blog.improve_hook"] = Create("blog.improve_hook", "Improve Hook", "Create a stronger opening for audience attention.", "You are a blog writing assistant focused on engagement and retention.", "Rewrite the opening to create a stronger hook in 1-3 sentences while preserving topic and voice. Context:\n{context}"),
                    ["blog.improve_readability"] = Create("blog.improve_readability", "Improve Readability", "Make content easier to scan and read online.", "You are a blog writing assistant focused on scannability and readability.", "Revise this section for web readability with shorter sentences and scannable phrasing. Context:\n{context}"),
                    ["blog.generate_headlines"] = Create("blog.generate_headlines", "Generate Headlines", "Generate title ideas tailored to topic and audience.", "You are a blog writing assistant focused on compelling headline options.", "Generate 5 concise headline options tailored to this section's topic and audience. Context:\n{context}"),
                    ["other.improve_flow"] = Create("other.improve_flow", "Improve Flow", "Smooth transitions and coherence across ideas.", "You are a writing assistant focused on clarity, flow, and coherence.", "Revise this section to improve flow between ideas and sentence transitions. Context:\n{context}"),
                    ["other.expand_idea"] = Create("other.expand_idea", "Expand Idea", "Develop the strongest point with concise detail.", "You are a writing assistant focused on developing ideas with concise support.", "Expand the strongest idea in this section with one concise supporting paragraph. Context:\n{context}"),
                    ["other.summarize_clearly"] = Create("other.summarize_clearly", "Summarize Clearly", "Provide concise summaries with clear wording.", "You are a writing assistant focused on concise, accurate summaries.", "Produce a clear concise summary of this section in 2-3 sentences. Context:\n{context}")
                };

            public static IReadOnlyList<WritingToolDefinition> GetTopWritingToolsForIntent(string? intent)
            {
                string intentKey = NormalizeIntent(intent);
                if (!IntentToolOrder.TryGetValue(intentKey, out IReadOnlyList<string>? toolIds))
                {
                    toolIds = IntentToolOrder["Other"];
                }

                List<WritingToolDefinition> result = new(toolIds.Count);
                foreach (string id in toolIds)
                {
                    if (Registry.TryGetValue(id, out WritingToolDefinition? definition))
                    {
                        result.Add(definition);
                    }
                }

                return result;
            }

            private static WritingToolDefinition Create(
                string id,
                string displayName,
                string description,
                string systemTemplate,
                string userTemplate)
            {
                return new WritingToolDefinition(
                    id,
                    displayName,
                    description,
                    new WritingToolPromptTemplate(systemTemplate, userTemplate),
                    WritingToolsCategory,
                    true);
            }
        }

        public sealed record WritingToolPromptTemplate(
            string SystemTemplate,
            string UserTemplate);

        public sealed record WritingToolDefinition(
            string Id,
            string DisplayName,
            string Description,
            WritingToolPromptTemplate PromptTemplate,
            string Category,
            bool IsIntentRecommended);

