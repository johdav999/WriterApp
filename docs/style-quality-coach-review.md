# Style and quality coach review

Implemented on 2026-10-04 in the current WriterApp checkout.

The desktop AI review now states its criteria: clarity, repetition, sentence flow,
word choice, grammar and punctuation. It defaults to polishing the existing
style, with optional concise and vivid goals. Requests preserve voice, meaning,
names, facts, point of view, tense, language and paragraph breaks.

AI output is a bounded findings report. Each proposed edit supplies an exact
source passage, replacement, criterion, possible correction or optional
preference, reason, and effect on voice or emphasis. The review highlights
changed wording, supports individual selection, and shows a complete preview
composed from only the selected changes. Empty reports and empty approvals
cannot change the manuscript.

Apply patches only selected passages in one editor transaction. Unchanged
wording, inline formatting and surrounding blocks remain intact. All patches
are validated before dispatch. Ambiguous, overlapping, stale, unsupported
structural or mixed-format replacements fail without partially changing text.
Rich-formatting preflight runs before a generated proposal is offered.
Partial approvals retain their own immutable recovery record alongside the
original full review; existing undo, redo and original-copy recovery remain.

Local checks state their actual coverage and limits, including English passive
patterns and omitted cloud glossary terms. The web coach uses the shared style
goals for its existing individual AI fixes and highlights changed wording.
Changing its goal invalidates previously generated candidates.

The backend advertises SupportsStyleQualityReview. An older backend is rejected
before a billable style-review request. Custom prompt behavior remains on its
existing path; findings JSON never becomes a server replacement operation.

Verification evidence is under artifacts/style-quality/evidence. Tests cover
strict report parsing, source anchors, goal wiring, actual executor behavior,
component selection, partial-approval recovery, old-backend gating and the
existing quality and shared preview workflows. Browser checks use real rendered
Razor markup and production CSS at 320, 420 and 680 pixels. The editor scenarios
exercise the built production editor with formatting, Unicode, selection scope,
atomic rejection and undo/redo. Providers in these checks are synthetic.

Build/test logs: artifacts/style-quality-tests.log,
artifacts/style-quality-desktop-build.log and
artifacts/style-quality-editor-build.log. Isolated BaseOutputPath settings avoid
the currently running server's locked outputs. The build does not update or
restart the running application. Live signed-in provider output, a relaunched
native desktop session and deployment have not been verified.

## Style-request feedback repair (2026-10-05)

The AI request's progress, errors and proposal used to render after the separate
local-check panel. This put the response below the clicked button and often
outside the drawer's visible area. Several preparation guards also returned
without explaining why a review could not start, and a failed local save only
reported its error above the manuscript.

Feedback now renders immediately below the AI request button, before local
checks. It identifies save, sync and AI preparation, keeps cancellation available,
and focuses the completed result or error. Preparation guards and save failures
report beside the button. Empty reports explicitly confirm that the review
completed without suggesting changes; approval remains disabled.

Verification: 60 focused tests passed, including the actual workspace request
handler with synthetic changed-text, empty-report, service-error and
editor-not-ready responses, plus a rendered busy/cancel state. These tests check
feedback placement and unchanged saved writing. The Windows desktop build
succeeded with zero warnings and errors, and the rebuilt app was relaunched.
Live signed-in provider output and interactive native acceptance remain
unverified. Logs: `artifacts/style-feedback-tests.log` and
`artifacts/style-feedback-desktop-build.log`.

## Stale local backend repaired (2026-10-05)

The desktop displayed `Update the backend to support explained style reviews. No AI request was sent.` The message came from `DeviceAiService.ProposeAsync`: `/api/ai/status` omitted `supportsStyleQualityReview`, which deserializes to false. The preflight therefore stopped before the generation endpoint. The guard was behaving as designed.

The running desktop's assembly configuration points to `http://localhost:5387/`. That listener was the local backend started on 2026-10-04 at 17:07, using `artifacts/bin/Debug/net10.0/BlazorApp.dll` built at 17:05. `Program.cs` and the shared contract already contained the newer explained-review implementation, but isolated validation builds had not replaced the running backend. The captured live response confirms the mismatch; this was a stale running server, not a missing implementation in current source.

Built the current backend in an isolated folder and ran 63 focused style-review and device-action tests, all passing. Then rebuilt its normal launch folder (zero warnings/errors) and restarted the localhost backend at the same URL. The live status response now includes `supportsStyleQualityReview: true`; `/healthz` returns `ok` against the configured SQL Server database. Automatic database migrations remained disabled. The desktop did not need a restart because each proposal fetches fresh usage status.

Evidence: `artifacts/style-backend-repair/status-before.json`, `status-after.json`, `tests.log`, `results/style-backend.trx` and `backend-build.log`. The replacement service's PID is recorded in `backend.pid`. This fixes the observed capability rejection. No live billable generation or native review/apply interaction was performed, and no production deployment was made.

## Invalid style report repair (2026-10-05)

The next native requests reached the live provider but failed with
`ai.style_review_rejected` (HTTP 400). The active localhost backend log is
`artifacts/custom-prompt-service-fix/backend-fixed.stdout.log`: requests
`0HNP2KIFP113H:00000015` and `0HNP2KIFP113H:0000001A` reviewed 1,774 characters.
The first exhausted the general 800-token output limit; the second returned
441 tokens and also failed validation. Historical logs did not preserve the
specific validation reason or response, so the second report's exact defect
cannot be reconstructed. The client converted both into a generic request
error suggesting a problem with the selected writing.

Style reports were generated as ordinary text with prose instructions to
produce JSON, despite strict downstream requirements for six edit fields,
criterion/kind values and exact source anchors. The provider now uses a strict
Responses JSON schema for the edits array, with all fields required and extra
properties prohibited. The bounded style budget scales from 8,192 to 32,768
tokens with source length, unless configuration explicitly requests more.
Plain-text/custom and structured-section budgets retain their existing behavior.
Instructions clarify verbatim anchors, concise explanations and omission of
unchanged replacements. This follows the official
[Structured Outputs documentation](https://developers.openai.com/api/docs/guides/structured-outputs).

Incomplete provider responses are rejected explicitly as
`ai.style_review_incomplete`; invalid findings use `ai.style_review_rejected`.
Both are HTTP 502 result failures and have allowlisted desktop messages that
explain the failed report. Exact unique anchors, overlap, prose and paragraph
validation remain mandatory; no invalid report becomes an edit. Validation
reasons are logged without logging manuscript passages. No automatic billable
retry was added.

Verification: 83 focused tests passed, including provider schema/capacity,
completed empty reports, incomplete parseable responses, result status mapping,
device feedback, executor validation and approval/recovery regressions.
The isolated Windows desktop build succeeded with zero warnings/errors.
A real `gpt-4.1-mini` adapter request using only synthetic writing (1,794
characters) returned 22 validated edits, consuming 1,332 output tokens. It
composed a changed preview and read/applied no user's manuscript.
Evidence: `artifacts/style-request-fix/tests.log`, `results/style-request.trx`,
`desktop-build.log` and `live-provider-result.json`. Interactive native
review/apply acceptance and production deployment are not established by these
checks.

The user explicitly authorized activation after automatic approval review
initially rejected the restart. Replaced the identified local backend at the
same `http://localhost:5387` URL with the tested build at
`artifacts/style-request-fix/tests/Debug/net10.0/BlazorApp.exe` (PID 45080).
Startup migrations remain disabled; `/healthz` returns `ok` for SQL Server.
Gracefully closed Prosa through its main window, rebuilt its normal launch
folder with zero warnings/errors, and reopened it (PID 22048).
Evidence: `backend-health.json`, `backend.pid`, `desktop.pid`,
`desktop-launch-build.log` and `running-processes.json` in
`artifacts/style-request-fix`. Live interactive native review/apply acceptance
remains distinct from process activation and the synthetic provider check.

## Targeted repeated-word review repaired (2026-10-05)

The screenshot's `The suggestion did not safely reduce the repeated word`
message comes from `LocalQualityChecks.Validate`, after the review click and AI
request but before preview construction. The handler was running. The original
provider candidate is unavailable, so its exact counts cannot be reconstructed.
Source inspection and three initially failing regression tests established:

- The shared five-word repetition rule treated ordinary separated English
  articles (`a`, `an`, `the`) as rewrite targets. They are now excluded unless
  they are adjacent duplicates separated by whitespace, such as `the the`.
- Reduction validation imposed a maximum of one occurrence for anchors of
  three or more characters. A valid three-to-two reduction was rejected.
  Validation now accepts an actual reduction, while retaining the existing
  prose, no-op, length, stale-source and exact-passage guards.
- Expiring an older word from the rolling window erased its newer occurrence.
  Expiration now removes only the occurrence that actually left the window.

Desktop targets now include both occurrences when the repetition crosses a
sentence boundary, bounded by the checked selection. Pending, rejection and
approval feedback render beside the clicked finding. Rejections and completed
reviews receive focus; failed approval retains its error in the existing
preview. An invalid candidate creates no preview, saved edit or history entry.

Verification: 75 focused tests passed, covering shared rules/validation,
selection scope, actual desktop component handlers, approval/history,
cancellation, stale edits, and checked web-quality endpoints. Nine headless
Edge checks exercised actual rendered Razor markup at 320/420/680 pixels and
the built quality editor's targeted previews, emphasis preservation and
mismatched-passage rejection. AI responses were synthetic. Screenshots and
results are in `artifacts/targeted-quality-fix/evidence`; test results are in
`artifacts/targeted-quality-fix/results/after.trx`.

The isolated desktop build and normal launch-folder build both succeeded with
zero warnings/errors. Prosa was closed gracefully and relaunched from the
normal output (PID 15848). The local backend was not restarted; shared web-rule
changes require an updated web/backend build to take effect there. A concurrent
unrelated `BiblePatchApplier.cs` compilation error interrupted the extra evidence
build, so that test was compiled against the already validated dependencies;
the final focused run used those tested binaries. Logs are `tests.log`,
`ui-evidence.log`, `browser.log`, `desktop-build.log` and
`desktop-launch-build.log` under `artifacts/targeted-quality-fix`.
Live provider output and interactive native review/apply acceptance remain
unverified; process activation and browser fixtures do not establish those.

## Targeted fix formatting rejection repaired (2026-10-05)

`LocalQualityPanel.Review` called `validateQualityRange` on the full sentence
context before generation. That guard requires every text node to have the
same inline marks, so even an unchanged bold name or italic word rejected an
otherwise safe targeted fix. The browser regression reproduced the reported
`Module.xb` formatting error against the previously shipped bundle. The panel
also displayed `JSException.Message` verbatim, duplicating the message and
exposing the WebView stack trace.

Targeted checks now use dedicated preflight and preview exports. Preflight
still verifies exact offsets, whole-word/Unicode boundaries, one source block
and absence of embedded content, while allowing inline marks in sentence
context. Preview retains the common unchanged prefix and suffix, expands the
changed portion to complete words, and replaces only that portion using its
source marks. Deletions can remove a duplicate across marks without changing
the formatting of surviving text. A nonempty replacement spanning mixed marks
is still rejected. Paragraph splits retain their existing stricter validation;
writing and consistency tools keep their existing guards. Provider markup
stays inert, and the preview verifies the full resulting plain text and uses
the existing explicit approval/save/history path. Error feedback now displays
only the first actionable line beside the clicked finding.

Verification: 111 focused .NET tests passed, including actual component
preflight/preview rejection, no billable preflight failure, unchanged saved
writing and empty history on failure. Eight targeted-formatting browser
scenarios, six explained-style editor scenarios and all 82 full shipped-editor
scenarios passed in headless Edge. The full harness's punctuation fixture ended
before its expected comma; its endpoint was corrected to include the comma,
without changing selection behavior. Tests cover rich context/target marks,
later identical sentences, Unicode, links, duplicate deletion, inert markup,
exact saved-preview reload, paragraph splits and structural rejection.

The isolated and normal Windows desktop builds succeeded with zero warnings
and errors. Prosa was closed through its normal save-on-close handler and
reopened from its rebuilt normal launch folder. Evidence is under
`artifacts/targeted-quality-formatting`, including `tests.log`,
`results/formatting.trx`, `evidence/editor-results.json`,
`evidence/full-editor-results.json`, `desktop-build.log`,
`desktop-launch-build.log` and `desktop-process.json`. No live provider request
against the user's passage or interactive native review/apply acceptance was
performed. These source changes require no backend restart.
