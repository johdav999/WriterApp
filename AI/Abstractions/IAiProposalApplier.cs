using WriterApp.Application.Commands;

namespace WriterApp.AI.Abstractions
{
    public interface IAiProposalApplier
    {
        void Apply(CommandProcessor commandProcessor, AiProposal proposal);
    }
}
