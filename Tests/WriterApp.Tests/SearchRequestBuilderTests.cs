using System;
using WriterApp.Application.Documents;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class SearchRequestBuilderTests
    {
        [Fact]
        public void BuildUrl_IncludesProjectIdParameter()
        {
            Guid projectId = Guid.Parse("11111111-2222-3333-4444-555555555555");

            string url = SearchRequestBuilder.BuildUrl("Test", projectId, includeMeta: true, limit: 100);

            Assert.Contains("api/search?", url, StringComparison.Ordinal);
            Assert.Contains("q=Test", url, StringComparison.Ordinal);
            Assert.Contains($"projectId={projectId:D}", url, StringComparison.Ordinal);
            Assert.Contains("includeMeta=true", url, StringComparison.Ordinal);
            Assert.Contains("limit=100", url, StringComparison.Ordinal);
        }

        [Fact]
        public void BuildUrl_ThrowsWhenProjectIdIsEmpty()
        {
            Assert.Throws<ArgumentException>(() =>
                SearchRequestBuilder.BuildUrl("Test", Guid.Empty, includeMeta: true, limit: 100));
        }
    }
}
