using System.Net.Http.Json;
using WriterApp.Controllers;
using WriterApp.Device.Shared.Storage;

namespace WriterApp.Device.Shared.Services;

/// <summary>Explicit copy transfers: the existing API has no idempotent upsert or conflict token.</summary>
public sealed class DevicePromptLibrary(HttpClient http, DeviceAccountService account, LocalAiStore store)
{
    private long RequireAccount()
    {
        if (!account.IsSignedIn) throw new DeviceAiException(DeviceAiFailure.Authentication, "Sign in before transferring prompts.");
        return account.Generation;
    }
    private void Check(long generation, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!account.IsSignedIn || account.Generation != generation)
            throw new DeviceAiException(DeviceAiFailure.Authentication, "Account changed. Refresh the prompt library before transferring again.");
    }
    public async Task<IReadOnlyList<PromptPresetDto>> ListAsync(CancellationToken ct)
    {
        long generation = RequireAccount();
        using var response = await http.GetAsync("api/ai/presets", ct);
        response.EnsureSuccessStatusCode();
        var values = await response.Content.ReadFromJsonAsync<PromptPresetDto[]>(cancellationToken: ct) ?? [];
        Check(generation, ct);
        // Only plain custom templates map losslessly to the local schema.
        return values.Where(p => p is not null && p.ProjectId is null && p.Kind == "custom" && p.Parameters is { Count: 0 }
            && !string.IsNullOrWhiteSpace(p.Name) && p.Name.Length <= 100 && p.TemplateText?.Length is > 0 and <= 2000).ToArray();
    }
    public async Task UploadCopyAsync(LocalAiPrompt prompt, CancellationToken ct)
    {
        long generation = RequireAccount();
        Check(generation, ct);
        using var response = await http.PostAsJsonAsync("api/ai/presets",
            new UpsertPromptPresetRequest(null, prompt.Name, "Desktop", "custom", null, prompt.Instruction, new()), ct);
        response.EnsureSuccessStatusCode();
        Check(generation, ct);
    }
    public async Task ImportCopyAsync(Guid id, CancellationToken ct)
    {
        // Re-fetch from the current account; never import a stale cached account's result.
        var prompt = (await ListAsync(ct)).SingleOrDefault(p => p.Id == id)
            ?? throw new InvalidOperationException("This prompt is unavailable for the current account. Refresh the cloud library.");
        await store.SavePromptAsync(prompt.Name, prompt.TemplateText!, ct);
    }
}
