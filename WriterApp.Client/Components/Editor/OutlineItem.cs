namespace WriterApp.Client.Components.Editor
{
    public sealed class OutlineItem
    {
        public string Text { get; set; } = string.Empty;
        public int Level { get; set; }
        public int Position { get; set; }
    }
}
