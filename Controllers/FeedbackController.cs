using System;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using WriterApp.Application.Feedback;
using WriterApp.Application.Security;

namespace WriterApp.Controllers
{
    [ApiController]
    [Route("api/feedback")]
    [Authorize]
    public sealed class FeedbackController : ControllerBase
    {
        private static readonly ConcurrentDictionary<string, ConcurrentQueue<DateTimeOffset>> SubmissionWindows = new();
        private static readonly TimeSpan Window = TimeSpan.FromMinutes(10);
        private const int MaxSubmissionsPerWindow = 5;

        private readonly ILogger<FeedbackController> _logger;
        private readonly IUserIdResolver _userIdResolver;
        private readonly IFeedbackEmailSender _feedbackEmailSender;

        public FeedbackController(
            ILogger<FeedbackController> logger,
            IUserIdResolver userIdResolver,
            IFeedbackEmailSender feedbackEmailSender)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _userIdResolver = userIdResolver ?? throw new ArgumentNullException(nameof(userIdResolver));
            _feedbackEmailSender = feedbackEmailSender ?? throw new ArgumentNullException(nameof(feedbackEmailSender));
        }

        [HttpPost]
        public async Task<IActionResult> Submit([FromBody] FeedbackSubmitRequest? request, CancellationToken ct)
        {
            if (request is null)
            {
                ModelState.AddModelError(string.Empty, "Request body is required.");
                LogModelStateErrors();
                return BadRequest(ModelState);
            }

            string type = (request.Type ?? string.Empty).Trim();
            if (!string.Equals(type, "bug", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(type, "enhancement", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(nameof(FeedbackSubmitRequest.Type), "Type must be Bug or Enhancement.");
            }

            string subject = (request.Subject ?? string.Empty).Trim();
            string description = (request.Description ?? string.Empty).Trim();
            if (description.Length > 8000)
            {
                description = description[..8000];
            }

            if (string.IsNullOrWhiteSpace(subject))
            {
                ModelState.AddModelError(nameof(FeedbackSubmitRequest.Subject), "Subject is required.");
            }

            if (string.IsNullOrWhiteSpace(description))
            {
                ModelState.AddModelError(nameof(FeedbackSubmitRequest.Description), "Description is required.");
            }

            if (!ModelState.IsValid)
            {
                LogModelStateErrors();
                return BadRequest(ModelState);
            }

            string userId = _userIdResolver.ResolveUserId(User);
            if (!CheckRateLimit(userId))
            {
                return StatusCode((int)HttpStatusCode.TooManyRequests, new { message = "Too many feedback submissions. Please try again later." });
            }

            string senderName = User.Identity?.Name
                ?? User.FindFirstValue(ClaimTypes.Email)
                ?? userId;
            string? userEmail = User.FindFirstValue(ClaimTypes.Email);
            FeedbackEmailRequest emailRequest = new(
                type,
                subject,
                description,
                userId,
                senderName,
                userEmail,
                DateTimeOffset.UtcNow,
                request.Diagnostics?.Url,
                request.Diagnostics?.Version,
                request.Diagnostics?.UserAgent,
                BuildDiagnosticsSummary(request.Diagnostics));

            try
            {
                FeedbackEmailSendResult result = await _feedbackEmailSender.SendAsync(emailRequest, ct);
                if (result.Succeeded)
                {
                    return Ok(new { message = result.UserMessage });
                }

                _logger.LogWarning(
                    "Feedback delivery failed. UserId={UserId} ProviderStatusCode={ProviderStatusCode} ProviderResponseSummary={ProviderResponseSummary}",
                    userId,
                    result.ProviderStatusCode.HasValue ? (int)result.ProviderStatusCode.Value : null,
                    result.ProviderResponseSummary);
                int statusCode = result.ProviderStatusCode == HttpStatusCode.ServiceUnavailable
                    ? StatusCodes.Status503ServiceUnavailable
                    : StatusCodes.Status500InternalServerError;
                return StatusCode(statusCode, new { message = result.UserMessage });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Feedback email send failed for user {UserId}.", userId);
                return StatusCode((int)HttpStatusCode.InternalServerError, new { message = "Could not send feedback right now. Please retry." });
            }
        }

        private void LogModelStateErrors()
        {
            foreach ((string key, Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateEntry? entry) in ModelState)
            {
                if (entry?.Errors is null || entry.Errors.Count == 0)
                {
                    continue;
                }

                foreach (Microsoft.AspNetCore.Mvc.ModelBinding.ModelError error in entry.Errors)
                {
                    _logger.LogWarning(
                        "Feedback validation error. Field={Field} Error={Error}",
                        key,
                        error.ErrorMessage);
                }
            }
        }

        private static string? BuildDiagnosticsSummary(FeedbackDiagnosticsDto? diagnostics)
        {
            if (diagnostics is null)
            {
                return null;
            }

            string[] parts = new[]
            {
                diagnostics.Url is null ? string.Empty : $"URL: {diagnostics.Url}",
                diagnostics.Version is null ? string.Empty : $"Version: {diagnostics.Version}",
                diagnostics.UserAgent is null ? string.Empty : $"UserAgent: {diagnostics.UserAgent}"
            };

            string combined = string.Join(Environment.NewLine, parts.Where(static part => !string.IsNullOrWhiteSpace(part)));
            return string.IsNullOrWhiteSpace(combined) ? null : combined;
        }

        private static bool CheckRateLimit(string userId)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            ConcurrentQueue<DateTimeOffset> queue = SubmissionWindows.GetOrAdd(userId, _ => new ConcurrentQueue<DateTimeOffset>());
            while (queue.TryPeek(out DateTimeOffset ts) && now - ts > Window)
            {
                queue.TryDequeue(out _);
            }

            if (queue.Count >= MaxSubmissionsPerWindow)
            {
                return false;
            }

            queue.Enqueue(now);
            return true;
        }

        public sealed record FeedbackSubmitRequest(
            [Required] string? Type,
            [Required] string? Subject,
            [Required] string? Description,
            bool IncludeDiagnostics,
            FeedbackDiagnosticsDto? Diagnostics);

        public sealed record FeedbackDiagnosticsDto(
            string? Url,
            string? Version,
            string? UserAgent);
    }
}
