using System;
using System.Collections.Generic;
using WriterApp.Application.Commands;
using WriterApp.Application.State;
using WriterApp.Domain.Documents;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class CoverImageCommandTests
    {
        [Fact]
        public void SetCoverImageCommand_UndoRedo_RestoresCoverId()
        {
            Document document = CreateDocument();
            DocumentState state = new(document);
            CommandProcessor processor = new(state);

            Guid artifactId = Guid.NewGuid();
            DocumentArtifact artifact = new(artifactId, "image/svg+xml", "ZHVtbXk=", null);
            Guid sectionId = document.Chapters[0].Sections[0].SectionId;
            AiEditGroup group = new(sectionId, "cover");

            processor.Execute(new SetCoverImageCommand(sectionId, artifact, group, "cover"));
            Assert.Equal(artifactId, state.Document.CoverImageId);

            processor.Undo();
            Assert.Null(state.Document.CoverImageId);

            processor.Redo();
            Assert.Equal(artifactId, state.Document.CoverImageId);
        }

        private static Document CreateDocument()
        {
            return new Document
            {
                Metadata = new DocumentMetadata { Title = "Test", Language = "en" },
                Chapters = new List<Chapter>
                {
                    new Chapter
                    {
                        Order = 0,
                        Title = "Chapter",
                        Sections = new List<Section>
                        {
                            new Section
                            {
                                Order = 0,
                                Title = "Section",
                                Content = new SectionContent { Format = "html", Value = "<p>Test</p>" },
                                Stats = new SectionStats(),
                                Flags = new SectionFlags(),
                                AI = new SectionAIInfo()
                            }
                        }
                    }
                }
            };
        }
    }
}
