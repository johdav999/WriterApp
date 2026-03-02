using System;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
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
        private readonly IHostEnvironment _hostEnvironment;

        public FeedbackController(
            IConfiguration configuration,
            ILogger<FeedbackController> logger,
            IUserIdResolver userIdResolver,
            IHostEnvironment hostEnvironment)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _userIdResolver = userIdResolver ?? throw new ArgumentNullException(nameof(userIdResolver));
            _hostEnvironment = hostEnvironment ?? throw new ArgumentNullException(nameof(hostEnvironment));
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

            string toAddress = _configuration["Feedback:Email:To"] ?? "johan.davidsson@hotmail.se";
            string fromAddress = _configuration["Feedback:Email:From"] ?? "writerapp@localhost";
            string smtpHost = _configuration["Feedback:Smtp:Host"] ?? string.Empty;
            int smtpPort = _configuration.GetValue<int?>("Feedback:Smtp:Port") ?? 25;
            bool useSsl = _configuration.GetValue<bool?>("Feedback:Smtp:UseSsl") ?? true;
            string smtpUser = _configuration["Feedback:Smtp:Username"] ?? string.Empty;
            string smtpPassword = _configuration["Feedback:Smtp:Password"] ?? string.Empty;
            string kindLabel = string.Equals(type, "bug", StringComparison.OrdinalIgnoreCase) ? "Bug" : "Enhancement";
            string senderName = User.Identity?.Name
                ?? User.FindFirstValue(ClaimTypes.Email)
                ?? userId;
            string body = BuildFeedbackBody(kindLabel, subject, senderName, userId, description, request);

            if (string.IsNullOrWhiteSpace(smtpHost))
            {
                if (_hostEnvironment.IsDevelopment())
                {
                    _logger.LogInformation(
                        "Feedback captured locally because SMTP host is not configured. UserId={UserId} Subject={Subject} Type={Type}{NewLine}{Body}",
                        userId,
                        subject,
                        kindLabel,
                        Environment.NewLine,
                        body);
                    return Ok(new { message = "Feedback captured locally (SMTP not configured)." });
                }

                _logger.LogWarning("Feedback SMTP host is not configured.");
                return StatusCode((int)HttpStatusCode.ServiceUnavailable, new { message = "Feedback email is not configured." });
            }

            try
            {
                using MailMessage message = new(fromAddress, toAddress)
                {
                    Subject = $"[WriterApp Feedback] {kindLabel}: {subject}",
                    Body = body
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
                return StatusCode((int)HttpStatusCode.InternalServerError, $"Feedback send failed: {ex.Message}");
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

        private static string BuildFeedbackBody(
            string kindLabel,
            string subject,
            string senderName,
            string userId,
            string description,
            FeedbackSubmitRequest request)
        {
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

            return body.ToString();
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
