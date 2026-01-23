using WriterApp.Application.State;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class LayoutStateTests
    {
        [Fact]
        public void DefaultState_UsesExpectedDefaults()
        {
            LayoutState state = LayoutState.Default;

            Assert.False(state.FocusMode);
            Assert.False(state.LeftNavCollapsed);
            Assert.False(state.SectionsCollapsed);
            Assert.False(state.ContextCollapsed);
            Assert.Equal(ManuscriptWidthMode.Manuscript, state.ManuscriptWidthMode);
            Assert.Equal(100, state.EditorZoomPercent);
        }

        [Theory]
        [InlineData(80, 90)]
        [InlineData(90, 90)]
        [InlineData(120, 120)]
        [InlineData(140, 140)]
        [InlineData(200, 140)]
        public void ClampZoom_EnforcesBounds(int input, int expected)
        {
            Assert.Equal(expected, LayoutState.ClampZoom(input));
        }
    }
}
