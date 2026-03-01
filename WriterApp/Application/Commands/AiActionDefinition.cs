namespace WriterApp.Application.Commands
{
    public enum AiActionKind
    {
        RewriteSelection,
        ShortenSelection,
        FixGrammar,
        ChangeTone,
        SummarizeParagraph
    }

    public enum AiActionScope
    {
        Selection,
        Paragraph,
        Section
    }

    public sealed record AiActionDefinition(
        AiActionKind Kind,
        string Label,
        string Instruction,
        AiActionScope Scope);
}
