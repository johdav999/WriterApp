using WriterApp.Shared;
using Xunit;

namespace WriterApp.Tests;

public sealed class ConsistencyPagePassagesTests
{
    [Theory][InlineData(0, 999)][InlineData(-1, 2)][InlineData(int.MaxValue, int.MaxValue)]
    public void ConsistencyPageOffsetsCannotRedirectUniqueEvidence(int start, int length)
    {
        var pages = new[] { new ConsistencyPageSource(Guid.NewGuid(), "<p>Other clock.</p>", "Other clock."),
            new ConsistencyPageSource(Guid.NewGuid(), "<p>The clock stood.</p>", "The clock stood.") };
        var passage = ConsistencyPagePassages.Resolve(pages, "The clock stood.", start, length);
        Assert.Equal(pages[1].PageId, passage.PageId); Assert.Equal(0, passage.Start); Assert.Equal("The clock stood.", passage.Text);
    }
    [Fact]
    public void ConsistencyPageNarrowAnchorMapsFromSectionOffsetAndUnicodeCutsFallBackToFullEvidence()
    {
        var pages = new[] { new ConsistencyPageSource(Guid.NewGuid(), "", "🧭 Prologue."), new ConsistencyPageSource(Guid.NewGuid(), "", "Åsa saw a café\u0301 clock.") };
        int offset = pages[0].PlainText.Length + 2;
        Assert.Equal(new(pages[1].PageId, 0, 3, "Åsa"), ConsistencyPagePassages.Resolve(pages, pages[1].PlainText, offset, 3));
        Assert.Equal(pages[1].PlainText, ConsistencyPagePassages.Resolve(pages, pages[1].PlainText, offset + 10, 4).Text);
        Assert.Throws<InvalidDataException>(() => ConsistencyPagePassages.Resolve(pages, "café", 0, 0));
        Assert.Throws<InvalidDataException>(() => ConsistencyPagePassages.Resolve(pages, "Ås", 0, 0));
    }
    [Fact]
    public void ConsistencyPageDuplicateOccurrencesAndCrossPageQuotesCannotAuthorizeAnyTarget()
    {
        var pages = new[] { new ConsistencyPageSource(Guid.NewGuid(), "", "clock clock"), new ConsistencyPageSource(Guid.NewGuid(), "", "another") };
        Assert.Throws<InvalidOperationException>(() => ConsistencyPagePassages.Resolve(pages, "clock", 0, 5));
        Assert.Throws<InvalidOperationException>(() => ConsistencyPagePassages.Resolve(pages, "clock\n\nanother", 6, 14));
    }
    [Fact]
    public void ConsistencyPageSourceBoundsRejectInsteadOfTruncatingOrReusingPageIdentity()
    {
        var id = Guid.NewGuid();
        Assert.Throws<InvalidDataException>(() => ConsistencyPagePassages.Text([new(id, "", new('x', 100_001))]));
        Assert.Throws<InvalidDataException>(() => ConsistencyPagePassages.Text([new(id, new('x', 2_000_001), "a")]));
        Assert.Throws<InvalidDataException>(() => ConsistencyPagePassages.Text([new(id, "", "a"), new(id, "", "b")]));
        Assert.Throws<InvalidDataException>(() => ConsistencyPagePassages.Text([new(Guid.Empty, "", "a")]));
        Assert.Throws<InvalidDataException>(() => ConsistencyPagePassages.Text([]));
    }
}
