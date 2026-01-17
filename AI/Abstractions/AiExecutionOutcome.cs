namespace WriterApp.AI.Abstractions
{
    public sealed record AiExecutionOutcome(
        AiProposal Proposal,
        AiResult Result,
        string ProviderId);
}
