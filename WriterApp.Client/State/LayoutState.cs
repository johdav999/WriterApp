using System;

namespace WriterApp.Client.State
{
    public enum ManuscriptWidthMode
    {
        FullWidth,
        Manuscript
    }

    public sealed record LayoutState
    {
        public bool FocusMode { get; init; }
        public bool LeftNavCollapsed { get; init; }
        public bool SectionsCollapsed { get; init; }
        public bool ContextCollapsed { get; init; }
        public ManuscriptWidthMode ManuscriptWidthMode { get; init; } = ManuscriptWidthMode.Manuscript;
        public int EditorZoomPercent { get; init; } = 100;

        public static LayoutState Default { get; } = new();

        public static int ClampZoom(int zoomPercent)
        {
            return Math.Clamp(zoomPercent, 90, 140);
        }

        public LayoutState Normalize()
        {
            return this with { EditorZoomPercent = ClampZoom(EditorZoomPercent) };
        }
    }
}
