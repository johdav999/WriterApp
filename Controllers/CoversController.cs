using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using WriterApp.Application.Covers;
using WriterApp.Application.Subscriptions;
using WriterApp.Shared;

namespace WriterApp.Controllers
{
    [ApiController]
    [Route("api/covers")]
    [Authorize]
    public sealed class CoversController : ControllerBase
    {
        private readonly ICoverImageService _coverImageService;
        private readonly ILogger<CoversController> _logger;

        public CoversController(
            ICoverImageService coverImageService,
            ILogger<CoversController> logger)
        {
            _coverImageService = coverImageService ?? throw new ArgumentNullException(nameof(coverImageService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpPost("generate")]
        public async Task<ActionResult<CoverGenerationResponse>> Generate(
            [FromBody] CoverPrompt prompt,
            CancellationToken ct)
        {
            if (prompt is null)
            {
                return BadRequest(new { message = "Request body is required." });
            }

            try
            {
                List<string> imageUrls = await _coverImageService.GenerateCoverConceptsAsync(prompt, ct);
                return Ok(new CoverGenerationResponse(imageUrls));
            }
            catch (EntitlementDeniedException ex)
            {
                ProblemDetails problem = EntitlementDeniedApiError.ToProblemDetails(ex);
                problem.Extensions["code"] = "entitlement_denied";
                problem.Extensions["traceId"] = HttpContext.TraceIdentifier;
                return StatusCode(StatusCodes.Status402PaymentRequired, problem);
            }
            catch (CoverImageGenerationException ex)
            {
                _logger.LogWarning(ex, "Cover generation failed. Code={Code}", ex.Code);
                int statusCode = MapBlockedErrorStatusCode(ex.Code);
                ProblemDetails problem = BuildProblemDetails(
                    statusCode,
                    statusCode == StatusCodes.Status429TooManyRequests ? "Try again later" : "Cover generation unavailable",
                    ex.Message,
                    ex.Code);

                return StatusCode(statusCode, problem);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(BuildProblemDetails(
                    StatusCodes.Status400BadRequest,
                    "Invalid request",
                    ex.Message,
                    "invalid_request"));
            }
        }

        private ProblemDetails BuildProblemDetails(int statusCode, string title, string detail, string code)
        {
            ProblemDetails problem = new()
            {
                Status = statusCode,
                Title = title,
                Detail = detail
            };
            problem.Extensions["code"] = code;
            problem.Extensions["traceId"] = HttpContext.TraceIdentifier;
            return problem;
        }

        private static int MapBlockedErrorStatusCode(string? errorCode)
        {
            if (string.IsNullOrWhiteSpace(errorCode))
            {
                return StatusCodes.Status503ServiceUnavailable;
            }

            return errorCode switch
            {
                "ai.rate_limited" => StatusCodes.Status429TooManyRequests,
                "ai.provider_missing" => StatusCodes.Status503ServiceUnavailable,
                "ai.provider_unavailable" => StatusCodes.Status503ServiceUnavailable,
                "ai.disabled" => StatusCodes.Status503ServiceUnavailable,
                "auth.required" => StatusCodes.Status401Unauthorized,
                _ => StatusCodes.Status503ServiceUnavailable
            };
        }
    }
}
