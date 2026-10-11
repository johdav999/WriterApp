using System.Security.Cryptography;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WriterApp.Shared.Brainstorming;

public static class BrainstormingValidation
{
    public const int Version = 1, MaxSessionBytes = 2_000_000, MaxTurns = 500, MaxSources = 100,
        MaxContextCharacters = 100_000, MaxTextCharacters = 100_000, MaxInstructionCharacters = 8_000, MaxSourceCharacters = 500_000;
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { MaxDepth = 32, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    public static string Fingerprint(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    // Authored proposal HTML is retained as inert data; the actual editor must independently validate it before mounting/applying.
    public static string CandidateFingerprint(BrainstormingDraft draft) => Fingerprint(JsonSerializer.Serialize(new { draft.PlainText, draft.EditorHtml }, Json));
    private static void Require([DoesNotReturnIf(false)] bool valid, string message)
    { if (!valid) throw new InvalidDataException("Brainstorming: " + message); }
    private static void Schema(int version) => Require(version == Version, "unsupported schema; update Prosa and the backend.");
    private static void Id(Guid id) => Require(id != Guid.Empty, "missing identity.");
    private static void OptionalId(Guid? id) => Require(id != Guid.Empty, "empty optional identity.");
    private static void Text(string? value, int max, bool required = false) => Require(value is not null && value.Length <= max
        && (!required || !string.IsNullOrWhiteSpace(value)), "missing or oversized text.");
    private static void Hash(string? value) => Require(value is { Length: 64 } && value.All(Uri.IsHexDigit), "invalid fingerprint.");
    private static void EnumValue<T>(T value) where T : struct, Enum => Require(Enum.IsDefined(value), "unsupported state or kind.");
    private static void Time(DateTimeOffset value) => Require(value >= DateTimeOffset.UnixEpoch && value <= DateTimeOffset.UtcNow.AddMinutes(1), "invalid timestamp.");
    private static void Stamp(BrainstormingSourceStamp value)
    { Require(value is not null, "missing source stamp."); Text(value.ContentRevision, 200, true); Text(value.StructureRevision, 200, true); Hash(value.Fingerprint); }
    private static Dictionary<Guid, T> Index<T>(IReadOnlyList<T> values, Func<T, Guid> key, int max)
    {
        Require(values is not null && values.Count <= max && values.All(v => v is not null), "missing or oversized collection.");
        var result = new Dictionary<Guid, T>();
        foreach (var value in values) { var id = key(value); Id(id); Require(result.TryAdd(id, value), "duplicate identity."); }
        return result;
    }
    public static void Validate(BrainstormingScope scope)
    {
        Require(scope is not null, "missing scope."); Schema(scope.Version); Id(scope.SessionId); Id(scope.DocumentId); OptionalId(scope.ProjectId);
        EnumValue(scope.Space); Hash(scope.BackendKey); OptionalId(scope.LocalOwnerId);
        if (scope.AccountKey is { } account) Hash(account);
        Require(scope.AccountKey is null ? scope.Space == BrainstormingIdentitySpace.Local && scope.LocalOwnerId is not null
            : scope.LocalOwnerId is null, "accountless data must retain a separate local owner; server data requires an account.");
    }
    public static void RequireScope(BrainstormingScope actual, BrainstormingScope expected)
    { Validate(actual); Validate(expected); Require(actual == expected, "session, document, account, backend or identity namespace changed."); }
    public static void Validate(BrainstormingMapping mapping)
    {
        Require(mapping is not null, "missing mapping."); Schema(mapping.Version); Validate(mapping.Local); Validate(mapping.Server);
        Require(mapping.Local.Space == BrainstormingIdentitySpace.Local && mapping.Server.Space == BrainstormingIdentitySpace.Server
            && mapping.Local.AccountKey is not null && mapping.Local.AccountKey == mapping.Server.AccountKey
            && mapping.Local.BackendKey == mapping.Server.BackendKey && mapping.Revision > 0, "mapping cannot adopt accountless or foreign data.");
        Time(mapping.UpdatedAt);
    }
    public static void Validate(BrainstormingTarget target, Guid documentId)
    {
        Require(target is not null, "missing target."); Schema(target.Version); Id(target.Id); Id(target.DocumentId);
        Require(target.DocumentId == documentId, "wrong-document target."); EnumValue(target.Kind); Stamp(target.Source);
        foreach (var id in new[] { target.ChapterNodeId, target.SceneNodeId, target.SectionId, target.PageId,
            target.ParentNodeId, target.BeforeSectionId, target.BeforeNodeId }) OptionalId(id);
        if (target.Kind == BrainstormingTargetKind.CreateSection)
            Require(target.SectionId is null && target.PageId is null && target.SceneNodeId is null && target.From is null
                && target.To is null && target.TextMapping is null, "new section must use checked placement, not an existing text range.");
        else
        {
            Require(target.SectionId is not null && target.PageId is not null && target.From >= 0 && target.To >= target.From
                && target.To <= MaxSourceCharacters && target.ParentNodeId is null && target.BeforeSectionId is null
                && target.BeforeNodeId is null, "existing text needs a checked page/range.");
            Text(target.TextMapping, 64, true);
            Require(target.Kind == BrainstormingTargetKind.InsertInPage ? target.From == target.To : target.From < target.To,
                "insertion and replacement ranges are distinct.");
        }
    }
    public static void Validate(BrainstormingClarification value)
    {
        Require(value is not null, "missing clarification."); Schema(value.Version); Id(value.RequestId); Text(value.Question, 2_000, true);
        var candidates = Index(value.Candidates, t => t.Id, 20);
        foreach (var target in candidates.Values) Validate(target, target.DocumentId);
        Require(candidates.Values.Select(t => t.DocumentId).Distinct().Count() <= 1, "clarification mixes manuscripts.");
    }
    public static void Validate(BrainstormingContext context, BrainstormingScope scope)
    {
        Require(context is not null, "missing context."); Schema(context.Version); Id(context.Id); RequireScope(context.Scope, scope); Time(context.CapturedAt);
        var sources = Index(context.Sources, s => s.Id, MaxSources);
        long characters = 0;
        foreach (var s in sources.Values)
        {
            Id(s.DocumentId); EnumValue(s.Kind); EnumValue(s.Freshness); EnumValue(s.Availability); EnumValue(s.Nature); Stamp(s.Source); Text(s.Title, 200); Text(s.Reason, 2_000, true);
            Text(s.Text, MaxContextCharacters); OptionalId(s.SectionId); OptionalId(s.PageId); OptionalId(s.SummaryOfSourceId);
            Require(s.DocumentId == scope.DocumentId || s.Kind == BrainstormingSourceKind.SupportingDocument && s.ExplicitlyEnabled,
                "supporting documents require explicit selection and later server authorization.");
            Require(s.Included || s.Text.Length == 0, "excluded source contains text.");
            Require(!s.Included || s.DocumentId == scope.DocumentId || s.ExplicitlyEnabled, "unselected supporting content.");
            Require(!s.Included || s.Kind == BrainstormingSourceKind.Manuscript || s.ExplicitlyEnabled, "optional sources must be enabled.");
            Require(s.From is null && s.To is null || s.From >= 0 && s.To >= s.From && s.To <= MaxSourceCharacters, "invalid source range.");
            Require(s.PageId is null || s.SectionId is not null, "page context needs its section.");
            if (s.IsSummary) { Require(s.SummaryOfSourceId is not null, "summary needs provenance."); Text(s.SummaryOfRevision, 200, true); }
            else Require(s.SummaryOfSourceId is null && s.SummaryOfRevision is null, "unexpected summary provenance.");
            if (s.Parts is { } parts) {
                Require(parts.Count is > 0 and <= 1000 && parts.All(p => p is not null) && parts.Select(p => p.PageId).Distinct().Count() == parts.Count, "invalid chapter page manifest.");
                int end = 0;
                foreach (var part in parts) { Id(part.SectionId); Id(part.PageId); Text(part.Revision, 200, true); Hash(part.ContentFingerprint);
                    Require(part.From >= end && part.To >= part.From && part.To <= BrainstormingStorySources.MaxRawCharacters, "invalid chapter source range."); end = part.To; }
                Require(s.Kind == BrainstormingSourceKind.Manuscript && s.PageId is null && (!s.Included || s.IsSummary || end == s.Text.Length), "chapter page ranges differ from full supplied text.");
            }
            characters += s.Text.Length + s.Title.Length + s.Reason.Length;
        }
        Require(characters <= MaxContextCharacters, "context budget exceeded; choose a disclosed smaller/summary path.");
        if (context.Policy is { } policy) {
            Schema(policy.Version); Require(policy.CharacterBudget is >= 100 and <= 90_000 && policy.CharactersUsed >= 0 && policy.CharactersUsed <= policy.CharacterBudget, "invalid context budget.");
            Text(policy.Limitation, 2000); Require(policy.Warnings is not null && policy.Warnings.Count <= 100, "too many context warnings.");
            foreach (var warning in policy.Warnings) Text(warning, 8000, true);
            Require(policy.CharactersUsed == sources.Values.Where(s => s.Included).Sum(s => s.Text.Length + s.Title.Length + s.Reason.Length), "context budget receipt differs from supplied text.");
            Require(!policy.Ready || sources.Values.All(s => !s.Mandatory || s.Included), "mandatory context omission blocks generation.");
            Require(sources.Values.All(s => !s.Included || s.Availability == BrainstormingSourceAvailability.Available && s.Freshness != BrainstormingFreshness.Stale), "stale or unavailable source was included.");
        }
    }
    public static void Validate(BrainstormingSession session)
    {
        Require(session is not null, "missing session."); Schema(session.Version); Validate(session.Scope); EnumValue(session.State);
        Require(session.Revision > 0, "invalid session revision."); Time(session.CreatedAt); Time(session.UpdatedAt);
        OptionalId(session.BranchedFromSessionId); Require(session.BranchedFromSessionId != session.Scope.SessionId, "session cannot branch from itself.");
        Require(session.EnabledSources is null || session.EnabledSources.Count <= MaxSources, "too many enabled sources.");
        foreach (var selection in session.EnabledSources ?? [])
        { Require(selection is not null, "missing enabled source."); Id(selection.DocumentId); EnumValue(selection.Kind); if (selection.EntityKey is not null) Text(selection.EntityKey, 200, true); }
        Require((session.EnabledSources ?? []).Distinct().Count() == (session.EnabledSources?.Count ?? 0), "duplicate enabled source.");
        Require(session.UpdatedAt >= session.CreatedAt, "session timestamp order.");
        var turns = Index(session.Turns, t => t.Id, MaxTurns);
        var contexts = Index(session.Contexts, c => c.Id, MaxTurns);
        var targets = Index(session.Targets, t => t.Id, MaxTurns);
        var requests = Index(session.Requests, r => r.Id, MaxTurns);
        var drafts = Index(session.Drafts, d => d.Id, MaxTurns);
        var planning = Index(session.Planning, p => p.Id, MaxTurns);
        var approvals = Index(session.Approvals, a => a.Id, MaxTurns);
        var applications = Index(session.Applications, a => a.OperationId, MaxTurns);
        foreach (var c in contexts.Values) Validate(c, session.Scope);
        foreach (var t in targets.Values)
        {
            Validate(t, session.Scope.DocumentId);
            Require(session.Scope.ProjectId is not null || t.ChapterNodeId is null && t.SceneNodeId is null
                && t.ParentNodeId is null && t.BeforeNodeId is null, "standalone manuscript cannot invent project outline nodes.");
        }
        long sequence = 0;
        foreach (var t in session.Turns)
        {
            Schema(t.Version); RequireScope(t.Scope, session.Scope); Id(t.OperationId); Time(t.CreatedAt); EnumValue(t.Role); EnumValue(t.State);
            Text(t.Text, t.Role == BrainstormingRole.User ? MaxInstructionCharacters : MaxTextCharacters);
            Require(t.Sequence == ++sequence, "turns must be contiguous in accepted order.");
            OptionalId(t.ParentTurnId); OptionalId(t.RequestId); OptionalId(t.ContextId);
            Require(t.ParentTurnId is null || turns.TryGetValue(t.ParentTurnId.Value, out var parent) && parent.Sequence < t.Sequence, "invalid turn ancestry.");
            Require(t.ContextId is null || contexts.ContainsKey(t.ContextId.Value), "missing turn context.");
            Require(t.RequestId is null || requests.TryGetValue(t.RequestId.Value, out var request) && request.ContextId == t.ContextId
                && request.OperationId == t.OperationId, "wrong turn request/context/operation.");
            Require(t.Role != BrainstormingRole.Assistant || t.RequestId is not null && t.ParentTurnId is not null
                && turns[t.ParentTurnId.Value].Role == BrainstormingRole.User && requests[t.RequestId.Value].UserTurnId == t.ParentTurnId,
                "assistant must belong to its user request.");
            Require(t.Role != BrainstormingRole.Assistant || t.State != BrainstormingTurnState.Complete
                || requests[t.RequestId!.Value].State == BrainstormingGenerationState.Completed, "complete assistant turn needs a validated terminal result.");
            Require(t.Role != BrainstormingRole.User || t.State != BrainstormingTurnState.Unsent || t.RequestId is null,
                "unsent message cannot be a dispatched request.");
        }
        Require(session.Turns.Where(t => t.Role == BrainstormingRole.User).Select(t => t.OperationId).Distinct().Count()
            == session.Turns.Count(t => t.Role == BrainstormingRole.User), "duplicate user send operation.");
        Require(requests.Values.Select(r => r.OperationId).Distinct().Count() == requests.Count, "duplicate generation operation.");
        Require(session.Turns.Where(t => t.Role == BrainstormingRole.Assistant).Select(t => t.RequestId).Distinct().Count()
            == session.Turns.Count(t => t.Role == BrainstormingRole.Assistant), "duplicate assistant turn for one request.");
        foreach (var r in requests.Values)
        {
            Schema(r.Version); RequireScope(r.Scope, session.Scope); Id(r.OperationId); Time(r.CreatedAt); EnumValue(r.Intent); EnumValue(r.State);
            Require(r.ExpectedSessionRevision > 0 && r.ExpectedSessionRevision <= session.Revision, "invalid request revision.");
            Require(turns.TryGetValue(r.UserTurnId, out var user) && user.Role == BrainstormingRole.User
                && user.OperationId == r.OperationId && user.RequestId == r.Id && user.ContextId == r.ContextId
                && user.State != BrainstormingTurnState.Unsent && contexts.ContainsKey(r.ContextId), "request is detached from its accepted user/context.");
            OptionalId(r.TargetId); OptionalId(r.ParentDraftId);
            Require(r.TargetId is null || targets.ContainsKey(r.TargetId.Value), "missing request target.");
            Require(r.Intent is not (BrainstormingIntent.Draft or BrainstormingIntent.Revise) || r.TargetId is not null, "drafting needs a resolved target.");
            Require(r.Intent != BrainstormingIntent.Revise || r.ParentDraftId is not null, "revision needs a parent draft.");
            Require(r.State != BrainstormingGenerationState.Completed || session.Turns.Any(t => t.RequestId == r.Id
                && t.Role == BrainstormingRole.Assistant && t.State == BrainstormingTurnState.Complete), "completed request has no complete assistant result.");
            Require(r.State is BrainstormingGenerationState.Idle or BrainstormingGenerationState.Prepared or BrainstormingGenerationState.Cancelled
                or BrainstormingGenerationState.Failed || contexts[r.ContextId].Sources.All(s => !s.Mandatory || s.Included),
                "mandatory context omitted; clarify the smaller/summary path before generation.");
            Require(r.ParentDraftId is null || drafts.TryGetValue(r.ParentDraftId.Value, out var parent) && parent.TargetId == r.TargetId
                && parent.CreatedAt <= r.CreatedAt && parent.RequestId != r.Id, "invalid revision source.");
        }
        Require(requests.Values.Count(r => r.State is BrainstormingGenerationState.Accepted or BrainstormingGenerationState.Streaming
            or BrainstormingGenerationState.Unknown) <= 1, "settle the active/unknown generation before another send.");
        foreach (var d in drafts.Values)
        {
            Schema(d.Version); RequireScope(d.Scope, session.Scope); Time(d.CreatedAt); Require(d.Revision > 0, "invalid draft revision.");
            Text(d.Instruction, MaxInstructionCharacters, true); Text(d.PlainText, MaxTextCharacters, true); Text(d.Constraints, MaxInstructionCharacters);
            if (d.EditorHtml is not null) {
                Text(d.EditorHtml, MaxTextCharacters, true);
                // Generated rich content is only the exact reviewed prefix plus escaped new prose.
                // The provider response has no HTML field.
                if(!d.ManuallyEdited) {
                    BrainstormingDraftContent.RequireHtml(d.EditorHtml);
                    Require(BrainstormingDraftContent.PlainText(d.EditorHtml)==d.PlainText,"draft text and editor snapshot disagree.");
                    Require(d.ParentDraftId is { } previousId && drafts.ContainsKey(previousId),"generated rich content needs its reviewed parent.");
                    var previous=drafts[d.ParentDraftId!.Value];
                    Require(d.PlainText.StartsWith(previous.PlainText,StringComparison.Ordinal),"continuation changed its reviewed prefix.");
                    var suffix=d.PlainText[previous.PlainText.Length..].TrimStart('\n');
                    Require(d.EditorHtml==(previous.EditorHtml??BrainstormingDraftContent.Html(previous.PlainText))+BrainstormingDraftContent.Html(suffix),"generated rich content is not an escaped continuation.");
                }
            }
            Require(d.Contradictions is null || d.Contradictions.Count<=30,"oversized draft warnings.");
            foreach(var warning in d.Contradictions ?? [])Text(warning,2000,true);
            Require(d.IntroducedFacts is not null && d.IntroducedFacts.Count <= 100, "oversized facts.");
            foreach (var fact in d.IntroducedFacts) Text(fact, 2_000, true);
            EnumValue(d.Origin);
            if(d.Origin==BrainstormingDraftOrigin.AuthorDictated) {
                Require(d.ManuallyEdited && d.RequestId==Guid.Empty && d.TurnId==Guid.Empty && d.DictationOperationId is { } dictated && dictated!=Guid.Empty
                    && contexts.ContainsKey(d.ContextId) && targets.ContainsKey(d.TargetId),"dictated proposal needs explicit owned provenance and destination; it is not an AI result.");
            } else {
            Require(d.DictationOperationId is null,"AI draft cannot claim dictated provenance.");
            Require(requests.TryGetValue(d.RequestId, out var request) && request.State == BrainstormingGenerationState.Completed
                && request.Intent is BrainstormingIntent.Draft or BrainstormingIntent.Revise && request.ContextId == d.ContextId
                && request.TargetId == d.TargetId, "incomplete/cancelled/wrong generation cannot become a draft.");
            Require(turns.TryGetValue(d.TurnId, out var turn) && turn.Role == BrainstormingRole.Assistant
                && turn.RequestId == d.RequestId && turn.State == BrainstormingTurnState.Complete, "draft lacks complete assistant turn.");
            }
            OptionalId(d.ParentDraftId);
            OptionalId(d.BranchId);
            Require(d.BranchName is null || d.BranchId is not null && !string.IsNullOrWhiteSpace(d.BranchName) && d.BranchName.Length<=60 && !d.BranchName.Any(char.IsControl), "invalid alternative name.");
            Require(d.ParentDraftId is null || drafts.TryGetValue(d.ParentDraftId.Value, out var parent) && parent.Id != d.Id
                && parent.TargetId == d.TargetId && parent.CreatedAt <= d.CreatedAt, "invalid draft ancestry.");
            Require(d.ManuallyEdited || d.ParentDraftId == requests[d.RequestId].ParentDraftId, "generated revision lost its requested parent.");
            var visited = new HashSet<Guid> { d.Id }; var ancestor = d.ParentDraftId;
            while (ancestor is { } id) { Require(visited.Add(id) && drafts.ContainsKey(id), "cyclic draft ancestry."); ancestor = drafts[id].ParentDraftId; }
        }
        foreach (var p in planning.Values)
        {
            Schema(p.Version); RequireScope(p.Scope, session.Scope); EnumValue(p.Kind); Stamp(p.Source); Time(p.CreatedAt);
            Require(p.Revision > 0 && contexts.ContainsKey(p.ContextId) && turns.TryGetValue(p.TurnId, out var turn)
                && turn.State == BrainstormingTurnState.Complete && turn.ContextId == p.ContextId, "planning needs complete provenance.");
            if (p.EntityKey is not null) Text(p.EntityKey, 200, true);
            Require(!p.AuthoredDocumentNotes || p.Kind==BrainstormingPlanningKind.CharacterNote && p.EntityKey==session.Scope.DocumentId.ToString(), "authored character notes need the owned manuscript notes target.");
            Require(p.Kind == BrainstormingPlanningKind.SceneIdea || p.EntityKey is not null, "existing planning target needs identity.");
            Text(p.Field, 200, true); Text(p.Before, MaxTextCharacters); Text(p.Proposed, MaxTextCharacters, true);
        }
        foreach (var a in approvals.Values)
        {
            Schema(a.Version); RequireScope(a.Scope, session.Scope); Id(a.OperationId); Time(a.ReviewedAt); Hash(a.ReviewedContentFingerprint);
            string? content = null; long revision = 0;
            if (drafts.TryGetValue(a.CandidateId, out var d)) { content = CandidateFingerprint(d); revision = d.Revision; }
            if (planning.TryGetValue(a.CandidateId, out var p)) { Require(content is null, "ambiguous candidate identity."); content = Fingerprint(p.Proposed); revision = p.Revision; }
            Require(content is not null && a.CandidateRevision == revision && a.ReviewedContentFingerprint == content, "reviewed candidate changed.");
        }
        Require(approvals.Values.Select(a => a.OperationId).Distinct().Count() == approvals.Count, "duplicate approval operation.");
        foreach (var a in applications.Values)
        {
            Schema(a.Version); RequireScope(a.Scope, session.Scope); EnumValue(a.State); Time(a.UpdatedAt); Text(a.Message, 2_000);
            Require(approvals.TryGetValue(a.ApprovalId, out var approval) && approval.OperationId == a.OperationId, "application lacks exact approval.");
            OptionalId(a.ReceiptId); OptionalId(a.HistoryOperationId);
            if (a.State is BrainstormingApplicationState.Committed or BrainstormingApplicationState.Undone)
            { Require(a.ReceiptId is not null && a.HistoryOperationId is not null && a.CommittedSource is not null, "no durable commit receipt/history."); Stamp(a.CommittedSource!); }
            else Require(a.ReceiptId is null && a.HistoryOperationId is null && a.CommittedSource is null, "unconfirmed application cannot claim a commit.");
        }
        Require(JsonSerializer.SerializeToUtf8Bytes(session, Json).Length <= MaxSessionBytes, "session exceeds 2 MB; archive/page explicitly.");
    }
    public static BrainstormingSession ReadSession(ReadOnlySpan<byte> utf8)
    {
        Require(utf8.Length <= MaxSessionBytes, "session exceeds byte limit.");
        var value = JsonSerializer.Deserialize<BrainstormingSession>(utf8, Json);
        Require(value is not null, "empty session."); Validate(value!); return value!;
    }
    public static void RequireCurrent(BrainstormingSourceStamp captured, BrainstormingSourceStamp current, CancellationToken ct = default)
    { ct.ThrowIfCancellationRequested(); Stamp(captured); Stamp(current); Require(captured == current, "source or target order changed; refresh and review."); }
    public static void RequireApplicable(BrainstormingSession session, Guid operationId, BrainstormingScope currentScope,
        BrainstormingSourceStamp currentSource, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested(); Validate(session); RequireScope(session.Scope, currentScope);
        Require(session.State != BrainstormingConversationState.Deleted && session.State != BrainstormingConversationState.Conflict, "session needs recovery.");
        var application = session.Applications.SingleOrDefault(a => a.OperationId == operationId);
        Require(application is not null && application.State == BrainstormingApplicationState.Approved, "operation is not ready; reconcile pending/unknown/committed results.");
        var approval = session.Approvals.Single(a => a.Id == application!.ApprovalId);
        var draft = session.Drafts.SingleOrDefault(d => d.Id == approval.CandidateId);
        var source = draft is not null ? session.Targets.Single(t => t.Id == draft.TargetId).Source
            : session.Planning.Single(p => p.Id == approval.CandidateId).Source;
        RequireCurrent(source, currentSource, ct);
    }
    public static void ValidateEvent(BrainstormingEvent value, BrainstormingRequest request, Guid assistantTurnId,
        long previousSequence, bool alreadyTerminal, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested(); Require(value is not null && request is not null, "missing stream identity."); Schema(value.Version); Schema(request.Version);
        Id(request.Id); Id(request.OperationId); Id(request.UserTurnId); Id(request.ContextId); EnumValue(request.State); EnumValue(request.Intent);
        RequireScope(value.Scope, request.Scope); EnumValue(value.Kind); Time(value.CreatedAt); Text(value.Text, MaxInstructionCharacters);
        Require(!alreadyTerminal && request.State is not (BrainstormingGenerationState.Cancelled or BrainstormingGenerationState.Failed
            or BrainstormingGenerationState.Completed or BrainstormingGenerationState.Interrupted), "late terminal/cancelled event.");
        Require(value.RequestId == request.Id && value.OperationId == request.OperationId && value.TurnId == assistantTurnId
            && assistantTurnId != Guid.Empty && value.Sequence == previousSequence + 1 && value.Sequence > 0, "out-of-order/duplicate/wrong stream event.");
        OptionalId(value.ResultId);
        Require(value.Kind == BrainstormingEventKind.Completed ? value.ResultId is not null : value.ResultId is null, "only a terminal completion has a validated result identity.");
    }
}
