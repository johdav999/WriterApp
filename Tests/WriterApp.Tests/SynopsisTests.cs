using System;
using System.Linq;
using System.Text.Json;
using WriterApp.AI.Abstractions;
using WriterApp.AI.Core;
using WriterApp.Application.Commands;
using WriterApp.Application.State;
using WriterApp.Domain.Documents;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class SynopsisTests
    {
        [Fact]
        public void UpdateSynopsisFieldCommand_UndoRedo_Works()
        {
            Document document = DocumentFactory.CreateNewDocument();
            Guid sectionId = document.Chapters[0].Sections[0].SectionId;
            DocumentState state = new(document);
            CommandProcessor processor = new(state);
            AiEditGroup group = new(sectionId, "Synopsis:premise");

            string oldValue = document.Synopsis.Premise;
            processor.Execute(new UpdateSynopsisFieldCommand(sectionId, "premise", "New premise", oldValue, group));

            Assert.Equal("New premise", state.Document.Synopsis.Premise);

            processor.Undo();
            Assert.Equal(oldValue, state.Document.Synopsis.Premise);
        }

        [Fact]
        public void AiProposal_Apply_UpdatesSynopsis()
        {
            Document document = DocumentFactory.CreateNewDocument();
            Guid sectionId = document.Chapters[0].Sections[0].SectionId;
            DocumentState state = new(document);
            CommandProcessor processor = new(state);
            IAiProposalApplier applier = new DefaultProposalApplier(new InMemoryArtifactStore());

            AiProposal proposal = new(
                Guid.NewGuid(),
                sectionId,
                "Story Coach",
                "synopsis.story_coach",
                "mock",
                Guid.NewGuid(),
                DateTime.UtcNow,
                "Synopsis:premise",
                new System.Collections.Generic.List<ProposedOperation>
                {
                    new ReplaceSynopsisFieldOperation("premise", "Updated premise")
                },
                new System.Collections.Generic.List<Guid>(),
                "Story Coach suggestion",
                "Synopsis",
                "Synopsis:premise",
                state.Document.Synopsis.Premise,
                "Updated premise");

            applier.Apply(processor, proposal);

            Assert.Equal("Updated premise", state.Document.Synopsis.Premise);
            Assert.True(state.Document.Chapters[0].Sections[0].AI?.AiEditGroups?.Any() ?? false);
        }

        [Fact]
        public void Document_Serialization_PreservesSynopsis()
        {
            Document document = DocumentFactory.CreateNewDocument();
            document.Synopsis.Premise = "Premise text";
            document.Synopsis.Protagonist = "Hero";
            document.Synopsis.ModifiedUtc = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            string json = JsonSerializer.Serialize(document);
            Document? deserialized = JsonSerializer.Deserialize<Document>(json);

            Assert.NotNull(deserialized);
            Assert.NotNull(deserialized!.Synopsis);
            Assert.Equal("Premise text", deserialized.Synopsis.Premise);
            Assert.Equal("Hero", deserialized.Synopsis.Protagonist);
        }
    }
}
