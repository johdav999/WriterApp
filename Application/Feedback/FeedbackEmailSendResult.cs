using System.Net;

namespace WriterApp.Application.Feedback
{
    public sealed record FeedbackEmailSendResult(
        bool Succeeded,
        string UserMessage,
        HttpStatusCode? ProviderStatusCode = null,
        string? ProviderResponseSummary = null)
    {
        public static FeedbackEmailSendResult Success(string userMessage)
            => new(true, userMessage);

        public static FeedbackEmailSendResult Failure(
            string userMessage,
            HttpStatusCode? providerStatusCode = null,
            string? providerResponseSummary = null)
            => new(false, userMessage, providerStatusCode, providerResponseSummary);
    }
}
