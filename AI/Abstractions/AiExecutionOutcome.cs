namespace WriterApp.AI.Abstractions
{
    public sealed record AiExecutionOutcome(
        AiProposal? Proposal,
        AiResult Result,
        string ProviderId,
        string? ErrorCode,
        string? ErrorMessage)
    {
        public static AiExecutionOutcome Success(AiProposal proposal, AiResult result, string providerId)
        {
            return new AiExecutionOutcome(proposal, result, providerId, null, null);
        }

        public static AiExecutionOutcome Rejected(AiResult result, string providerId, string errorCode, string errorMessage)
        {
            return new AiExecutionOutcome(null, result, providerId, errorCode, errorMessage);
        }
    }
}
