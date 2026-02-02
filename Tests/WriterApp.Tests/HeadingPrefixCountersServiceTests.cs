using System;
using System.Collections.Generic;
using WriterApp.Application.Documents;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class HeadingPrefixCountersServiceTests
    {
        [Fact]
        public void ComputePrefix_StopsBeforeTargetPage()
        {
            HeadingPrefixCountersService service = new();
            Guid firstPageId = Guid.NewGuid();
            Guid secondPageId = Guid.NewGuid();
            int[] counters = service.CreateCounters();

            List<HeadingPageContent> pages = new()
            {
                new HeadingPageContent(firstPageId, "<h1>A</h1><p>Body</p><h2>A1</h2>"),
                new HeadingPageContent(secondPageId, "<h2>B1</h2>")
            };

            bool found = service.TryComputePrefix(pages, secondPageId, counters);

            Assert.True(found);
            Assert.Equal(1, counters[1]);
            Assert.Equal(1, counters[2]);
            Assert.Equal(0, counters[3]);
        }
    }
}
