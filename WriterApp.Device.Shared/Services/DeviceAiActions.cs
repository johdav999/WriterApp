using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using WriterApp.Application.AI;
using WriterApp.Application.Usage;
using WriterApp.Device.Shared.Components;
using WriterApp.Device.Shared.Storage;

namespace WriterApp.Device.Shared.Services;

public enum DeviceAiAction { Rewrite, Expand, Shorten, Summarize, Custom }
public enum DeviceAiFailure { Authentication, Upgrade, Quota, Safety, Timeout, Offline, Server, Invalid }
public sealed class DeviceAiException(DeviceAiFailure kind, string message) : Exception(message)
{
    public DeviceAiFailure Kind { get; } = kind;
}

public interface IDeviceAiApi
{
    Task<AiUsageStatusDto> GetUsageAsync(CancellationToken ct);
    Task<AiActionExecuteResponseDto> ExecuteAsync(string key, AiActionExecuteRequestDto request, CancellationToken ct);
}

public sealed class DeviceAiApi(HttpClient http) : IDeviceAiApi
{
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
        if (status is HttpStatusCode.BadRequest or HttpStatusCode.RequestEntityTooLarge) return DeviceAiFailure.Invalid;
        return DeviceAiFailure.Server;
    }
}

public sealed record DeviceAiPrepared(string Key, AiActionExecuteRequestDto Request, DeviceAiAction Action,
    Guid LocalPageId, string BaseHtml, int From, int To, string SelectedText, string ApplyMode);
public sealed record DeviceAiProposal(DeviceAiPrepared Prepared, string ProposedText, string? Summary,
    string SourceText, AiUsageStatusDto Usage);

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
        AiEditorSnapshot editor, DeviceAiAction action, string? instruction = null)
    {
        if (doc.ServerDocumentId is null || doc.ServerVersion is null || doc.SyncState != LocalSyncState.Synced)
            throw new DeviceAiException(DeviceAiFailure.Offline, "Sync this document before using AI. Local editing remains available.");
        if (doc.DeletedAtUtc is not null) throw new DeviceAiException(DeviceAiFailure.Invalid, "Restore this document before using AI.");
        bool selected = !string.IsNullOrWhiteSpace(editor.SelectedText) && editor.From < editor.To;
        if (action is DeviceAiAction.Rewrite or DeviceAiAction.Expand or DeviceAiAction.Shorten && !selected)
            throw new DeviceAiException(DeviceAiFailure.Invalid, "Select text in the editor for this action.");
        if (action == DeviceAiAction.Custom && (string.IsNullOrWhiteSpace(instruction) || instruction.Length > 2000))
            throw new DeviceAiException(DeviceAiFailure.Invalid, "Enter a custom instruction of at most 2,000 characters.");
        bool sectionScope = action == DeviceAiAction.Summarize || action == DeviceAiAction.Custom && !selected;
        string key = action switch
        {
            DeviceAiAction.Rewrite => "rewrite.selection",
            DeviceAiAction.Expand => "expand.selection",
            DeviceAiAction.Shorten => "tighten.selection",
            _ => "custom_transform"
        };
        string context = sectionScope ? SectionText(section, page.PageId, editor.PlainText) : editor.PlainText;
        if (string.IsNullOrWhiteSpace(context))
            throw new DeviceAiException(DeviceAiFailure.Invalid, "Add writing before using AI.");
        var parameters = new Dictionary<string, object?>();
        if (action == DeviceAiAction.Rewrite)
        { parameters["tone"] = "Neutral"; parameters["length"] = "Same"; parameters["preserve_terms"] = true; }
        if (key == "custom_transform")
        {
            parameters["scope"] = sectionScope ? "section" : "selection";
            parameters["template"] = action == DeviceAiAction.Summarize
                ? "Summarize this section in two or three concise sentences. Preserve its facts and intent. Context: {context}"
                : instruction!.Trim();
        }
        Guid documentId = doc.ServerDocumentId.Value;
        Guid sectionId = section.ServerSectionId ?? section.SectionId;
        Guid pageId = page.ServerPageId ?? page.PageId;
        var request = new AiActionExecuteRequestDto(documentId, sectionId, pageId,
            selected && !sectionScope ? editor.SelectionStart : null,
            selected && !sectionScope ? editor.SelectionEnd : null,
            selected && !sectionScope ? editor.SelectedText : null,
            context, null, parameters);
        return new(key, request, action, page.PageId, editor.Html, editor.From, editor.To, editor.SelectedText,
            sectionScope ? "append" : "replace");
    }
}

public sealed class DeviceAiService(IDeviceAiApi api, DeviceAccountService account, DeviceConnectivity network)
{
    public async Task<DeviceAiProposal> ProposeAsync(DeviceAiPrepared prepared, CancellationToken ct)
    {
        if (!network.IsOnline) throw new DeviceAiException(DeviceAiFailure.Offline, "Connect to use AI. Local writing remains available.");
        if (!account.IsSignedIn) throw new DeviceAiException(DeviceAiFailure.Authentication, "Sign in to use AI. Local writing remains available.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(90));
        try
        {
            AiUsageStatusDto usage = await api.GetUsageAsync(timeout.Token);
            if (!usage.AiEnabled || !usage.UiEnabled)
                throw new DeviceAiException(DeviceAiFailure.Upgrade, "AI is unavailable for this plan or environment. Local writing remains available.");
            if (usage.QuotaRemaining <= 0)
                throw new DeviceAiException(DeviceAiFailure.Quota, "Your AI quota is exhausted. Local writing remains available.");
            AiActionExecuteResponseDto result = await api.ExecuteAsync(prepared.Key, prepared.Request, timeout.Token);
            if (result.ActionKey != prepared.Key || result.ProposalId == Guid.Empty || string.IsNullOrWhiteSpace(result.ProposedText)
                || result.ProposedText.Length > 100_000)
                throw new DeviceAiException(DeviceAiFailure.Server, "The AI service returned an invalid proposal. Your writing is unchanged.");
            return new(prepared, result.ProposedText, result.ChangesSummary,
                prepared.ApplyMode == "replace" ? prepared.SelectedText : prepared.Request.SurroundingText ?? "", usage);
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
