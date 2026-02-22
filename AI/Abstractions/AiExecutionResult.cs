using System.Collections.Generic;

namespace WriterApp.AI.Abstractions
{
    public sealed record AiExecutionResult(
        bool Succeeded,
        AiProposal? Proposal,
        string? ErrorCode,
        string? ErrorMessage,
        IReadOnlyDictionary<string, object?>? ErrorDetails = null)
    {
        public static AiExecutionResult Success(AiProposal proposal)
        {
            return new AiExecutionResult(true, proposal, null, null, null);
        }

        public static AiExecutionResult Blocked(string errorCode, string errorMessage)
        {
            return new AiExecutionResult(false, null, errorCode, errorMessage, null);
        }

        public static AiExecutionResult Blocked(
            string errorCode,
            string errorMessage,
            IReadOnlyDictionary<string, object?> errorDetails)
        {
            return new AiExecutionResult(false, null, errorCode, errorMessage, errorDetails);
        }
    }
}
