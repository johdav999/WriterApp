using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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

        private readonly IConfiguration _configuration;
        private readonly ILogger<FeedbackController> _logger;
        private readonly IUserIdResolver _userIdResolver;

        public FeedbackController(
            IConfiguration configuration,
            ILogger<FeedbackController> logger,
            IUserIdResolver userIdResolver)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _userIdResolver = userIdResolver ?? throw new ArgumentNullException(nameof(userIdResolver));
        }

        [HttpPost]
        public async Task<IActionResult> Submit([FromBody] FeedbackSubmitRequest? request, CancellationToken ct)
        {
            if (request is null)
            {
                return BadRequest(new { message = "Request body is required." });
            }

            string type = (request.Type ?? string.Empty).Trim();
            if (!string.Equals(type, "bug", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(type, "enhancement", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Type must be Bug or Enhancement." });
            }

            string subject = (request.Subject ?? string.Empty).Trim();
            string description = (request.Description ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(description))
            {
                return BadRequest(new { message = "Subject and description are required." });
            }

            string userId = _userIdResolver.ResolveUserId(User);
            if (!CheckRateLimit(userId))
            {
                return StatusCode((int)HttpStatusCode.TooManyRequests, new { message = "Too many feedback submissions. Please try again later." });
            }

            string toAddress = _configuration["Feedback:Email:To"] ?? "johan.davidsson@hotmail.se";
            string fromAddress = _configuration["Feedback:Email:From"] ?? "writerapp@localhost";
            string smtpHost = _configuration["Feedback:Smtp:Host"] ?? string.Empty;
            int smtpPort = _configuration.GetValue<int?>("Feedback:Smtp:Port") ?? 25;
            bool useSsl = _configuration.GetValue<bool?>("Feedback:Smtp:UseSsl") ?? true;
            string smtpUser = _configuration["Feedback:Smtp:Username"] ?? string.Empty;
            string smtpPassword = _configuration["Feedback:Smtp:Password"] ?? string.Empty;

            if (string.IsNullOrWhiteSpace(smtpHost))
            {
                _logger.LogWarning("Feedback SMTP host is not configured.");
                return StatusCode((int)HttpStatusCode.ServiceUnavailable, new { message = "Feedback email is not configured." });
            }

            try
            {
                string kindLabel = string.Equals(type, "bug", StringComparison.OrdinalIgnoreCase) ? "Bug" : "Enhancement";
                string senderName = User.Identity?.Name
                    ?? User.FindFirstValue(ClaimTypes.Email)
                    ?? userId;
                StringBuilder body = new();
                body.AppendLine($"Type: {kindLabel}");
                body.AppendLine($"Subject: {subject}");
                body.AppendLine($"User: {senderName}");
                body.AppendLine($"UserId: {userId}");
                body.AppendLine($"Timestamp (UTC): {DateTimeOffset.UtcNow:O}");
                body.AppendLine();
                body.AppendLine("Description:");
                body.AppendLine(description);

                if (request.IncludeDiagnostics && request.Diagnostics is not null)
                {
                    body.AppendLine();
                    body.AppendLine("Diagnostics:");
                    body.AppendLine($"URL: {request.Diagnostics.Url}");
                    body.AppendLine($"Version: {request.Diagnostics.Version}");
                    body.AppendLine($"UserAgent: {request.Diagnostics.UserAgent}");
                }

                using MailMessage message = new(fromAddress, toAddress)
                {
                    Subject = $"[WriterApp Feedback] {kindLabel}: {subject}",
                    Body = body.ToString()
                };

                using SmtpClient client = new(smtpHost, smtpPort)
                {
                    EnableSsl = useSsl
                };
                if (!string.IsNullOrWhiteSpace(smtpUser))
                {
                    client.Credentials = new NetworkCredential(smtpUser, smtpPassword);
                }

                await client.SendMailAsync(message, ct);
                return Ok(new { message = "Feedback sent." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Feedback email send failed for user {UserId}.", userId);
                return StatusCode((int)HttpStatusCode.InternalServerError, new { message = "Feedback send failed." });
            }
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
            string? Type,
            string? Subject,
            string? Description,
            bool IncludeDiagnostics,
            FeedbackDiagnosticsDto? Diagnostics);

        public sealed record FeedbackDiagnosticsDto(
            string? Url,
            string? Version,
            string? UserAgent);
    }
}
