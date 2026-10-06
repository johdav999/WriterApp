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
using Microsoft.EntityFrameworkCore;
using WriterApp.Data;
using WriterApp.Application.Security;

namespace WriterApp.Controllers
{
    [ApiController]
    [Route("api/covers")]
    [Authorize]
    public sealed partial class CoversController : ControllerBase
    {
        private readonly ICoverImageService _coverImageService;
        private readonly ILogger<CoversController> _logger;
        private readonly AppDbContext? _db;
        private readonly IUserIdResolver? _userIds;
        private readonly CoverAssetService? _assets;

        public CoversController(
            ICoverImageService coverImageService,
            ILogger<CoversController> logger, AppDbContext? db = null, IUserIdResolver? userIds = null, CoverAssetService? assets = null)
        {
            _coverImageService = coverImageService ?? throw new ArgumentNullException(nameof(coverImageService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _db = db; _userIds = userIds;
            _assets=assets;
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
                CoverStudioContract.ValidatePrompt(prompt);
                if (prompt.ContractVersion == 1 && !await CurrentAsync(prompt, ct))
                    return Conflict(new { code = "cover.stale_source", message = "The owned project or document changed. Synchronize and generate again." });
                List<string> imageUrls = (await _coverImageService.GenerateCoverConceptsAsync(prompt, ct)).ToList();
                if(imageUrls.Count is <1 or >4)throw new System.IO.InvalidDataException("Generation returned an invalid concept count.");
                var identities=new List<CoverAssetIdentity?>();
                for(int i=0;i<imageUrls.Count;i++) {
                    if(imageUrls[i].StartsWith("data:",StringComparison.OrdinalIgnoreCase)) {
                        if(_assets is not null && _userIds is not null && prompt.ContractVersion==1) {
                            var inlineSource=await _assets.GenerationSourceAsync(_userIds.ResolveUserId(User),prompt,imageUrls[i],ct);
                            var inline=await _assets.RegisterInlineAsync(_userIds.ResolveUserId(User),inlineSource,CoverStudioContract.ReadPng(imageUrls[i]),ct);
                            identities.Add(inline.Asset);
                        } else identities.Add(null);
                        continue;
                    }
                    if(_assets is null || _userIds is null || imageUrls.Count is <1 or >4)
                        throw new System.IO.InvalidDataException("Remote cover results require the owned asset backend and configured trusted PNG storage.");
                    string owner=_userIds.ResolveUserId(User);
                    var source=await _assets.GenerationSourceAsync(owner,prompt,imageUrls[i],ct);
                    var asset=await _assets.MaterializeAsync(owner,source,imageUrls[i],false,ct);
                    imageUrls[i]="data:image/png;base64,"+Convert.ToBase64String(asset.Bytes);identities.Add(asset.Asset);
                }
                if (prompt.ContractVersion == 1) {
                    if (!await CurrentAsync(prompt, ct)) return Conflict(new { code = "cover.stale_source", message = "The project changed during generation. Generate again." });
                    if (imageUrls.Count is < 1 or > 4) return UnprocessableEntity(new { message = "Generation returned an invalid concept count." });
                    foreach (string image in imageUrls) CoverStudioContract.ReadPng(image);
                }
                return Ok(new CoverGenerationResponse(imageUrls, prompt.ContractVersion, prompt.ProjectId, prompt.DocumentId, prompt.ExpectedMetadataRevision, prompt.ExpectedDocumentVersion,identities));
            }
            catch(CoverAssetException e){return StatusCode(e.Status,new {code=e.Code,message=e.Message});}
            catch (System.IO.InvalidDataException ex) { return UnprocessableEntity(new { message = ex.Message }); }
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
            catch(Exception e) when(e is System.IO.IOException or HttpRequestException or System.Data.Common.DbException or DbUpdateException) {
                return StatusCode(503,new{code="cover.asset_unavailable",message="Owned cover storage was not acknowledged. Previous cover/cache is preserved. Check the asset migration/storage configuration or retry deliberately."});
            }
        }

        private async Task<bool> CurrentAsync(CoverPrompt prompt, CancellationToken ct)
        {
            if (_db is null || _userIds is null) return false;
            string owner = _userIds.ResolveUserId(User);
            return await _db.Projects.AsNoTracking().AnyAsync(p => p.Id == prompt.ProjectId && p.OwnerUserId == owner && p.MetadataRevision == prompt.ExpectedMetadataRevision, ct)
                && await _db.Documents.AsNoTracking().AnyAsync(d => d.Id == prompt.DocumentId && d.ProjectId == prompt.ProjectId && d.OwnerUserId == owner && d.DeletedAtUtc == null && !d.IsArchived, ct)
                && await _db.DocumentSyncRecords.AsNoTracking().AnyAsync(r => r.DocumentId == prompt.DocumentId && r.OwnerUserId == owner && r.Version == prompt.ExpectedDocumentVersion && !r.IsDeleted && !r.IsTrashed, ct);
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
