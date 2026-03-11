using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using WriterApp.Application.Continuity;
using WriterApp.Application.Documents;
using WriterApp.Application.Security;
using WriterApp.Application.Subscriptions;
using WriterApp.Data.Documents;
using WriterApp.Domain.Documents;

namespace WriterApp.Controllers
{
    [ApiController]
    [Route("api/documents/{documentId:guid}/bibles")]
    [Authorize]
    public sealed class DocumentBiblesController : ControllerBase
    {
        private readonly IDocumentRepository _documents;
        private readonly ISectionRepository _sections;
        private readonly IPageRepository _pages;
        private readonly IUserIdResolver _userIdResolver;
        private readonly IBibleStore _bibleStore;
        private readonly BibleRefreshService _refreshService;
        private readonly IEntitlementService _entitlementService;
        private readonly ILogger<DocumentBiblesController> _logger;

        public DocumentBiblesController(
            IDocumentRepository documents,
            ISectionRepository sections,
            IPageRepository pages,
            IUserIdResolver userIdResolver,
            IBibleStore bibleStore,
            BibleRefreshService refreshService,
            IEntitlementService entitlementService,
            ILogger<DocumentBiblesController> logger)
        {
            _documents = documents ?? throw new ArgumentNullException(nameof(documents));
            _sections = sections ?? throw new ArgumentNullException(nameof(sections));
            _pages = pages ?? throw new ArgumentNullException(nameof(pages));
            _userIdResolver = userIdResolver ?? throw new ArgumentNullException(nameof(userIdResolver));
            _bibleStore = bibleStore ?? throw new ArgumentNullException(nameof(bibleStore));
            _refreshService = refreshService ?? throw new ArgumentNullException(nameof(refreshService));
            _entitlementService = entitlementService ?? throw new ArgumentNullException(nameof(entitlementService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet("{bibleType}")]
        public async Task<ActionResult<BibleSnapshotDto>> GetSnapshot(
            Guid documentId,
            string bibleType,
            CancellationToken ct)
        {
            if (!TryParseBibleType(bibleType, out BibleType parsedType))
            {
                return BadRequest(new { message = $"Unsupported bible type '{bibleType}'." });
            }

            string userId;
            try
            {
                userId = _userIdResolver.ResolveUserId(User);
            }
            catch (SecurityException)
            {
                return Unauthorized();
            }

            ActionResult? gate = await EnsureFeatureAllowedAsync(userId, FeatureKey.StoryCanon, "story.canon");
            if (gate is not null)
            {
                return gate;
            }

            DocumentRecord? documentRecord = await _documents.GetAsync(documentId, userId, ct);
            if (documentRecord is null)
            {
                return NotFound();
            }

            IReadOnlyList<SectionRecord> sectionRecords = await _sections.ListByDocumentAsync(documentId, userId, ct);
            Document document = await BuildDocumentAsync(documentRecord, sectionRecords, userId, ct);
            BibleSnapshotState? snapshot = await _bibleStore.GetSnapshotAsync(documentId, parsedType, ct);
            if (snapshot is null)
            {
                return Ok(BibleSnapshotDto.Empty(parsedType.ToString()));
            }

            int changedSections = CountChangedSections(snapshot.Cursor, document);
            return Ok(BibleSnapshotDto.FromState(snapshot, changedSections));
        }

        [HttpPost("{bibleType}/refresh")]
        public async Task<ActionResult<BibleSnapshotDto>> Refresh(
            Guid documentId,
            string bibleType,
            [FromBody] RefreshBibleRequest? request,
            CancellationToken ct)
        {
            if (!TryParseBibleType(bibleType, out BibleType parsedType))
            {
                return BadRequest(new { message = $"Unsupported bible type '{bibleType}'." });
            }

            string userId;
            try
            {
                userId = _userIdResolver.ResolveUserId(User);
            }
            catch (SecurityException)
            {
                return Unauthorized();
            }

            ActionResult? gate = await EnsureFeatureAllowedAsync(userId, FeatureKey.CanonRefresh, "story.canon.refresh");
            if (gate is not null)
            {
                return gate;
            }

            DocumentRecord? documentRecord = await _documents.GetAsync(documentId, userId, ct);
            if (documentRecord is null)
            {
                return NotFound();
            }

            IReadOnlyList<SectionRecord> sectionRecords = await _sections.ListByDocumentAsync(documentId, userId, ct);
            if (sectionRecords.Count == 0)
            {
                return BadRequest(new { message = "Document has no sections." });
            }

            Document document = await BuildDocumentAsync(documentRecord, sectionRecords, userId, ct);
            Guid activeSectionId = request?.ActiveSectionId ?? sectionRecords.OrderBy(section => section.OrderIndex).First().Id;
            if (!sectionRecords.Any(section => section.Id == activeSectionId))
            {
                activeSectionId = sectionRecords.OrderBy(section => section.OrderIndex).First().Id;
            }

            BibleSnapshotState snapshot;
            try
            {
                snapshot = await _refreshService.RefreshAsync(
                    document,
                    userId,
                    activeSectionId,
                    parsedType,
                    request?.FullRebuild ?? false,
                    ct);
            }
            catch (EntitlementDeniedException ex)
            {
                ProblemDetails payload = EntitlementDeniedApiError.ToProblemDetails(ex);
                payload.Extensions["code"] = "entitlement_denied";
                payload.Extensions["traceId"] = HttpContext.TraceIdentifier;
                ObjectResult result = new(payload)
                {
                    StatusCode = StatusCodes.Status402PaymentRequired
                };
                result.ContentTypes.Add("application/problem+json");
                return result;
            }
            catch (BibleRefreshInvalidPayloadException ex)
            {
                _logger.LogWarning(
                    "Bible refresh returned invalid structured AI output. CorrelationId={CorrelationId} BibleType={BibleType} DocumentId={DocumentId} ActionId={ActionId} RepairAttempted={RepairAttempted} Reason={Reason} RawPayload={RawPayload}",
                    HttpContext.TraceIdentifier,
                    ex.BibleType,
                    ex.DocumentId,
                    ex.ActionId,
                    ex.RepairAttempted,
                    ex.FailureReason,
                    ex.RawPayload);

                bool isCharacterInvalidStructuredData = ex.BibleType == BibleType.Character;
                ProblemDetails payload = new()
                {
                    Status = isCharacterInvalidStructuredData
                        ? StatusCodes.Status422UnprocessableEntity
                        : StatusCodes.Status502BadGateway,
                    Title = isCharacterInvalidStructuredData
                        ? "AI returned invalid structured data"
                        : "Bible refresh failed",
                    Detail = isCharacterInvalidStructuredData
                        ? (ex.RepairAttempted
                            ? "The AI returned invalid structured data for the character bible after one repair attempt. Please retry."
                            : "The AI returned invalid structured data for the character bible. Please retry.")
                        : "The AI returned an invalid refresh payload. Please retry."
                };
                payload.Extensions["code"] = isCharacterInvalidStructuredData
                    ? "ai_invalid_structured_data"
                    : "bible_refresh_invalid_payload";
                payload.Extensions["traceId"] = HttpContext.TraceIdentifier;
                payload.Extensions["bibleType"] = ex.BibleType.ToString();
                ObjectResult result = new(payload)
                {
                    StatusCode = payload.Status
                };
                result.ContentTypes.Add("application/problem+json");
                return result;
            }

            int changedSections = CountChangedSections(snapshot.Cursor, document);
            return Ok(BibleSnapshotDto.FromState(snapshot, changedSections));
        }

        private async Task<Document> BuildDocumentAsync(
            DocumentRecord record,
            IReadOnlyList<SectionRecord> sections,
            string ownerUserId,
            CancellationToken ct)
        {
            List<Section> domainSections = new();
            foreach (SectionRecord sectionRecord in sections.OrderBy(section => section.OrderIndex))
            {
                IReadOnlyList<PageRecord> pages = await _pages.ListBySectionAsync(sectionRecord.Id, ownerUserId, ct);
                string content = string.Join("\n\n", pages.Select(page => page.Content ?? string.Empty));

                domainSections.Add(new Section
                {
                    SectionId = sectionRecord.Id,
                    Order = sectionRecord.OrderIndex,
                    Title = sectionRecord.Title,
                    Content = new SectionContent
                    {
                        Format = "html",
                        Value = content
                    },
                    Notes = sectionRecord.NarrativePurpose ?? string.Empty,
                    CreatedUtc = sectionRecord.CreatedAt.UtcDateTime,
                    ModifiedUtc = sectionRecord.UpdatedAt.UtcDateTime
                });
            }

            Chapter chapter = new()
            {
                Order = 0,
                Title = string.IsNullOrWhiteSpace(record.Title) ? "Draft" : record.Title,
                Sections = domainSections
            };

            return new Document
            {
                DocumentId = record.Id,
                Metadata = new DocumentMetadata
                {
                    Title = record.Title,
                    Language = string.IsNullOrWhiteSpace(record.LanguageCode) ? "en" : record.LanguageCode,
                    CreatedUtc = record.CreatedAt.UtcDateTime,
                    ModifiedUtc = record.UpdatedAt.UtcDateTime
                },
                Chapters = new List<Chapter> { chapter },
                Synopsis = new Synopsis()
            };
        }

        private static bool TryParseBibleType(string value, out BibleType bibleType)
        {
            if (Enum.TryParse(value, ignoreCase: true, out bibleType))
            {
                return true;
            }

            bibleType = default;
            return false;
        }

        private static int CountChangedSections(BibleRefreshCursor cursor, Document document)
        {
            Dictionary<Guid, string> currentHashes = new();
            foreach (Section section in document.Chapters.SelectMany(chapter => chapter.Sections))
            {
                string plain = WriterApp.Application.State.PlainTextMapper.ToPlainText(section.Content.Value ?? string.Empty);
                currentHashes[section.SectionId] = ComputeHash(plain);
            }

            int changed = 0;
            foreach (KeyValuePair<Guid, string> entry in currentHashes)
            {
                if (!cursor.SectionHashes.TryGetValue(entry.Key, out string? existingHash)
                    || !string.Equals(existingHash, entry.Value, StringComparison.Ordinal))
                {
                    changed++;
                }
            }

            return changed;
        }

        private static string ComputeHash(string input)
        {
            byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input ?? string.Empty));
            return Convert.ToHexString(bytes);
        }

        private async Task<ActionResult?> EnsureFeatureAllowedAsync(string userId, FeatureKey feature, string featureCode)
        {
            UserEntitlements entitlements = await _entitlementService.GetEntitlementsAsync(userId);
            PlanTier userTier = _entitlementService.GetUserTier(entitlements);
            if (FeatureRegistry.IsFeatureAllowed(feature, userTier))
            {
                return null;
            }

            PlanTier requiredTier = FeatureRegistry.FeatureMinimumTier[feature];
            _logger.LogInformation(
                "FeatureAccessDenied FeatureKey={FeatureKey} UserTier={UserTier} RequiredTier={RequiredTier}",
                feature,
                userTier,
                requiredTier);

            ProblemDetails problem = EntitlementDeniedApiError.ForFeature(
                featureCode,
                $"Available in {requiredTier} plan.");
            problem.Extensions["code"] = "entitlement_denied";
            problem.Extensions["traceId"] = HttpContext.TraceIdentifier;

            ObjectResult result = new(problem)
            {
                StatusCode = StatusCodes.Status402PaymentRequired
            };
            result.ContentTypes.Add("application/problem+json");
            return result;
        }
    }

    public sealed record RefreshBibleRequest(bool FullRebuild, Guid? ActiveSectionId);

    public sealed record BibleSnapshotDto(
        string BibleType,
        int SchemaVersion,
        string ContentJson,
        DateTimeOffset? LastRefreshUtc,
        int ChangedSectionsSinceLastRefresh,
        int ChangedSections,
        int NewSections,
        int DeletedSections,
        int NewEntries,
        int UpdatedEntries,
        int Flags)
    {
        public static BibleSnapshotDto Empty(string bibleType) =>
            new(
                bibleType,
                1,
                "{}",
                null,
                0,
                0,
                0,
                0,
                0,
                0,
                0);

        public static BibleSnapshotDto FromState(BibleSnapshotState state, int changedSectionsSinceLastRefresh) =>
            new(
                state.BibleType.ToString(),
                state.SchemaVersion,
                state.ContentJson,
                state.LastRefreshUtc,
                changedSectionsSinceLastRefresh,
                state.Stats.ChangedSections,
                state.Stats.NewSections,
                state.Stats.DeletedSections,
                state.Stats.NewEntries,
                state.Stats.UpdatedEntries,
                state.Stats.Flags);
    }
}
