namespace WriterApp.AI.Abstractions
{
    public abstract record AiStreamEvent
    {
        public sealed record Started : AiStreamEvent;

        public sealed record TextDelta(string Delta) : AiStreamEvent;

        public sealed record ImageDelta(string Reference) : AiStreamEvent;

        public sealed record Completed : AiStreamEvent;

        public sealed record Failed(string Error) : AiStreamEvent;
    }
}
