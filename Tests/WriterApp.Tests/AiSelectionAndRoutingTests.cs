using System;
using System.Collections.Generic;
using WriterApp.AI.Actions;
using WriterApp.AI.Abstractions;
using WriterApp.Application.Commands;
using WriterApp.Application.Documents;
using WriterApp.Application.State;
using WriterApp.Domain.Documents;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class AiSelectionAndRoutingTests
    {
        [Fact]
        public void RewriteSelectionAction_UsesClientSelectedText_WhenProvided()
        {
            Document document = CreateDocument("<p>Alpha beta gamma.</p>");
            Guid sectionId = document.Chapters[0].Sections[0].SectionId;

            RewriteSelectionAction action = new();
            AiRequest request = action.BuildRequest(new AiActionInput(
                document,
                sectionId,
                new TextRange(0, 5),
                "Client selection text",
                "Rewrite",
                new Dictionary<string, object?>()));

            Assert.Equal("Client selection text", request.Context.SelectionText);
            Assert.Equal("Client selection text", request.Context.OriginalText);
        }

        [Fact]
        public void TranslateSelectionAction_UsesClientSelectedText_WhenProvided()
        {
            Document document = CreateDocument("<p>Det har ar borjan pa berattelsen.</p>");
            Guid sectionId = document.Chapters[0].Sections[0].SectionId;

            TranslateSelectionAction action = new();
            AiRequest request = action.BuildRequest(new AiActionInput(
                document,
                sectionId,
                new TextRange(0, 10),
                "Det har ar",
                "Translate",
                new Dictionary<string, object?>
                {
                    ["source_language"] = "sv",
                    ["target_language"] = "en"
                }));

            Assert.Equal("Det har ar", request.Context.SelectionText);
            Assert.Equal("Det har ar", request.Context.OriginalText);
        }

        [Fact]
        public void SceneSuggestAction_UsesSectionOverride_And_ContainsExpectedInputs()
        {
            Document document = CreateDocument("<p>Old stored text.</p>");
            Guid sectionId = document.Chapters[0].Sections[0].SectionId;

            SceneSuggestAction action = new();
            AiRequest request = action.BuildRequest(new AiActionInput(
                document,
                sectionId,
                new TextRange(0, 0),
                string.Empty,
                "Suggest scene card fields",
                new Dictionary<string, object?>
                {
                    ["section_text_override"] = "Maya rushed into the office at 08:10 and apologized to the team.",
                    ["narrative_purpose"] = "Introduce scheduling conflict",
                    ["pov_character_id"] = "Maya",
                    ["place_id"] = "Office",
                    ["time_ref"] = "Morning"
                }));

            Assert.Equal("Maya rushed into the office at 08:10 and apologized to the team.", request.Inputs["section_text"]);
            Assert.True(request.Inputs.ContainsKey("narrative_purpose"));
            Assert.True(request.Inputs.ContainsKey("emotional_beat"));
            Assert.True(request.Inputs.ContainsKey("key_events"));
            Assert.True(request.Inputs.ContainsKey("open_questions"));
            Assert.True(request.Inputs.ContainsKey("pov_character_id"));
            Assert.True(request.Inputs.ContainsKey("place_id"));
            Assert.True(request.Inputs.ContainsKey("timeline_event_id"));
            Assert.True(request.Inputs.ContainsKey("time_ref"));
            Assert.True(request.Inputs.ContainsKey("tags_json"));
            Assert.True(request.Inputs.ContainsKey("references_json"));
        }

        [Fact]
        public void SceneRouteBuilder_BuildsExpectedRelativePath()
        {
            Guid projectId = Guid.Parse("8d6acb8e-67f6-4454-a9c3-aca8dad38d89");
            Guid sceneId = Guid.Parse("0b0520b2-1f10-4adc-9fac-162be4299df4");

            string path = SceneRouteBuilder.BuildRelativeSceneEditorPath(projectId, sceneId);

            Assert.Equal("projects/8d6acb8e-67f6-4454-a9c3-aca8dad38d89/scenes/0b0520b2-1f10-4adc-9fac-162be4299df4", path);
        }

        private static Document CreateDocument(string html)
        {
            Document document = DocumentFactory.CreateNewDocument();
            Section section = document.Chapters[0].Sections[0];
            document.Chapters[0].Sections[0] = section with
            {
                Content = section.Content with
                {
                    Value = html
                }
            };
            return document;
        }
    }
}
