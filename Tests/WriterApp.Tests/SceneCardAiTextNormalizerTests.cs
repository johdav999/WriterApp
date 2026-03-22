using System.Collections.Generic;
using System.Linq;
using WriterApp.Application.Documents;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class SceneCardAiTextNormalizerTests
    {
        [Fact]
        public void NormalizeAiText_ReplacesUnderscoresAndCapitalizesFirstLetter()
        {
            string? normalized = SceneCardAiTextNormalizer.NormalizeAiText("uncharted_ocean_coast");

            Assert.Equal("Uncharted ocean coast", normalized);
        }

        [Fact]
        public void NormalizeAiText_TrimWhitespace_WithoutChangingInternalCasing()
        {
            string? normalized = SceneCardAiTextNormalizer.NormalizeAiText("  quiet_Tension at sunrise  ");

            Assert.Equal("Quiet Tension at sunrise", normalized);
        }

        [Fact]
        public void NormalizeAiTextList_NormalizesAndDeduplicatesValues()
        {
            IReadOnlyList<string> normalized = SceneCardAiTextNormalizer.NormalizeAiTextList(
                new[] { "subplot_hook", " Subplot_hook ", "sea_change" });

            Assert.Equal(new[] { "Subplot hook", "Sea change" }, normalized.ToArray());
        }
    }
}
