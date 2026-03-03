using WriterApp.Client.Pages;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class DocumentEditorAiApplyModeTests
    {
        [Fact]
        public void ResolveAiApplyMode_UsesScopeSectionForCustomTransform()
        {
            string mode = DocumentEditor.ResolveAiApplyMode("section", "custom_transform");

            Assert.Equal("section", mode);
        }

        [Fact]
        public void ResolveAiApplyMode_UsesLegacyKeyFallbackWhenScopeUnknown()
        {
            string mode = DocumentEditor.ResolveAiApplyMode(null, "rewrite.section");

            Assert.Equal("section", mode);
        }
    }
}
