namespace WriterApp.AI.Abstractions
{
    public sealed record AiExecutionResult(
        bool Succeeded,
        AiProposal? Proposal,
        string? ErrorCode,
        string? ErrorMessage)
    {
        public static AiExecutionResult Success(AiProposal proposal)
        {
            return new AiExecutionResult(true, proposal, null, null);
        }

        public static AiExecutionResult Blocked(string errorCode, string errorMessage)
        {
            return new AiExecutionResult(false, null, errorCode, errorMessage);
        }
    }
}
