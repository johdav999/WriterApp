using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace WriterApp.Application.Feedback
{
    public sealed class MailgunFeedbackEmailSender : IFeedbackEmailSender
    {
        private const int MaxSubjectLength = 120;
        private const int MaxMessageLength = 8000;
        private const int MaxFieldLength = 512;

        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly IHostEnvironment _hostEnvironment;
        private readonly ILogger<MailgunFeedbackEmailSender> _logger;

        public MailgunFeedbackEmailSender(
            HttpClient httpClient,
            IConfiguration configuration,
            IHostEnvironment hostEnvironment,
            ILogger<MailgunFeedbackEmailSender> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _hostEnvironment = hostEnvironment ?? throw new ArgumentNullException(nameof(hostEnvironment));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<FeedbackEmailSendResult> SendAsync(FeedbackEmailRequest request, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(request);

            string apiKey = ResolveMailgunApiKey(_configuration);
            string configuredBaseUrl = (_configuration["MailGunBaseUrl"] ?? string.Empty).Trim();
            string domain = (_configuration["MailGunDomain"] ?? string.Empty).Trim();
            string fromEmail = (_configuration["MailGunFromEmail"] ?? string.Empty).Trim();
            string fromName = (_configuration["MailGunFromName"] ?? "Prosa Feedback").Trim();
            string toEmail = (_configuration["FeedbackToEmail"] ?? string.Empty).Trim();
            string baseUrl = NormalizeBaseUrl(configuredBaseUrl);

            if (string.IsNullOrWhiteSpace(apiKey)
                || string.IsNullOrWhiteSpace(domain)
                || string.IsNullOrWhiteSpace(fromEmail)
                || string.IsNullOrWhiteSpace(toEmail))
            {
                if (_hostEnvironment.IsDevelopment())
                {
                    _logger.LogInformation(
                        "Feedback captured locally because Mailgun is not fully configured. UserId={UserId} Subject={Subject}",
                        request.UserId,
                        request.Subject);
                    return FeedbackEmailSendResult.Success("Feedback captured locally (Mailgun not configured).");
                }

                _logger.LogWarning(
                    "Feedback email is not configured. Missing Mailgun settings. HasApiKey={HasApiKey} HasBaseUrl={HasBaseUrl} HasDomain={HasDomain} HasFromEmail={HasFromEmail} HasToEmail={HasToEmail}",
                    !string.IsNullOrWhiteSpace(apiKey),
                    !string.IsNullOrWhiteSpace(configuredBaseUrl),
                    !string.IsNullOrWhiteSpace(domain),
                    !string.IsNullOrWhiteSpace(fromEmail),
                    !string.IsNullOrWhiteSpace(toEmail));
                return FeedbackEmailSendResult.Failure("Feedback email is not configured.");
            }

            string subject = BuildSubject(request);
            string body = BuildBody(request);
            string requestUri = $"{baseUrl}/v3/{domain}/messages";
            AuthenticationHeaderValue authorization = BuildBasicAuth(apiKey);
            string keyFingerprint = BuildKeyFingerprint(apiKey);
            string[] fieldNames = ["from", "to", "subject", "text"];

            _logger.LogInformation(
                "Sending feedback through Mailgun. HasConfiguredBaseUrl={HasConfiguredBaseUrl} RequestUri={RequestUri}",
                !string.IsNullOrWhiteSpace(configuredBaseUrl),
                requestUri);
            _logger.LogInformation(
                "Mailgun feedback diagnostics. ApiKeyPresent={ApiKeyPresent} ApiKeyStartsWithKeyDash={ApiKeyStartsWithKeyDash} ApiKeyFingerprint={ApiKeyFingerprint} ApiKeyLength={ApiKeyLength} DomainPresent={DomainPresent} FromEmailPresent={FromEmailPresent} RecipientPresent={RecipientPresent} AuthScheme={AuthScheme} ContentKind={ContentKind} FieldNames={FieldNames}",
                !string.IsNullOrWhiteSpace(apiKey),
                apiKey.StartsWith("key-", StringComparison.Ordinal),
                keyFingerprint,
                apiKey.Length,
                !string.IsNullOrWhiteSpace(domain),
                !string.IsNullOrWhiteSpace(fromEmail),
                !string.IsNullOrWhiteSpace(toEmail),
                authorization.Scheme,
                "multipart/form-data",
                string.Join(",", fieldNames));

            using HttpRequestMessage httpRequest = new(HttpMethod.Post, requestUri);
            httpRequest.Headers.Authorization = authorization;
            MultipartFormDataContent content = new();
            content.Add(new StringContent($"{fromName} <{fromEmail}>"), "from");
            content.Add(new StringContent(toEmail), "to");
            content.Add(new StringContent(subject), "subject");
            content.Add(new StringContent(body), "text");
            httpRequest.Content = content;

            try
            {
                using HttpResponseMessage response = await _httpClient.SendAsync(httpRequest, ct);
                string responseBody = await response.Content.ReadAsStringAsync(ct);
                if (response.IsSuccessStatusCode)
                {
                    return FeedbackEmailSendResult.Success("Feedback sent.");
                }

                string summary = SummarizeProviderResponse(responseBody);
                _logger.LogWarning(
                    "Feedback Mailgun request failed. StatusCode={StatusCode} ResponseSummary={ResponseSummary}",
                    (int)response.StatusCode,
                    summary);
                return FeedbackEmailSendResult.Failure(
                    "Could not send feedback right now. Please retry.",
                    response.StatusCode,
                    summary);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Feedback Mailgun request failed unexpectedly.");
                return FeedbackEmailSendResult.Failure("Could not send feedback right now. Please retry.");
            }
        }

        private static AuthenticationHeaderValue BuildBasicAuth(string apiKey)
        {
            string token = Convert.ToBase64String(Encoding.ASCII.GetBytes($"api:{apiKey}"));
            return new AuthenticationHeaderValue("Basic", token);
        }

        private static string ResolveMailgunApiKey(IConfiguration configuration)
        {
            string configuredValue = (configuration["MailGunAPIKey"] ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(configuredValue))
            {
                return configuredValue;
            }

            string userValue = (Environment.GetEnvironmentVariable("MailGunAPIKey", EnvironmentVariableTarget.User) ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(userValue))
            {
                return userValue;
            }

            return (Environment.GetEnvironmentVariable("MailGunAPIKey", EnvironmentVariableTarget.Machine) ?? string.Empty).Trim();
        }

        private static string NormalizeBaseUrl(string? configuredBaseUrl)
        {
            if (string.IsNullOrWhiteSpace(configuredBaseUrl))
            {
                return "https://api.mailgun.net";
            }

            return configuredBaseUrl.Trim().TrimEnd('/');
        }

        private static string BuildKeyFingerprint(string apiKey)
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                return "<empty>";
            }

            string prefix = apiKey.Length >= 4 ? apiKey[..4] : apiKey;
            string suffix = apiKey.Length >= 4 ? apiKey[^4..] : apiKey;
            return $"{prefix}...{suffix}";
        }

        private static string BuildSubject(FeedbackEmailRequest request)
        {
            string kindLabel = NormalizeType(request.Type);
            string trimmedSubject = TrimToLength(request.Subject, MaxSubjectLength);
            return $"[Prosa Feedback] {kindLabel}: {trimmedSubject}";
        }

        private static string BuildBody(FeedbackEmailRequest request)
        {
            StringBuilder body = new();
            body.AppendLine($"Type: {NormalizeType(request.Type)}");
            body.AppendLine($"Subject: {TrimToLength(request.Subject, MaxSubjectLength)}");
            body.AppendLine($"User: {TrimToLength(request.UserDisplayName, MaxFieldLength)}");
            body.AppendLine($"User Email: {TrimToLength(request.UserEmail, MaxFieldLength)}");
            body.AppendLine($"UserId: {TrimToLength(request.UserId, MaxFieldLength)}");
            body.AppendLine($"Route/Page: {TrimToLength(request.RouteOrPage, MaxFieldLength)}");
            body.AppendLine($"Timestamp (UTC): {request.SubmittedAtUtc:O}");
            body.AppendLine();
            body.AppendLine("Message:");
            body.AppendLine(TrimToLength(request.Message, MaxMessageLength));

            if (!string.IsNullOrWhiteSpace(request.DiagnosticsVersion)
                || !string.IsNullOrWhiteSpace(request.DiagnosticsUserAgent)
                || !string.IsNullOrWhiteSpace(request.DiagnosticsSummary))
            {
                body.AppendLine();
                body.AppendLine("Diagnostics:");
                if (!string.IsNullOrWhiteSpace(request.DiagnosticsVersion))
                {
                    body.AppendLine($"Version: {TrimToLength(request.DiagnosticsVersion, MaxFieldLength)}");
                }

                if (!string.IsNullOrWhiteSpace(request.DiagnosticsUserAgent))
                {
                    body.AppendLine($"UserAgent: {TrimToLength(request.DiagnosticsUserAgent, MaxFieldLength)}");
                }

                if (!string.IsNullOrWhiteSpace(request.DiagnosticsSummary))
                {
                    body.AppendLine("Details:");
                    body.AppendLine(TrimToLength(request.DiagnosticsSummary, MaxMessageLength));
                }
            }

            return body.ToString();
        }

        private static string NormalizeType(string? type)
            => string.Equals(type, "bug", StringComparison.OrdinalIgnoreCase) ? "Bug" : "Enhancement";

        private static string TrimToLength(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string trimmed = value.Trim();
            return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
        }

        private static string SummarizeProviderResponse(string? responseBody)
        {
            if (string.IsNullOrWhiteSpace(responseBody))
            {
                return "<empty>";
            }

            string singleLine = responseBody.Replace(Environment.NewLine, " ").Replace('\n', ' ').Replace('\r', ' ').Trim();
            return singleLine.Length <= 400 ? singleLine : singleLine[..400];
        }
    }
}
