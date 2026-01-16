using System;
using System.Collections.Generic;
using WriterApp.Domain.Documents;

namespace WriterApp.Application.State
{
    public static class DocumentFactory
    {
        public static Document CreateNewDocument()
        {
            DateTime now = DateTime.UtcNow;

            return new Document
            {
                Metadata = new DocumentMetadata
                {
                    Title = "Sample Draft",
                    Author = "Demo",
                    Language = "en",
                    Tags = new List<string> { "demo" },
                    CreatedUtc = now,
                    ModifiedUtc = now
                },
                Settings = new DocumentSettings
                {
                    DefaultFont = "Georgia",
                    DefaultFontSize = 12,
                    PageSize = "Letter",
                    LineSpacing = 1.5
                },
                Chapters = new List<Chapter>
                {
                    new Chapter
                    {
                        Order = 0,
                        Title = "Draft",
                        Sections = new List<Section>
                        {
                            new Section
                            {
                                Order = 0,
                                Title = "Opening Scene",
                                Content = new SectionContent
                                {
                                    Format = "html",
                                    Value = "<h1>Opening Scene</h1><p>The storm rolled in just after dusk, wrapping the town in a soft gray hush.</p>"
                                },
                                Stats = new SectionStats(),
                                Flags = new SectionFlags(),
                                AI = new SectionAIInfo(),
                                CreatedUtc = now,
                                ModifiedUtc = now
                            },
                            new Section
                            {
                                Order = 1,
                                Title = "Chapter One",
                                Content = new SectionContent
                                {
                                    Format = "html",
                                    Value = "<h2>Chapter One</h2><p>Eva traced the map with her finger, pausing at the edge of the inked coastline.</p>"
                                },
                                Stats = new SectionStats(),
                                Flags = new SectionFlags(),
                                AI = new SectionAIInfo(),
                                CreatedUtc = now,
                                ModifiedUtc = now
                            },
                            new Section
                            {
                                Order = 2,
                                Title = "Chapter Two",
                                Content = new SectionContent
                                {
                                    Format = "html",
                                    Value = "<h2>Chapter Two</h2><p>By morning, the docks were empty, save for a single lantern swaying against the tide.</p>"
                                },
                                Stats = new SectionStats(),
                                Flags = new SectionFlags(),
                                AI = new SectionAIInfo(),
                                CreatedUtc = now,
                                ModifiedUtc = now
                            }
                        }
                    }
                }
            };
        }
    }
}
