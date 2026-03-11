using System;

namespace WriterApp.Application.Feedback
{
    public sealed record FeedbackEmailRequest(
        string Type,
        string Subject,
        string Message,
        string UserId,
        string? UserDisplayName,
        string? UserEmail,
        DateTimeOffset SubmittedAtUtc,
        string? RouteOrPage,
        string? DiagnosticsVersion,
        string? DiagnosticsUserAgent,
        string? DiagnosticsSummary);
}
