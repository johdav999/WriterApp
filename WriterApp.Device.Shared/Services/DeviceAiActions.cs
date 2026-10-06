using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using WriterApp.Application.AI;
using WriterApp.Application.Usage;
using WriterApp.Device.Shared.Components;
using WriterApp.Device.Shared.Storage;

namespace WriterApp.Device.Shared.Services;

public enum DeviceAiAction { Rewrite, Expand, Shorten, Summarize, Custom, Translate, StyleQuality }
public enum DeviceAiApplyTarget { ManuscriptSelection, ManuscriptAppend, TranslationSelection, Analysis, ManuscriptPage }
public enum DeviceAiFailure { Authentication, Upgrade, Quota, Safety, Timeout, Offline, Server, Invalid }
public sealed class DeviceAiException(DeviceAiFailure kind, string message) : Exception(message)
{
    public DeviceAiFailure Kind { get; } = kind;
}

public interface IDeviceAiApi
{
    Task<WriterApp.Shared.OnboardingDemoStatus?> GetOnboardingDemoAsync(CancellationToken ct) => Task.FromResult<WriterApp.Shared.OnboardingDemoStatus?>(null);
    Task<WriterApp.Application.Synopsis.SynopsisAiResponseDto> ExecuteSynopsisAsync(Guid documentId, string mode,
        WriterApp.Application.Synopsis.SynopsisAiRequestDto request, CancellationToken ct) =>
        throw new DeviceAiException(DeviceAiFailure.Invalid, "Update the backend to support revision-checked synopsis coaching.");
    Task<WriterApp.Shared.WritingAvailability> GetWritingAvailabilityAsync(CancellationToken ct) => Task.FromResult(new WriterApp.Shared.WritingAvailability([], false));
    Task<AiUsageStatusDto> GetUsageAsync(CancellationToken ct);
    Task<AiActionExecuteResponseDto> ExecuteAsync(string key, AiActionExecuteRequestDto request, CancellationToken ct);
}

public sealed class DeviceAiApi(HttpClient http) : IDeviceAiApi
{
    public async Task<WriterApp.Shared.OnboardingDemoStatus?> GetOnboardingDemoAsync(CancellationToken ct) {
        using var response=await http.GetAsync("api/onboarding/demo/status",ct);
        if(response.StatusCode==HttpStatusCode.NotFound)return null;
        await CheckAsync(response,ct);
        await response.Content.LoadIntoBufferAsync(16_384,ct);
        return await response.Content.ReadFromJsonAsync<WriterApp.Shared.OnboardingDemoStatus>(cancellationToken:ct);
    }
    public async Task<WriterApp.Application.Synopsis.SynopsisAiResponseDto> ExecuteSynopsisAsync(Guid documentId, string mode,
        WriterApp.Application.Synopsis.SynopsisAiRequestDto request, CancellationToken ct) {
        WriterApp.Shared.SynopsisCoaching.Validate(mode, request);
        using var response = await http.PostAsJsonAsync($"api/documents/{documentId}/synopsis/ai/{mode}", request, ct);
        await CheckAsync(response,ct);
        return await response.Content.ReadFromJsonAsync<WriterApp.Application.Synopsis.SynopsisAiResponseDto>(cancellationToken:ct)
            ?? throw new DeviceAiException(DeviceAiFailure.Server,"The synopsis response was empty.");
    }
    public async Task<WriterApp.Shared.WritingAvailability> GetWritingAvailabilityAsync(CancellationToken ct) {
        using var response = await http.GetAsync("api/ai/actions/writing-availability", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return new([], false);
        await CheckAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<WriterApp.Shared.WritingAvailability>(cancellationToken: ct)
            ?? throw new DeviceAiException(DeviceAiFailure.Server, "Writing availability is unavailable.");
    }
    public async Task<AiUsageStatusDto> GetUsageAsync(CancellationToken ct)
    {
        using var response = await http.GetAsync("api/ai/status", ct);
        await CheckAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<AiUsageStatusDto>(cancellationToken: ct)
            ?? throw new DeviceAiException(DeviceAiFailure.Server, "The AI status response was empty.");
    }
    public async Task<AiActionExecuteResponseDto> ExecuteAsync(string key, AiActionExecuteRequestDto request, CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync($"api/ai/actions/{key}/execute", request, ct);
        await CheckAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<AiActionExecuteResponseDto>(cancellationToken: ct)
            ?? throw new DeviceAiException(DeviceAiFailure.Server, "The AI response was empty.");
    }
    private static async Task CheckAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;
        string? code = null;
        try
        {
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            if (json.RootElement.ValueKind == JsonValueKind.Object
                && json.RootElement.TryGetProperty("code", out var value)
                && value.ValueKind == JsonValueKind.String) code = value.GetString();
        }
        catch (JsonException) { }
        var kind = Classify(response.StatusCode, code);
        if (code == "ai.stale_source") throw new DeviceAiException(DeviceAiFailure.Invalid,"The cloud manuscript changed. Synchronize and run AI again; writing is unchanged.");
        if (code == "ai.invalid_section_revision") throw new DeviceAiException(DeviceAiFailure.Invalid,
            "The AI returned an incomplete or invalid section revision. Select a shorter passage and try again; your writing is unchanged.");
        if (code == "ai.style_review_rejected") throw new DeviceAiException(DeviceAiFailure.Invalid,
            "The AI returned style suggestions that could not be validated against your writing. Run the coach again or select a shorter passage; your writing is unchanged.");
        if (code == "ai.style_review_incomplete") throw new DeviceAiException(DeviceAiFailure.Invalid,
            "The AI style review stopped before it was complete. Select a shorter passage and run the coach again; your writing is unchanged.");
        if (code == "ai.rate_limited") throw new DeviceAiException(DeviceAiFailure.Server,
            "The AI service is handling too many requests. Wait a moment and try again; your writing is unchanged.");
        if (code == "ai.misconfigured") throw new DeviceAiException(DeviceAiFailure.Server,
            "The AI provider is not configured for this backend. Check the backend AI settings; your writing is unchanged.");
        string message = kind switch
        {
            DeviceAiFailure.Authentication => "Sign in again to use AI. Your writing is unchanged.",
            DeviceAiFailure.Upgrade => "This AI action needs a higher plan or active subscription. Your writing is unchanged.",
            DeviceAiFailure.Quota => "Your AI quota is exhausted. Your writing is unchanged.",
            DeviceAiFailure.Safety => "The AI service could not complete this request under its content rules. Your writing is unchanged.",
            DeviceAiFailure.Timeout => "The AI request timed out. Try again; your writing is unchanged.",
            DeviceAiFailure.Invalid => "The AI request could not be used. Check the selected text and try again.",
            _ => "The AI service is unavailable. Try again later; your writing is unchanged."
        };
        throw new DeviceAiException(kind, message);
    }
    public static DeviceAiFailure Classify(HttpStatusCode status, string? code)
    {
        if (status == HttpStatusCode.Unauthorized) return DeviceAiFailure.Authentication;
        if (code is "ai.quota_exceeded" or "AI_QUOTA_EXCEEDED") return DeviceAiFailure.Quota;
        if (code is "entitlement_denied" or "plan_upgrade_required" or "AI_SUBSCRIPTION_INACTIVE"
            || status is HttpStatusCode.PaymentRequired or HttpStatusCode.Forbidden) return DeviceAiFailure.Upgrade;
        if (code?.Contains("safety", StringComparison.OrdinalIgnoreCase) == true
            || code?.Contains("moderation", StringComparison.OrdinalIgnoreCase) == true
            || code is "ai.blocked") return DeviceAiFailure.Safety;
        if (status == HttpStatusCode.GatewayTimeout || code == "ai.timeout") return DeviceAiFailure.Timeout;
        if (code is "ai.invalid_section_revision" or "ai.style_review_rejected" or "ai.style_review_incomplete") return DeviceAiFailure.Invalid;
        if (status is HttpStatusCode.BadRequest or HttpStatusCode.RequestEntityTooLarge) return DeviceAiFailure.Invalid;
        return DeviceAiFailure.Server;
    }
}

public sealed record DeviceAiPrepared(string Key, AiActionExecuteRequestDto Request, DeviceAiAction Action,
    Guid LocalPageId, string BaseHtml, int From, int To, string SelectedText, string ApplyMode,
    WriterApp.Application.Synopsis.SynopsisAiRequestDto? SynopsisRequest = null)
{
    public DeviceAiApplyTarget Target => ApplyMode == "analysis" ? DeviceAiApplyTarget.Analysis
        : Action == DeviceAiAction.Translate ? DeviceAiApplyTarget.TranslationSelection
        : Action == DeviceAiAction.StyleQuality && Request.SelectionStart == 0 && SelectedText == Request.SurroundingText ? DeviceAiApplyTarget.ManuscriptPage
        : ApplyMode == "append" ? DeviceAiApplyTarget.ManuscriptAppend : DeviceAiApplyTarget.ManuscriptSelection;
    public void RequireManuscriptTarget()
    {
        string expectedKey = Action switch {
            DeviceAiAction.Rewrite => "rewrite.selection", DeviceAiAction.Expand => "expand.selection",
            DeviceAiAction.Shorten => "tighten.selection", DeviceAiAction.Translate => "translate.selection",
            DeviceAiAction.Custom or DeviceAiAction.Summarize or DeviceAiAction.StyleQuality => "custom_transform", _ => "" };
        if (Key != expectedKey || ApplyMode is not ("replace" or "append")
            || (Action is DeviceAiAction.Rewrite or DeviceAiAction.Expand or DeviceAiAction.Shorten or DeviceAiAction.Translate or DeviceAiAction.StyleQuality or DeviceAiAction.Custom && ApplyMode != "replace")
            || (ApplyMode == "replace" && (From >= To || string.IsNullOrWhiteSpace(SelectedText))))
            throw new DeviceAiException(DeviceAiFailure.Invalid,"The proposal has an invalid manuscript target.");
    }
}
public sealed record DeviceAiProposal(DeviceAiPrepared Prepared, string ProposedText, string? Summary,
    string SourceText, AiUsageStatusDto Usage, Guid ProposalId = default,
    WriterApp.Application.Documents.SectionSceneCardProposalDto? SceneCard = null, long AccountGeneration = -1, DateTimeOffset PreparedAt = default,
    WriterApp.Shared.AiHistoryOrigin? HistoryOrigin = null,string? BackendIdentity = null)
{
    public WriterApp.Shared.StyleQualityReport? StyleReview { get; init; }
}

public static class DeviceAiRequests
{
    private static readonly Regex Breaks = new(@"<br\s*/?>|</(?:p|h[1-6]|li|blockquote|pre)>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Tags = new(@"<[^>]*>", RegexOptions.Compiled);
    public static string PlainText(LocalPage page)
    {
        if (page.ContentFormat == LocalContentFormat.LegacyText) return page.Content;
        if (page.ContentFormat == LocalContentFormat.LegacyJson) return ""; // The active editor supplies its exact mapped text.
        return WebUtility.HtmlDecode(Tags.Replace(Breaks.Replace(page.Content, "\n"), "")).TrimEnd('\n');
    }
    public static string SectionText(LocalSection section, Guid activePage, string activePlain) =>
        string.Join("\n\n", section.Pages.OrderBy(p => p.OrderIndex)
            .Select(p => p.PageId == activePage ? activePlain : p.ContentFormat == LocalContentFormat.LegacyJson
                ? throw new DeviceAiException(DeviceAiFailure.Invalid,
                    "Convert the other legacy page before using a whole-section AI action. No page was omitted.")
                : PlainText(p)));

    public static DeviceAiPrepared Build(LocalDocument doc, LocalSection section, LocalPage page,
        AiEditorSnapshot editor, DeviceAiAction action, string? instruction = null, string? sectionContext = null,
        string translationSource = "auto", string translationStyle = "natural", string styleGoal = WriterApp.Shared.StyleQualityReview.DefaultGoal)
    {
        if (doc.ServerDocumentId is null || doc.ServerVersion is null || doc.SyncState != LocalSyncState.Synced)
            throw new DeviceAiException(DeviceAiFailure.Offline, "Sync this document before using AI. Local editing remains available.");
        if (doc.DeletedAtUtc is not null) throw new DeviceAiException(DeviceAiFailure.Invalid, "Restore this document before using AI.");
        bool selected = !string.IsNullOrWhiteSpace(editor.SelectedText) && editor.From < editor.To;
        if (action is DeviceAiAction.Rewrite or DeviceAiAction.Expand or DeviceAiAction.Shorten or DeviceAiAction.Translate or DeviceAiAction.StyleQuality && !selected)
            throw new DeviceAiException(DeviceAiFailure.Invalid, "Select text in the editor for this action.");
        if (action == DeviceAiAction.Custom && (string.IsNullOrWhiteSpace(instruction) || instruction.Length > 2000))
            throw new DeviceAiException(DeviceAiFailure.Invalid, "Enter a custom instruction of at most 2,000 characters.");
        if (action == DeviceAiAction.Custom && !selected)
            throw new DeviceAiException(DeviceAiFailure.Invalid, "Use the page-mapped writing review to revise a complete section. Custom revisions cannot be appended.");
        bool sectionScope = action == DeviceAiAction.Summarize;
        string key = action switch
        {
            DeviceAiAction.Rewrite => "rewrite.selection",
            DeviceAiAction.Expand => "expand.selection",
            DeviceAiAction.Shorten => "tighten.selection",
            DeviceAiAction.Translate => "translate.selection",
            _ => "custom_transform"
        };
        string context = sectionScope ? sectionContext ?? SectionText(section, page.PageId, editor.PlainText) : editor.PlainText;
        if (string.IsNullOrWhiteSpace(context))
            throw new DeviceAiException(DeviceAiFailure.Invalid, "Add writing before using AI.");
        var parameters = new Dictionary<string, object?>();
        if (action == DeviceAiAction.Translate)
        {
            if (string.IsNullOrWhiteSpace(instruction) || instruction.Length > 40) throw new DeviceAiException(DeviceAiFailure.Invalid,"Choose a target language.");
            parameters["target_language"] = instruction; parameters["source_language"] = translationSource; parameters["style"] = translationStyle;
        }
        if (action == DeviceAiAction.Rewrite)
        { parameters["tone"] = "Neutral"; parameters["length"] = "Same"; parameters["preserve_terms"] = true; }
        if (key == "custom_transform")
        {
            parameters["scope"] = sectionScope ? "section" : "selection";
            parameters["template"] = action == DeviceAiAction.Summarize
                ? "Summarize this section in two or three concise sentences. Preserve its facts and intent. Context: {context}"
                : action == DeviceAiAction.StyleQuality
                ? WriterApp.Shared.StyleQualityReview.GoalInstruction(styleGoal)
                : instruction!.Trim();
        }
        if (action == DeviceAiAction.StyleQuality) {
            parameters[WriterApp.Shared.StyleQualityReview.Parameter] = WriterApp.Shared.StyleQualityReview.Goal(styleGoal).Key;
            if (editor.SelectedText.Length > 60_000) throw new DeviceAiException(DeviceAiFailure.Invalid, "Check a shorter selection (maximum 60,000 characters).");
        }
        Guid documentId = doc.ServerDocumentId.Value;
        Guid sectionId = section.ServerSectionId ?? section.SectionId;
        Guid pageId = page.ServerPageId ?? page.PageId;
        var request = new AiActionExecuteRequestDto(documentId, sectionId, pageId,
            selected && !sectionScope ? editor.SelectionStart : null,
            selected && !sectionScope ? editor.SelectionEnd : null,
            selected && !sectionScope ? editor.SelectedText : null,
            context, null, parameters, doc.ServerVersion);
        return new(key, request, action, page.PageId, editor.Html, editor.From, editor.To, editor.SelectedText,
            sectionScope ? "append" : "replace");
    }
}

public sealed class DeviceAiService(IDeviceAiApi api, DeviceAccountService account, DeviceConnectivity network, DeviceHostOptions? host = null, DeviceAiHistoryService? history = null)
{
    // Resolving the scoped reporter connects durable history notifications before generation.
    private readonly DeviceAiHistoryService? _history = history;
    public async Task RequireWritingOutlineCapabilityAsync(CancellationToken ct)
    {
        if(!(await api.GetWritingAvailabilityAsync(ct)).SavedOutlineContext)
            throw new DeviceAiException(DeviceAiFailure.Upgrade,"Update the backend to support source-checked saved writing outline context. No generation was sent.");
    }
    public void RequireCurrentAccount(DeviceAiProposal proposal)
    {
        if (proposal.PreparedAt == default || DateTimeOffset.UtcNow - proposal.PreparedAt > TimeSpan.FromMinutes(30))
            throw new DeviceAiException(DeviceAiFailure.Invalid,"This proposal expired. Run AI again before applying.");
        if (!account.IsSignedIn || proposal.AccountGeneration != account.Generation)
            throw new DeviceAiException(DeviceAiFailure.Authentication,"The account changed. Run AI again before applying this proposal.");
        if(proposal.Prepared.Request.WritingOutline is not null && proposal.BackendIdentity!=host?.ApiBaseAddress.AbsoluteUri)
            throw new DeviceAiException(DeviceAiFailure.Authentication,"The writing backend changed. Generate and review a new proposal.");
    }
    public async Task<DeviceAiProposal> ProposeAsync(DeviceAiPrepared prepared, CancellationToken ct)
    {
        if (!network.IsOnline) throw new DeviceAiException(DeviceAiFailure.Offline, "Connect to use AI. Local writing remains available.");
        if (!account.IsSignedIn) throw new DeviceAiException(DeviceAiFailure.Authentication, "Sign in to use AI. Local writing remains available.");
        long generation = account.Generation;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(90));
        try
        {
            AiUsageStatusDto usage = await api.GetUsageAsync(timeout.Token);
            bool demo=WriterApp.Shared.OnboardingAiDemoRequest.IsRequested(prepared.Key,prepared.Request.Parameters);
            if(demo) {
                var status=await api.GetOnboardingDemoAsync(timeout.Token);
                if(status is not { Version:1,Available:true,Scope:"section",Workspace: { } workspace } || status.ActionKey!=prepared.Key
                    || workspace.DocumentId!=prepared.Request.DocumentId || workspace.SectionId!=prepared.Request.SectionId)
                    throw new DeviceAiException(DeviceAiFailure.Quota,"The server-authorized demo is unavailable, expired or already used. Refresh its status; ordinary AI requires its actual plan and quota.");
            }
            if (!demo && (!usage.AiEnabled || !usage.UiEnabled))
                throw new DeviceAiException(DeviceAiFailure.Upgrade, "AI is unavailable for this plan or environment. Local writing remains available.");
            if (!demo && usage.QuotaRemaining <= 0)
                throw new DeviceAiException(DeviceAiFailure.Quota, "Your AI quota is exhausted. Local writing remains available.");
            if (prepared.Request.ExpectedDocumentVersion is not null && !usage.SupportsDocumentVersionChecks)
                throw new DeviceAiException(DeviceAiFailure.Invalid, "Update the backend to support revision-checked AI before using this action. No AI request was sent.");
            if (prepared.Request.ExpectedCanonVersions is not null && !usage.SupportsCanonVersionChecks)
                throw new DeviceAiException(DeviceAiFailure.Invalid, "Update the backend to support canon revision checks. No AI request was sent.");
            if (prepared.Request.Parameters?.ContainsKey(WriterApp.Shared.TranslationStructures.Parameter) == true && !usage.SupportsStructuredTranslation)
                throw new DeviceAiException(DeviceAiFailure.Invalid, "Update the backend to support complete page-mapped translation. No AI request was sent.");
            if (prepared.Action == DeviceAiAction.StyleQuality && !usage.SupportsStyleQualityReview)
                throw new DeviceAiException(DeviceAiFailure.Invalid, "Update the backend to support explained style reviews. No AI request was sent.");
            AiActionExecuteResponseDto result;
            if (prepared.SynopsisRequest is { } synopsis) {
                if (!usage.SupportsSynopsisCoaching) throw new DeviceAiException(DeviceAiFailure.Invalid,"Update the backend to support revision-checked synopsis coaching. No AI request was sent.");
                string mode=prepared.Key switch { "synopsis.evaluate"=>"evaluate", "synopsis.questions"=>"questions", "synopsis.story_coach"=>"suggest", _=>throw new InvalidDataException("Invalid synopsis action.") };
                WriterApp.Shared.SynopsisCoaching.Validate(mode,synopsis);
                if(prepared.Request.DocumentId is null || prepared.Request.DocumentId==Guid.Empty || prepared.Request.ExpectedDocumentVersion!=synopsis.ExpectedDocumentVersion)
                    throw new InvalidDataException("The synopsis request has an invalid document or revision target.");
                var response=await api.ExecuteSynopsisAsync(prepared.Request.DocumentId!.Value,mode,synopsis,timeout.Token);
                WriterApp.Shared.SynopsisCoaching.ValidateResponse(prepared.Request.DocumentId.Value,mode,synopsis,response);
                result=new(response.ProposalId,null,mode=="suggest" ? response.ProposedText : response.OutputText,response.OutputText,
                    DateTimeOffset.UtcNow,prepared.Key,SourceDocumentVersion:response.SourceDocumentVersion);
            } else result = await api.ExecuteAsync(prepared.Key, prepared.Request, timeout.Token);
            timeout.Token.ThrowIfCancellationRequested();
            if (!account.IsSignedIn || account.Generation != generation)
                throw new DeviceAiException(DeviceAiFailure.Authentication,"The account changed during the request. Run AI again.");
            if (prepared.Request.ExpectedDocumentVersion is { } version && result.SourceDocumentVersion != version)
                throw new DeviceAiException(DeviceAiFailure.Invalid,"This backend did not confirm the analyzed revision. Update the backend before using this action.");
            if(prepared.Request.WritingOutline is { } outline && result.SourceOutlineFingerprint!=outline.Fingerprint)
                throw new DeviceAiException(DeviceAiFailure.Invalid,"The backend did not confirm the saved outline. Generate again against the matching backend.");
            if (prepared.Request.ExpectedCanonVersions is { } canon && (result.SourceCanonVersions is null
                || canon.Count != result.SourceCanonVersions.Count || canon.Any(p => !result.SourceCanonVersions.TryGetValue(p.Key, out var token) || token != p.Value)))
                throw new DeviceAiException(DeviceAiFailure.Invalid, "The backend did not confirm the analyzed canon revisions. Run the check again.");
            if (result.ActionKey != prepared.Key || result.ProposalId == Guid.Empty || string.IsNullOrWhiteSpace(result.ProposedText)
                || result.ProposedText.Length > 100_000)
                throw new DeviceAiException(DeviceAiFailure.Server, "The AI service returned an invalid proposal. Your writing is unchanged.");
            var sceneCard = result.ProposedSceneCard;
            if (prepared.Key is "scene.suggest" or "scene.refine"
                && WriterApp.Application.Documents.SceneCardAiProposalParser.TryParse(result.ProposedText, out var parsed, out _))
                sceneCard = parsed;
            var styleReview = prepared.Action == DeviceAiAction.StyleQuality
                ? WriterApp.Shared.StyleQualityReview.Parse(result.ProposedText, prepared.SelectedText) : null;
            string proposed = styleReview is null ? result.ProposedText
                : WriterApp.Shared.StyleQualityReview.Compose(styleReview, prepared.SelectedText, Enumerable.Range(0, styleReview.Edits.Count).ToArray());
            string? summary = styleReview is null ? result.ChangesSummary : null;
            return new(prepared, proposed, summary,
                prepared.ApplyMode == "replace" ? prepared.SelectedText : prepared.Request.SurroundingText ?? "", usage,
                result.ProposalId, sceneCard, generation, DateTimeOffset.UtcNow,
                host is not null && !string.IsNullOrWhiteSpace(account.AccountId) && prepared.Request.DocumentId is { } document && !string.IsNullOrWhiteSpace(result.SourceDocumentVersion)
                    ? new WriterApp.Shared.AiHistoryOrigin(LocalBibleStore.ScopeKey(host.ApiBaseAddress, account.AccountId), result.ProposalId, document,
                        prepared.Request.SectionId == Guid.Empty ? null : prepared.Request.SectionId, prepared.Request.PageId, prepared.Key, result.SourceDocumentVersion) : null,host?.ApiBaseAddress.AbsoluteUri) { StyleReview = styleReview };
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { throw new DeviceAiException(DeviceAiFailure.Timeout, "The AI request timed out. Your writing is unchanged."); }
        catch (HttpRequestException)
        { throw new DeviceAiException(DeviceAiFailure.Offline, "Cannot reach the AI service. Check the connection; your writing is unchanged."); }
        catch (DeviceSignInRequiredException)
        { throw new DeviceAiException(DeviceAiFailure.Authentication, "Sign in again to use AI. Your writing is unchanged."); }
        catch (DeviceIdentityUnavailableException)
        { throw new DeviceAiException(DeviceAiFailure.Offline, "Sign-in is temporarily unavailable. Your writing is unchanged."); }
        catch (JsonException)
        { throw new DeviceAiException(DeviceAiFailure.Server, "The AI service returned an unreadable response. Your writing is unchanged."); }
    }
}
