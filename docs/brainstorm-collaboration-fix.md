# Brainstorm context in shared manuscripts

Verified locally on 2026-10-20.

The reported `Owned story context is unavailable` error came from the legacy-only
`BrainstormingStorySourceReader`. Its checked-source service rejected collaborative
manuscripts, and its page reader used legacy HTML. Conversation authorization and
the tracked-write guard also prevented an editor from saving their private Brainstorm
session against a shared manuscript.

The reader now accepts collaboration access, captures a consistent committed
projection, excludes deleted shared pages/sections, and uses shared prose with the
collaboration revision. Source, session, recovery, and generation controllers carry
the same access service. Owners and editors can prepare context and save their own
conversations; conversations remain private to the actor. Viewers and revoked members
cannot prepare AI context. Private sibling documents remain inaccessible. Conversation
records are excluded from the legacy manuscript write guard; manuscript write fences
remain enforced. Projection-pending reads have a bounded retry, and access/context
errors explain the actual recovery action.

Regression coverage in `CollaborationBrainstormingTests.cs` exercises production HTTP
controllers with migrated SQLite, actual collaboration projections, deterministic AI
responses, private session persistence/recovery, changed-source rejection before
provider dispatch, deleted pages, viewer access, and revocation. Transport tests cover
projection-only retries. The database-readiness test now selects the migration before
the first Brainstorm migration rather than assuming Brainstorm migrations are last.

The provider-signed-in owner (Edge) and editor (Chrome) both opened manuscript
`2921410a`, automatically reached **Context ready**, and retained the original
20-word shared manuscript. The editor's context disclosure showed the exact current
shared prose and a `collaboration:` source revision. Entering a temporary message
enabled Send; the temporary text was cleared without sending. Screenshots are in
`artifacts/brainstorm-collaboration/`. No paid provider call was made.

Validation command and result log:

```powershell
dotnet test Tests/WriterApp.Tests/WriterApp.Tests.csproj --no-restore -p:BaseOutputPath=artifacts/brainstorm-build/bin/ --filter "FullyQualifiedName~Brainstorm|FullyQualifiedName~CollaborationConversionTests" --logger "console;verbosity=minimal"
```

Result: **513 passed, 1 skipped, 0 failed** (514 total, 2 minutes 15 seconds).
The skipped test is the opt-in hundred-page load benchmark.
See `artifacts/brainstorm-collaboration-tests.log`. The isolated output avoids the
separate development host holding the standard build open. The collaboration site
was restarted with `artifacts/start-brainstorm-collaboration.ps1 -SkipBuild`, retaining
its database and sign-in cookies. The standard stop script still recognizes its PID.

This initial repair covered context, private conversation persistence, and generation checks.
Keep and planning saves were still fenced at this stage; the subsequent repair and
end-to-end acceptance are documented in [Brainstorm shared saves acceptance](brainstorm-shared-saves-uat.md).
This initial evidence did not establish live-provider generation,
shared draft application, native-device acceptance, or hosted deployment. The local
test profile at this stage still displayed Standard / AI not included. The send-time
follow-up below corrects that profile defect; production subscription checks remain.

## Send rejection follow-up

The reported `Owned server inspection confirmed this operation was never accepted`
message hid the original pre-acceptance error. `LocalCollaborationEntitlements` copied
the Standard plan but omitted `AiMonthlyTokenBudget`, so the account's local license
view had zero budget. `EntitlementService` consequently reported `ai.enabled=false`,
and the real AI usage policy refused the send. Capability discovery still advertised
conversation generation. The client retained the send and correctly inspected it,
but replaced the original rejection reason with the generic absence message.

The local license view now supplies the normal Standard budget (250,000 tokens) and
preserves usage counters and their period. Stored plan/subscription/budget values are
unchanged. Capability discovery checks the configured AI switch and account budget
before enabling generation, while context and private sessions remain available.
Policy/quota rejection responses explicitly identify the rejected operation and
state that it was not accepted. The transport verifies that identity and persists
the reason. Durable inspection still resolves the pending send; a missing operation
keeps the known rejection reason instead of replacing it. Unknown outcomes remain
fenced against automatic retry.

Replacing the test policy and quota substitutes with production services also
exposed a second failure: the legacy write guard treated a usage event carrying a
shared document ID as a manuscript edit. Usage events now have a narrow exemption,
alongside private conversation records and receipts. Shared content write fences
remain enabled. Owner/editor HTTP tests exercise actual permissions, local license,
entitlements, AI policy, quota charging, usage persistence, and conversation recovery;
only the external AI provider is deterministic. They verify a completed response,
one usage record, the charged counter, and unchanged underlying Free subscriptions.

Additional tests cover disabled/exhausted availability, quota/policy rejection across
inspection and local-store restart, and mismatched rejection operation identities.
Logs: `artifacts/brainstorm-send-focused.log` and `artifacts/brainstorm-send-regression.log`.
The final regression run passed **530 tests, 1 skipped, 0 failed** (531 total,
2 minutes 17 seconds), including Brainstorm, collaboration conversion, AI policy,
quota, and entitlement checks. The skipped check is the opt-in load benchmark.

The repaired site is running from `artifacts/brainstorm-send-build/bin/`, launched
with `artifacts/start-brainstorm-send-collaboration.ps1 -SkipBuild`. In a separate
Chrome tab, the real editor account now shows AI quota available. Reopening its
synced conversation restored the exact original love-story message, reached Context
ready, and enabled Send / Retry as a new request. The original tab was left untouched.
Evidence: `artifacts/brainstorm-send/retained-message-ready.png`. No paid provider
request was made and the retained message was not resent automatically.
