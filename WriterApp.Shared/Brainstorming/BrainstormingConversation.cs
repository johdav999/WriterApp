using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WriterApp.Shared.Brainstorming;

public sealed record BrainstormingServerContextRequest(int Version, BrainstormingScope Scope, BrainstormingTargetQuery Query,
    IReadOnlyList<BrainstormingContextChoice> Choices, IReadOnlyList<Guid> SupportingDocuments, int Budget);
public sealed record BrainstormingServerContext(BrainstormingPreparation Preparation, IReadOnlyList<BrainstormingStorySnapshot> Documents);

public sealed record BrainstormingSend(int Version, BrainstormingScope Scope, Guid OperationId, Guid RequestId,
    Guid UserTurnId, Guid AssistantTurnId, long ExpectedRevision, Guid ContextId, Guid TargetId, Guid? ParentDraftId, string Instruction,
    BrainstormingDraftSelection? Selection = null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] Guid? BranchId = null);
public sealed record BrainstormingProse(string Text, string Constraints, IReadOnlyList<string> IntroducedFacts,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingDefault)] bool Continuation = false,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] string? RevisionInstruction = null);
public sealed record BrainstormingPlanningSuggestion(Guid SourceId, BrainstormingPlanningKind Kind, string? EntityKey, string Field, string Before, string Proposed);
// Provider IDs, operations, HTML and tools are deliberately absent. Provenance is assigned by the server.
public sealed record BrainstormingAnswer(string Answer, int Version, BrainstormingIntent Intent, BrainstormingProse? Draft,
    IReadOnlyList<BrainstormingPlanningSuggestion> Planning, IReadOnlyList<string> Contradictions);
public enum BrainstormingUsageState { NotDispatched, Pending, Settling, Settled, Unknown }
public sealed record BrainstormingGenerationUsage(BrainstormingUsageState State, string Provider, string Model, int InputTokens, int OutputTokens);
public sealed record BrainstormingConversationEvent(int Version, BrainstormingScope Scope, Guid OperationId, Guid RequestId,
    Guid TurnId, long Sequence, BrainstormingEventKind Kind, string Text, BrainstormingAnswer? Output,
    BrainstormingGenerationUsage Usage, BrainstormingGenerationState State, DateTimeOffset CreatedAt);
public sealed record BrainstormingGeneration(int Version, BrainstormingSend Send, BrainstormingGenerationState State,
    IReadOnlyList<BrainstormingConversationEvent> Events, string InspectionText, BrainstormingGenerationUsage Usage, string Message, DateTimeOffset UpdatedAt);
public sealed class BrainstormingSendRejectedException(string message,string code,Guid operationId,System.Net.HttpStatusCode status)
    : HttpRequestException(message,null,status)
{
    public string Code {get;}=code;
    public Guid OperationId {get;}=operationId;
}
public interface IBrainstormingConversation
{
    IAsyncEnumerable<BrainstormingConversationEvent> SendAsync(BrainstormingSend send, CancellationToken ct = default);
    Task<BrainstormingGeneration?> InspectAsync(BrainstormingScope scope, Guid operationId, CancellationToken ct = default);
    Task StopAsync(BrainstormingScope scope, Guid operationId, CancellationToken ct = default);
}
public static class BrainstormingConversationContract
{
    public static readonly JsonSerializerOptions ResponseJson = new(BrainstormingValidation.Json) { Converters = {new JsonStringEnumConverter()} };
    public const int MaxOutput = 150_000, MaxEvents = 512, MaxDialogue = 24_000;
    public static string UnfinishedTurnMessage(BrainstormingTurnState state) => state switch {
        BrainstormingTurnState.Cancelled => "Generation stopped. Your working draft is unchanged.",
        BrainstormingTurnState.Failed => "Prosa could not complete this response. Your working draft is unchanged. You can retry as a new request.",
        _ => "This response is incomplete. Your working draft is unchanged. Inspect the request details before trying again."
    };
    public static void Validate(BrainstormingSend send) {
        BrainstormingValidation.Validate(send.Scope);
        if(send.BranchId==Guid.Empty)throw new InvalidDataException("Invalid working draft identity.");
        if (send.Version != 1 || send.Scope.Space != BrainstormingIdentitySpace.Server || send.OperationId == Guid.Empty || send.RequestId == Guid.Empty
            || send.UserTurnId == Guid.Empty || send.AssistantTurnId == Guid.Empty || send.UserTurnId == send.AssistantTurnId || send.ExpectedRevision < 1
            || send.ContextId == Guid.Empty || send.TargetId == Guid.Empty || send.ParentDraftId == Guid.Empty || string.IsNullOrWhiteSpace(send.Instruction) || send.Instruction.Length > 8000)
            throw new InvalidDataException("Invalid conversation request.");
        if(send.Selection is { } selection && (send.ParentDraftId!=selection.CandidateId || selection.Version!=1 || selection.CandidateFingerprint is not {Length:64}
            || selection.TextMapping!=BrainstormingDrafts.TextMapping || selection.From<0 || selection.To<=selection.From || selection.Quote is null || selection.Quote.Length>8000))
            throw new InvalidDataException("Invalid proposal selection.");
    }
    public static BrainstormingAnswer ReadAnswer(string text, BrainstormingSend send, BrainstormingContext context) {
        if (text.Length > MaxOutput) throw new InvalidDataException("Conversation output exceeds its limit.");
        using(var json=JsonDocument.Parse(text,new JsonDocumentOptions{MaxDepth=32})) {
            void Unique(JsonElement element) {
                if(element.ValueKind==JsonValueKind.Object) {var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);foreach(var p in element.EnumerateObject()) {if(!names.Add(p.Name))throw new InvalidDataException("Duplicate response property.");Unique(p.Value);}}
                else if(element.ValueKind==JsonValueKind.Array)foreach(var item in element.EnumerateArray())Unique(item);
            }
            Unique(json.RootElement);
        }
        var answer = JsonSerializer.Deserialize<BrainstormingAnswer>(text, ResponseJson) ?? throw new InvalidDataException("Missing conversation output.");
        // Normalize only decoded provider prose, before candidate fingerprints or rich
        // snapshots are created. InspectionText retains the exact provider response.
        answer = answer with {
            Answer = RepairQuotationMarks(answer.Answer),
            Draft = answer.Draft is { } draft ? draft with {
                Text = RepairQuotationMarks(draft.Text),
                Constraints = RepairQuotationMarks(draft.Constraints),
                IntroducedFacts = draft.IntroducedFacts?.Select(RepairQuotationMarks).ToArray()!
            } : null,
            Contradictions = answer.Contradictions?.Select(RepairQuotationMarks).ToArray()!,
            // Source identities, before-values and revision authority remain exact.
            Planning = answer.Planning?.Select(p => p is null ? p! : p with {
                Proposed = RepairQuotationMarks(p.Proposed)
            }).ToArray()!
        };
        // Tabs and line breaks are prose whitespace; other C0/C1 controls are not.
        void Bound(string? s, int max, bool required = false) { if (s is null || s.Length > max || required && string.IsNullOrWhiteSpace(s) || s.Any(c => char.IsControl(c) && c is not ('\t' or '\r' or '\n'))) throw new InvalidDataException("Invalid conversation output text."); }
        Bound(answer.Answer, 50_000, true);
        if (answer.Version != 1 || !Enum.IsDefined(answer.Intent) || answer.Planning is null || answer.Planning.Count > 20
            || answer.Contradictions is null || answer.Contradictions.Count > 30) throw new InvalidDataException("Invalid conversation response contract.");
        foreach (var conflict in answer.Contradictions) Bound(conflict, 2000, true);
        if ((answer.Intent is BrainstormingIntent.Draft or BrainstormingIntent.Revise) != (answer.Draft is not null)
            || answer.Intent == BrainstormingIntent.Revise && send.ParentDraftId is null
            || answer.Intent is BrainstormingIntent.Explore or BrainstormingIntent.Clarify && answer.Planning.Count != 0
            || answer.Intent == BrainstormingIntent.ProposePlanning && (answer.Planning.Count == 0 || answer.Draft is not null)) throw new InvalidDataException("Response intent and proposals disagree.");
        if (answer.Draft is { } d) { Bound(d.Text,100_000,true); Bound(d.Constraints,8000); if(d.IntroducedFacts is null || d.IntroducedFacts.Count>100)throw new InvalidDataException("Invalid introduced facts."); foreach(var fact in d.IntroducedFacts)Bound(fact,2000,true); }
        if(answer.Draft is { } prose) {
            if(prose.RevisionInstruction is { } revision) {
                Bound(revision,8000,true);
                if(send.ParentDraftId is null || prose.Continuation || !send.Instruction.Contains(revision,StringComparison.Ordinal))
                    throw new InvalidDataException("Replacing a working draft requires a rewrite instruction quoted from the current author message.");
            } else if(send.ParentDraftId is not null) {
                // Supplying a reviewed draft is context, not permission to replace it.
                // Default to adding the next story beat even if the provider omits or
                // mislabels continuation. An explicit revision must carry its authority.
                if(send.Selection is not null)
                    throw new InvalidDataException("A passage revision requires a rewrite instruction from the current author message.");
                answer=answer with {Draft=prose with {Continuation=true}};
            } else {
                // Manuscript context may describe earlier prose, but only a checked
                // parent identifies an editable working draft to append to. A model
                // continuation flag cannot turn an initial proposal into an append.
                answer=answer with {Draft=prose with {Continuation=false}};
            }
        }
        if(answer.Draft?.Continuation==true && (send.ParentDraftId is null || send.Selection is not null))throw new InvalidDataException("A continuation needs a complete reviewed working draft, without a passage selection.");
        if(answer.Draft is not null && context.Policy?.Warnings.Any(w=>w.StartsWith("Conflicting story assertion",StringComparison.Ordinal))==true && answer.Contradictions.Count==0)
            throw new InvalidDataException("Known story contradictions must be disclosed before a prose proposal.");
        foreach(var p in answer.Planning) {
            if(p is null || !Enum.IsDefined(p.Kind) || !context.Sources.Any(s=>s.Id==p.SourceId && s.Included && s.DocumentId==context.Scope.DocumentId && s.Nature!=BrainstormingSourceNature.ManuscriptFact)) throw new InvalidDataException("Planning suggestion lacks an enabled owned source.");
            Bound(p.Field,200,true); Bound(p.Before,8000); Bound(p.Proposed,8000,true); if(p.EntityKey is not null)Bound(p.EntityKey,200,true);
            if(p.Kind!=BrainstormingPlanningKind.SceneIdea && p.EntityKey is null)throw new InvalidDataException("Planning proposal needs an explicit entity identity for later review.");
        }
        return answer;
    }
    // The reported provider output used file/group separators in place of curly
    // double quotes. Do not drop them: that would silently remove dialogue punctuation.
    private static string RepairQuotationMarks(string text) => text?.Replace('\u001c', '\u201c').Replace('\u001d', '\u201d')!;
    // Preview only fully decoded root string properties. Partial JSON never becomes a draft.
    public static string? PreviewAnswer(string wire) {
        var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(wire), false, default); string? property = null;
        try { while(reader.Read()) { if(reader.TokenType==JsonTokenType.PropertyName && reader.CurrentDepth==1)property=reader.GetString();
            else if(reader.TokenType==JsonTokenType.String && reader.CurrentDepth==1 && property=="answer") {
                var preview=RepairQuotationMarks(reader.GetString()!);
                return preview.Any(c => char.IsControl(c) && c is not ('\t' or '\r' or '\n')) ? null : preview;
            } } }
        catch(JsonException) { }
        return null;
    }
    public static void ValidateEvent(BrainstormingConversationEvent e, BrainstormingSend send, long after) {
        if(e.Version!=1 || e.Scope!=send.Scope || e.OperationId!=send.OperationId || e.RequestId!=send.RequestId || e.TurnId!=send.AssistantTurnId
            || e.Sequence!=after+1 || e.Sequence>MaxEvents || !Enum.IsDefined(e.Kind) || !Enum.IsDefined(e.State) || e.Text is null || e.Text.Length>50_000
            || e.Usage is null || !Enum.IsDefined(e.Usage.State) || e.Usage.InputTokens<0 || e.Usage.OutputTokens<0 || e.CreatedAt==default
            || (e.Kind==BrainstormingEventKind.Completed)!=(e.Output is not null) || e.Kind==BrainstormingEventKind.Completed && (e.State!=BrainstormingGenerationState.Completed || e.Usage.State!=BrainstormingUsageState.Settled))
            throw new InvalidDataException("Invalid, reordered or mis-scoped conversation event.");
    }
}
