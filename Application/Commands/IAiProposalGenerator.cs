namespace WriterApp.Application.Commands
{
    public interface IAiProposalGenerator
    {
        string Generate(AiActionKind kind, string instruction, string originalText);
    }
}
