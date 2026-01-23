using System;
using System.Collections.Generic;
using WriterApp.Application.State;
using WriterApp.Domain.Documents;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class SectionNumberingServiceTests
    {
        [Fact]
        public void BuildIndex_SkipsExcludedSections()
        {
            SectionNumberingService service = new();
            Section first = BuildSection(0, SectionKind.Chapter, include: true, style: SectionNumberingStyle.Decimal);
            Section skipped = BuildSection(1, SectionKind.Chapter, include: false, style: SectionNumberingStyle.Decimal);
            Section third = BuildSection(2, SectionKind.Chapter, include: true, style: SectionNumberingStyle.Decimal);

            Document document = BuildDocument(first, skipped, third);
            IReadOnlyDictionary<Guid, SectionNumberingInfo> numbers = service.BuildIndex(document);

            Assert.Equal("1", numbers[first.SectionId].Number);
            Assert.False(numbers[skipped.SectionId].IsNumbered);
            Assert.Equal("2", numbers[third.SectionId].Number);
        }

        [Fact]
        public void BuildIndex_UpdatesWhenReordered()
        {
            SectionNumberingService service = new();
            Section first = BuildSection(0, SectionKind.Chapter, include: true, style: SectionNumberingStyle.Decimal);
            Section second = BuildSection(1, SectionKind.Chapter, include: true, style: SectionNumberingStyle.Decimal);

            Document document = BuildDocument(first, second);
            IReadOnlyDictionary<Guid, SectionNumberingInfo> numbers = service.BuildIndex(document);
            Assert.Equal("1", numbers[first.SectionId].Number);
            Assert.Equal("2", numbers[second.SectionId].Number);

            Document reordered = BuildDocument(
                second with { Order = 0 },
                first with { Order = 1 });
            IReadOnlyDictionary<Guid, SectionNumberingInfo> reorderedNumbers = service.BuildIndex(reordered);
            Assert.Equal("1", reorderedNumbers[second.SectionId].Number);
            Assert.Equal("2", reorderedNumbers[first.SectionId].Number);
        }

        [Fact]
        public void BuildIndex_FormatsRomanNumerals()
        {
            SectionNumberingService service = new();
            Section first = BuildSection(0, SectionKind.Chapter, include: true, style: SectionNumberingStyle.Roman);
            Section second = BuildSection(1, SectionKind.Chapter, include: true, style: SectionNumberingStyle.Roman);

            Document document = BuildDocument(first, second);
            IReadOnlyDictionary<Guid, SectionNumberingInfo> numbers = service.BuildIndex(document);

            Assert.Equal("I", numbers[first.SectionId].Number);
            Assert.Equal("II", numbers[second.SectionId].Number);
        }

        private static Document BuildDocument(params Section[] sections)
        {
            return new Document
            {
                Chapters = new List<Chapter>
                {
                    new Chapter
                    {
                        Order = 0,
                        Title = "Draft",
                        Sections = new List<Section>(sections)
                    }
                }
            };
        }

        private static Section BuildSection(int order, SectionKind kind, bool include, SectionNumberingStyle style)
        {
            return new Section
            {
                SectionId = Guid.NewGuid(),
                Order = order,
                Title = $"Section {order + 1}",
                Kind = kind,
                IncludeInNumbering = include,
                NumberingStyle = style,
                Content = new SectionContent { Format = "html", Value = "<p></p>" }
            };
        }
    }
}
