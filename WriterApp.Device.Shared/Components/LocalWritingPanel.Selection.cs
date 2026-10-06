using Microsoft.AspNetCore.Components;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared;

namespace WriterApp.Device.Shared.Components;

public partial class LocalWritingPanel
{
    private string _action = "rewrite", _presetId = "", _presetName = "";
    private IReadOnlyList<LocalAiPrompt> _savedPresets = [];
    private PromptDefinition? _selectedPreset;
    private string? _presetError, _presetMessage;
    private bool _savingPreset;
    private bool ControlsBusy => _busy || ExternalBusy || _savingPreset;

    private string SelectedKey => _action + (_scope == WritingScope.Section ? ".section" : ".selection");
    private string PreviewLabel => _action switch {
        "expand" => "Preview expansion",
        "tighten" => "Preview shortened text",
        "change_tone" => "Preview tone change",
        "show_dont_tell" => "Preview showing revision",
        _ => "Preview rewrite"
    };

    private void ClearPreset() { _presetId = ""; _selectedPreset = null; _presetMessage = null; }
    private void ScopeChanged()
    {
        if (_scope == WritingScope.Section && _action == "rewrite") _action = "expand";
        ClearPreset(); Dismiss();
    }
    private void SelectAction(string action)
    {
        if (ControlsBusy || !Operations.Any(a => a.Key == action)
            || !Available(action + (_scope == WritingScope.Section ? ".section" : ".selection"))) return;
        _action = action; ClearPreset(); Dismiss(); _error = null; _message = null;
    }
    private Task PreviewAction() => !ControlsBusy && _scope != WritingScope.Continuation && Available(SelectedKey)
        ? GenerateRequest(SelectedKey, _selectedPreset) : Task.CompletedTask;

    private async Task LoadPresets()
    {
        if (PresetOnly) return;
        var generation = Account.Generation;
        var scope = PromptLibrary.Scope;
        var documentId = DocumentId;
        try {
            var source = await Documents.LoadAsync(documentId);
            var presets = await History.PresetsAsync(scope);
            if (_disposed || generation != Account.Generation || scope != PromptLibrary.Scope || documentId != DocumentId) return;
            _savedPresets = presets.Where(p => CanSelectPreset(p, source?.ServerProjectId)).ToArray();
            _presetError = null;
            if (!_savedPresets.Any(p => p.Id.ToString() == _presetId)) ClearPreset();
        }
        catch (Exception e) when (e is IOException or System.Text.Json.JsonException or InvalidOperationException) {
            if (generation == Account.Generation && scope == PromptLibrary.Scope && documentId == DocumentId) {
                _savedPresets = []; ClearPreset(); _presetError = "Saved presets could not be loaded: " + e.Message;
            }
        }
    }

    // Templates and presets with free-form tones retain their full definition in Prompt Library.
    private static bool CanSelectPreset(LocalAiPrompt preset, Guid? projectId)
    {
        var definition = LocalAiStore.Definition(preset);
        return definition.Kind == "builtin" && (definition.ProjectId is null || definition.ProjectId == projectId)
            && ReusablePrompts.Unavailable(definition) is null
            && (!definition.Parameters.TryGetValue("tone", out var tone)
                || ReusablePrompts.Primitive(tone) is string value && WritingActions.Tones.Contains(value));
    }
    private void SelectPreset(ChangeEventArgs e)
    {
        if (ControlsBusy) return;
        ClearPreset(); Dismiss();
        if (!Guid.TryParse(e.Value?.ToString(), out var id)) return;
        var saved = _savedPresets.SingleOrDefault(p => p.Id == id);
        if (saved is null) return;
        ApplyPreset(saved);
    }
    private void ApplyPreset(LocalAiPrompt saved)
    {
        ClearPreset(); Dismiss();
        var preset = LocalAiStore.Definition(saved);
        var key = ReusablePrompts.Resolve(preset);
        _scope = preset.Scope;
        _action = key[..key.LastIndexOf('.')];
        _settings = new(
            ReusablePrompts.Primitive(preset.Parameters.GetValueOrDefault("tone")) as string ?? "Neutral",
            ReusablePrompts.Primitive(preset.Parameters.GetValueOrDefault("length")) as string ?? "Same",
            ReusablePrompts.Primitive(preset.Parameters.GetValueOrDefault("preserve_terms")) is not false);
        _selectedPreset = ReusablePrompts.Parse(ReusablePrompts.Serialize(preset));
        _presetId = saved.Id.ToString(); _error = null; _message = null;
    }

    private async Task SavePreset()
    {
        if (_busy || ExternalBusy || _savingPreset || _scope == WritingScope.Continuation) return;
        _savingPreset = true; _presetError = null; _presetMessage = null;
        var generation = Account.Generation; var scope = PromptLibrary.Scope; var documentId = DocumentId;
        try {
            Dictionary<string, object?> parameters = _action switch {
                "rewrite" => new() { ["tone"] = _settings.Tone, ["length"] = _settings.Length, ["preserve_terms"] = _settings.PreserveTerms },
                "change_tone" => new() { ["tone"] = _settings.Tone },
                _ => new()
            };
            var preset = new PromptDefinition(_presetName.Trim(), "Writing", "builtin", SelectedKey, null, parameters, _scope);
            var saved = await History.SavePresetAsync(preset, null, null, scope);
            if (_disposed || generation != Account.Generation || scope != PromptLibrary.Scope || documentId != DocumentId) return;
            await LoadPresets();
            if (_disposed || generation != Account.Generation || scope != PromptLibrary.Scope || documentId != DocumentId) return;
            ApplyPreset(saved);
            _presetName = ""; _presetMessage = "Preset saved locally.";
        }
        catch (Exception e) when (e is IOException or System.Text.Json.JsonException or InvalidOperationException) {
            if (generation == Account.Generation && scope == PromptLibrary.Scope) _presetError = e.Message;
        }
        finally { _savingPreset = false; }
    }
}
