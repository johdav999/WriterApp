using System.Text.Json;
using WriterApp.Application.Documents;

namespace WriterApp.Client.Pages;

public partial class DocumentEditor
{
    private IReadOnlyList<SceneFieldChange> _sceneAiChanges = [];
    private IReadOnlyList<SceneCoachingField> _approvedSceneFields = [];
    private bool _sceneApplying;
    private Task ApprovedSceneFieldsChanged(IReadOnlyList<SceneCoachingField> fields)
    {
        if (!_sceneApplying) _approvedSceneFields = fields.ToArray();
        return Task.CompletedTask;
    }
    private void PrepareSceneApproval(string original, SectionSceneCardProposalDto proposal, string? focus)
    {
        _sceneAiChanges = SceneCardApprovals.Changes(JsonSerializer.Deserialize<SectionSceneCardProposalDto>(original, JsonOptions)!, proposal, focus);
        _approvedSceneFields = focus is null ? [] : _sceneAiChanges.Where(c => c.Error is null).Select(c => c.Field).ToArray();
    }
    private async Task ApplySceneAiProposalAsync()
    {
        if (_sceneApplying || _sceneSaveInFlight || _sceneAiProposal is null || _activeSection is null
            || _sceneAiProposalId is not { } proposalId || _approvedSceneFields.Count == 0) return;
        _sceneApplying = true;
        var proposal = _sceneAiProposal;
        var approved = _approvedSceneFields.ToArray();
        var section = _activeSection.Id;
        string before = BuildSceneCardSnapshotJson();
        try {
            SceneCardApprovals.Require(approved, _sceneAiChanges);
            if (before != _sceneReviewedSnapshot || !_checkedProposals.TryGetValue(proposalId, out var context))
                throw new InvalidOperationException("Scene planning changed or proposal is unchecked. Save and generate again.");
            await RequireCheckedProposal(context);
            if (_sceneAiProposalId != proposalId || _sceneAiProposal != proposal || _activeSection?.Id != section
                || before != BuildSceneCardSnapshotJson())
                throw new InvalidOperationException("Scene review changed. Generate again.");
            _sceneApprovalLease = context.Lease;
            await SaveSceneCardAsync(section, isAutosave: false);
            if(_sceneAiProposalId!=proposalId || _activeSection?.Id!=section)return;
            if (_sceneStatus != "Scene card saved.")
                throw new InvalidOperationException("Scene save was not confirmed. Reload current planning before applying again.");
            await RecordAiSceneCardAppliedAsync(proposalId, before, BuildSceneCardSnapshotJson());
            DiscardSceneAiProposal();
            await LoadAiHistoryAsync();
        } catch (Exception e) { if(_sceneAiProposalId==proposalId)_sceneAiError = e.Message; }
        finally { _sceneApprovalLease = null; _sceneApplying = false; }
    }
}
