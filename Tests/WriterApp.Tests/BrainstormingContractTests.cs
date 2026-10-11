using System.Text;
using System.Text.Json;
using WriterApp.Shared.Brainstorming;
using Xunit;

namespace WriterApp.Tests;

public sealed class BrainstormingContractTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow.AddSeconds(-1);
    internal static BrainstormingSession Fixture()
    {
        var scope = new BrainstormingScope(1, Guid.NewGuid(), Guid.NewGuid(), null, BrainstormingIdentitySpace.Local,
            new('A', 64), new('B', 64), null);
        var stamp = new BrainstormingSourceStamp("local:2", "structure:1", new('C', 64));
        var section = Guid.NewGuid(); var page = Guid.NewGuid();
        var target = new BrainstormingTarget(1, Guid.NewGuid(), scope.DocumentId, BrainstormingTargetKind.InsertInPage,
            stamp, null, null, section, page, null, null, null, 0, 0, "quality-v1");
        var context = new BrainstormingContext(1, Guid.NewGuid(), scope, Now,
            [new(Guid.NewGuid(), scope.DocumentId, BrainstormingSourceKind.Manuscript, "Chapter 1", stamp,
                BrainstormingFreshness.LocalAheadOfServer, false, true, true, false, "Preceding writing", "Maya väntade.", section, page)]);
        var requestId = Guid.NewGuid(); var op = Guid.NewGuid(); var userId = Guid.NewGuid(); var assistantId = Guid.NewGuid();
        var user = new BrainstormingTurn(1, userId, scope, 1, null, op, requestId, context.Id, BrainstormingRole.User,
            BrainstormingTurnState.Accepted, "Draft the opening", Now);
        var assistant = new BrainstormingTurn(1, assistantId, scope, 2, userId, op, requestId, context.Id, BrainstormingRole.Assistant,
            BrainstormingTurnState.Complete, "The opening keeps her suspicious.", Now);
        var request = new BrainstormingRequest(1, requestId, scope, op, userId, context.Id, target.Id, null, 1,
            BrainstormingIntent.Draft, BrainstormingGenerationState.Completed, Now);
        var draft = new BrainstormingDraft(1, Guid.NewGuid(), scope, requestId, assistantId, context.Id, target.Id, null, 1, false,
            user.Text, "Hon väntade vid dörren. 👩🏽‍💻", "Understated dialogue", [], Now);
        var approval = new BrainstormingApproval(1, Guid.NewGuid(), scope, Guid.NewGuid(), draft.Id, draft.Revision,
            BrainstormingValidation.CandidateFingerprint(draft), Now);
        var application = new BrainstormingApplication(1, approval.OperationId, scope, approval.Id,
            BrainstormingApplicationState.Approved, null, null, null, "Reviewed; writing unchanged", Now);
        return new(1, scope, 1, BrainstormingConversationState.Saved, Now, Now, [user, assistant], [context], [target], [request],
            [draft], [], [approval], [application]);
    }
    [Fact]
    public void RoundTripRetainsSeparateStatesUnicodeAndStandaloneIdentity()
    {
        var s = Fixture(); var copy = BrainstormingValidation.ReadSession(JsonSerializer.SerializeToUtf8Bytes(s, BrainstormingValidation.Json));
        Assert.Null(copy.Scope.ProjectId); Assert.Equal(s.Drafts[0], copy.Drafts[0] with { IntroducedFacts = s.Drafts[0].IntroducedFacts });
        Assert.Equal(BrainstormingConversationState.Saved, copy.State);
        Assert.Equal(BrainstormingGenerationState.Completed, copy.Requests[0].State);
        Assert.Equal(BrainstormingApplicationState.Approved, copy.Applications[0].State);
        BrainstormingValidation.RequireApplicable(copy, copy.Applications[0].OperationId, copy.Scope, copy.Targets[0].Source);
    }
    [Theory]
    [InlineData("schema")][InlineData("account")][InlineData("backend")][InlineData("document")][InlineData("session")]
    [InlineData("order")][InlineData("parent")][InlineData("context")][InlineData("operation")][InlineData("null")]
    [InlineData("oversized")][InlineData("state")][InlineData("timestamp")][InlineData("revision")]
    public void RejectsInvalidAggregateBoundaries(string problem)
    {
        var s = Fixture(); var t = s.Turns[1];
        s = problem switch
        {
            "schema" => s with { Version = 2 },
            "account" => s with { Turns = [s.Turns[0], t with { Scope = t.Scope with { AccountKey = new('D', 64) } }] },
            "backend" => s with { Turns = [s.Turns[0], t with { Scope = t.Scope with { BackendKey = new('D', 64) } }] },
            "document" => s with { Targets = [s.Targets[0] with { DocumentId = Guid.NewGuid() }] },
            "session" => s with { Turns = [s.Turns[0], t with { Scope = t.Scope with { SessionId = Guid.NewGuid() } }] },
            "order" => s with { Turns = [s.Turns[0], t with { Sequence = 1 }] },
            "parent" => s with { Turns = [s.Turns[0], t with { ParentTurnId = t.Id }] },
            "context" => s with { Requests = [s.Requests[0] with { ContextId = Guid.NewGuid() }] },
            "operation" => s with { Turns = [s.Turns[0], t with { OperationId = Guid.NewGuid() }] },
            "null" => s with { Drafts = null! },
            "oversized" => s with { Turns = [s.Turns[0] with { Text = new('x', 8001) }, t] },
            "state" => s with { State = (BrainstormingConversationState)100 },
            "timestamp" => s with { CreatedAt = default },
            _ => s with { Requests = [s.Requests[0] with { ExpectedSessionRevision = 2 }] }
        };
        Assert.Throws<InvalidDataException>(() => BrainstormingValidation.Validate(s));
    }
    [Theory]
    [InlineData(BrainstormingGenerationState.Cancelled)][InlineData(BrainstormingGenerationState.Streaming)]
    [InlineData(BrainstormingGenerationState.Interrupted)][InlineData(BrainstormingGenerationState.Unknown)]
    public void OnlyCompletedGenerationCanSupplyApplicableProse(BrainstormingGenerationState state)
    {
        var s = Fixture() with { }; s = s with { Requests = [s.Requests[0] with { State = state }] };
        Assert.Throws<InvalidDataException>(() => BrainstormingValidation.Validate(s));
    }
    [Theory]
    [InlineData("edited")][InlineData("cycle")][InlineData("wrong-target")][InlineData("receipt")][InlineData("duplicate-send")]
    public void RefusesBrokenDraftReviewOrCommitProvenance(string problem)
    {
        var s = Fixture(); var d = s.Drafts[0]; var a = s.Applications[0];
        s = problem switch
        {
            "edited" => s with { Drafts = [d with { PlainText = "Edited after approval", Revision = 2 }] },
            "cycle" => s with { Drafts = [d with { ParentDraftId = d.Id }] },
            "wrong-target" => s with { Drafts = [d with { TargetId = Guid.NewGuid() }] },
            "receipt" => s with { Applications = [a with { State = BrainstormingApplicationState.Committed }] },
            _ => s with { Turns = [..s.Turns, s.Turns[0] with { Id = Guid.NewGuid(), Sequence = 3 }] }
        };
        Assert.Throws<InvalidDataException>(() => BrainstormingValidation.Validate(s));
    }
    [Theory]
    [InlineData(BrainstormingApplicationState.Unknown)][InlineData(BrainstormingApplicationState.Applying)]
    [InlineData(BrainstormingApplicationState.Cancelled)][InlineData(BrainstormingApplicationState.Failed)]
    public void PendingOrUncertainOperationsRequireReconciliation(BrainstormingApplicationState state)
    {
        var s = Fixture(); s = s with { Applications = [s.Applications[0] with { State = state }] };
        BrainstormingValidation.Validate(s);
        Assert.Throws<InvalidDataException>(() => BrainstormingValidation.RequireApplicable(s, s.Applications[0].OperationId, s.Scope, s.Targets[0].Source));
    }
    [Fact]
    public void ConfirmedReceiptIsInspectableButCannotApplyAgain()
    {
        var s = Fixture(); s = s with { Applications = [s.Applications[0] with { State = BrainstormingApplicationState.Committed,
            ReceiptId = Guid.NewGuid(), HistoryOperationId = Guid.NewGuid(), CommittedSource = s.Targets[0].Source }] };
        BrainstormingValidation.Validate(s);
        Assert.Throws<InvalidDataException>(() => BrainstormingValidation.RequireApplicable(s, s.Applications[0].OperationId, s.Scope, s.Targets[0].Source));
    }
    [Fact]
    public void StaleContentOrderWrongAccountAndCancellationRefuseApplication()
    {
        var s = Fixture(); var source = s.Targets[0].Source; var op = s.Applications[0].OperationId;
        foreach (var current in new[] { source with { ContentRevision = "new" }, source with { StructureRevision = "new" }, source with { Fingerprint = new('D', 64) } })
            Assert.Throws<InvalidDataException>(() => BrainstormingValidation.RequireApplicable(s, op, s.Scope, current));
        Assert.Throws<InvalidDataException>(() => BrainstormingValidation.RequireApplicable(s, op, s.Scope with { AccountKey = new('D', 64) }, source));
        Assert.Throws<OperationCanceledException>(() => BrainstormingValidation.RequireApplicable(s, op, s.Scope, source, new(true)));
    }
    [Fact]
    public void NewTextReplacementAndNewSectionHaveDistinctShapes()
    {
        var s = Fixture(); var t = s.Targets[0];
        BrainstormingValidation.Validate(t, s.Scope.DocumentId);
        Assert.Throws<InvalidDataException>(() => BrainstormingValidation.Validate(t with { Kind = BrainstormingTargetKind.ReplaceRange }, s.Scope.DocumentId));
        BrainstormingValidation.Validate(t with { Kind = BrainstormingTargetKind.ReplaceRange, To = 3 }, s.Scope.DocumentId);
        Assert.Throws<InvalidDataException>(() => BrainstormingValidation.Validate(t with { Kind = BrainstormingTargetKind.CreateSection }, s.Scope.DocumentId));
        BrainstormingValidation.Validate(t with { Kind = BrainstormingTargetKind.CreateSection, SectionId = null, PageId = null,
            From = null, To = null, TextMapping = null, BeforeSectionId = t.SectionId }, s.Scope.DocumentId);
    }
    [Theory]
    [InlineData("excluded")][InlineData("unselected")][InlineData("mandatory")][InlineData("budget")][InlineData("summary")]
    public void ContextCannotLeakExcludedSourcesOrSilentlyLoseMandatoryWriting(string problem)
    {
        var s = Fixture(); var c = s.Contexts[0]; var source = c.Sources[0];
        source = problem switch
        {
            "excluded" => source with { Included = false },
            "unselected" => source with { DocumentId = Guid.NewGuid(), Kind = BrainstormingSourceKind.SupportingDocument },
            "mandatory" => source with { Included = false, Text = "" },
            "budget" => source with { Text = new('x', BrainstormingValidation.MaxContextCharacters) },
            _ => source with { IsSummary = true }
        };
        s = s with { Contexts = [c with { Sources = [source] }] };
        Assert.Throws<InvalidDataException>(() => BrainstormingValidation.Validate(s));
    }
    [Fact]
    public void AccountlessOwnershipIsValidButCannotBeImplicitlyMapped()
    {
        var s = Fixture().Scope; var local = s with { AccountKey = null, LocalOwnerId = Guid.NewGuid() };
        BrainstormingValidation.Validate(local);
        var server = s with { Space = BrainstormingIdentitySpace.Server, SessionId = Guid.NewGuid(), DocumentId = Guid.NewGuid() };
        BrainstormingValidation.Validate(new BrainstormingMapping(1, s, server, 1, Now));
        Assert.Throws<InvalidDataException>(() => BrainstormingValidation.Validate(new BrainstormingMapping(1, local, server, 1, Now)));
        Assert.Throws<InvalidDataException>(() => BrainstormingValidation.Validate(server with { AccountKey = null }));
    }
    [Fact]
    public void StrictCodecRejectsOversizeUnknownAndMalformedDataWithoutRewriting()
    {
        Assert.Throws<InvalidDataException>(() => BrainstormingValidation.ReadSession(new byte[BrainstormingValidation.MaxSessionBytes + 1]));
        var json = JsonSerializer.Serialize(Fixture(), BrainstormingValidation.Json);
        Assert.Throws<JsonException>(() => BrainstormingValidation.ReadSession(Encoding.UTF8.GetBytes("{\"future\":1," + json[1..])));
        Assert.Throws<JsonException>(() => BrainstormingValidation.ReadSession("{"u8));
        Assert.Throws<InvalidDataException>(() => BrainstormingValidation.ReadSession("null"u8));
    }
    [Theory]
    [InlineData("duplicate")][InlineData("late")][InlineData("cancelled")][InlineData("identity")][InlineData("partial")]
    public void StreamFragmentsHaveIdentityOrderAndTerminalBoundaries(string problem)
    {
        var s = Fixture(); var request = s.Requests[0] with { State = BrainstormingGenerationState.Streaming };
        var e = new BrainstormingEvent(1, s.Scope, request.Id, request.OperationId, s.Turns[1].Id, 1,
            BrainstormingEventKind.AnswerDelta, "Partial explanation", null, Now);
        BrainstormingValidation.ValidateEvent(e, request, e.TurnId, 0, false);
        if (problem == "cancelled") request = request with { State = BrainstormingGenerationState.Cancelled };
        if (problem == "identity") e = e with { RequestId = Guid.NewGuid() };
        if (problem == "partial") e = e with { ResultId = Guid.NewGuid() };
        Assert.Throws<InvalidDataException>(() => BrainstormingValidation.ValidateEvent(e, request, e.TurnId, problem == "duplicate" ? 1 : 0, problem == "late"));
        Assert.Throws<OperationCanceledException>(() => BrainstormingValidation.ValidateEvent(e, request, e.TurnId, 0, false, new(true)));
    }
    [Fact]
    public void ExplorationAndUnsentTextHaveNoDraftOrApplication()
    {
        var s = Fixture(); s = s with { Requests = [s.Requests[0] with { Intent = BrainstormingIntent.Explore, TargetId = null }],
            Drafts = [], Approvals = [], Applications = [], Turns = [..s.Turns, new(1, Guid.NewGuid(), s.Scope, 3, s.Turns[1].Id,
                Guid.NewGuid(), null, null, BrainstormingRole.User, BrainstormingTurnState.Unsent, "Explore the station idea", Now)] };
        BrainstormingValidation.Validate(s); Assert.Empty(s.Applications);
    }
    [Fact]
    public void ExplicitSupportingSourceAndRecordedOmissionAreValid()
    {
        var s = Fixture(); var c = s.Contexts[0]; var source = c.Sources[0];
        var supporting = source with { Id = Guid.NewGuid(), DocumentId = Guid.NewGuid(), Kind = BrainstormingSourceKind.SupportingDocument,
            ExplicitlyEnabled = true, Mandatory = false, Reason = "Author selected this document", Text = "Planning intent only" };
        var omitted = source with { Id = Guid.NewGuid(), Kind = BrainstormingSourceKind.Character, Mandatory = false,
            Included = false, Text = "", Reason = "Character canon excluded by the author" };
        BrainstormingValidation.Validate(s with { Contexts = [c with { Sources = [source, supporting, omitted] }] });
    }
    [Fact]
    public void ManualRichSnapshotIsInertRetainedAndBoundToExactApproval()
    {
        var s = Fixture(); var draft = s.Drafts[0] with { ManuallyEdited = true, Revision = 2, EditorHtml = "<p><strong>Hon</strong> väntade vid dörren. 👩🏽‍💻</p>" };
        s = s with { Drafts = [draft], Approvals = [s.Approvals[0] with { CandidateRevision = 2,
            ReviewedContentFingerprint = BrainstormingValidation.CandidateFingerprint(draft) }] };
        var saved = BrainstormingValidation.ReadSession(JsonSerializer.SerializeToUtf8Bytes(s, BrainstormingValidation.Json));
        Assert.Equal(draft.EditorHtml, saved.Drafts[0].EditorHtml);
        Assert.Throws<InvalidDataException>(() => BrainstormingValidation.Validate(s with { Drafts = [draft with { EditorHtml = "<p>Changed markup</p>" }] }));
        Assert.Throws<InvalidDataException>(() => BrainstormingValidation.Validate(s with { Drafts = [draft with { ManuallyEdited = false }] }));
    }
    [Fact]
    public void PlanningPreservesNativeStringCanonKeysAndExactFieldApproval()
    {
        var s = Fixture(); var p = new BrainstormingPlanningProposal(1, Guid.NewGuid(), s.Scope, s.Turns[1].Id, s.Contexts[0].Id,
            BrainstormingPlanningKind.CharacterNote, "chr_maya", s.Targets[0].Source, "notes", "Old note", "Her brother remains unaware.", 1, Now);
        var approval = s.Approvals[0] with { CandidateId = p.Id, ReviewedContentFingerprint = BrainstormingValidation.Fingerprint(p.Proposed) };
        s = s with { Planning = [p], Approvals = [approval], Drafts = [] };
        BrainstormingValidation.Validate(s);
        BrainstormingValidation.RequireApplicable(s, approval.OperationId, s.Scope, p.Source);
        Assert.Throws<InvalidDataException>(() => BrainstormingValidation.Validate(s with { Planning = [p with { EntityKey = null }] }));
    }
    [Fact]
    public void BoundsProtectCollectionCountAndAggregateByteSize()
    {
        var s = Fixture(); var pending = s.Turns[0] with { RequestId = null, ContextId = null, State = BrainstormingTurnState.Unsent };
        var turns = Enumerable.Range(1, 501).Select(i => pending with { Id = Guid.NewGuid(), OperationId = Guid.NewGuid(), Sequence = i }).ToArray();
        Assert.Throws<InvalidDataException>(() => BrainstormingValidation.Validate(s with { Turns = turns }));
        turns = Enumerable.Range(1, 300).Select(i => pending with { Id = Guid.NewGuid(), OperationId = Guid.NewGuid(), Sequence = i,
            Text = new('x', 8_000) }).ToArray();
        Assert.Throws<InvalidDataException>(() => BrainstormingValidation.Validate(s with { Turns = turns, Requests = [], Drafts = [], Approvals = [], Applications = [] }));
    }
    [Fact]
    public void InterruptedGenerationRemainsReviewableAndUnknownReservesTheActiveSlot()
    {
        var s = Fixture(); var request = s.Requests[0] with { State = BrainstormingGenerationState.Unknown };
        s = s with { Requests = [request], Turns = [s.Turns[0], s.Turns[1] with { State = BrainstormingTurnState.Incomplete }], Drafts = [], Approvals = [], Applications = [] };
        BrainstormingValidation.Validate(s);
        var next = request with { Id = Guid.NewGuid(), OperationId = Guid.NewGuid(), UserTurnId = Guid.NewGuid(), State = BrainstormingGenerationState.Accepted };
        var turn = s.Turns[0] with { Id = next.UserTurnId, Sequence = 3, OperationId = next.OperationId, RequestId = next.Id };
        Assert.Throws<InvalidDataException>(() => BrainstormingValidation.Validate(s with { Requests = [request, next], Turns = [..s.Turns, turn] }));
        BrainstormingValidation.Validate(s with { Requests = [request with { State = BrainstormingGenerationState.Interrupted }] });
    }
}
