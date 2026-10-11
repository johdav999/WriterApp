# Prompt 6 draft/version acceptance packet

Date: 2026-10-07. Product: Prosa web and Windows desktop Brainstorm workspace.
Role: author, synthetic owned standalone/project data. Revision: original checkout's
uncommitted P1–6, based on `09352ded5c4f115578329323cf48569f141babd6`.
Environment: local SQLite/owned generation services and authenticated P4 TestServer
regressions; deterministic provider; real headless Edge 154.0.4258.62 modules; isolated
rebuilt Windows startup. No provider credentials or real manuscript data used.

Run `npm run build` in `WriterApp.Client`, then at repository root:

```powershell
./scripts/verify-brainstorming.ps1 -Phase p06 -Browser -PlaywrightModule 'C:/Users/Johan/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright' -NativeStartup
```

Ignored evidence lives in `artifacts/brainstorming-p06`: build/test logs and TRX,
validated .NET draft-buffer/base caches, real-handler review/versions HTML, browser
reports and wide/narrow screenshots. See the implementation status for exact rebuilt
desktop/data paths. Native validation closes only its launched process. Browser interop
receivers and persistence fixtures are synthetic; they exercise the actual shipped
editor factories, proposal module and IndexedDB, not authenticated interactive hosts.

## Flow packets

| Flow | Preconditions and actions | Expected and observed | Evidence/scope |
|---|---|---|---|
| P6-01 Generate/revise without changing writing | Checked context/target, owned session; deterministic Draft, author rich edit, checkpoint, fresh same-destination context, verified selected passage, Revise. | Three distinct versions: generated opening, manual checkpoint, generated revision. New revision carries manual parent and original target; exact manual plain/HTML and quote sent in quoted provider input. Manuscript/history/application remain unchanged. Passed through shared review and both production host checkpoint handlers. | `ReachableHostCheckpointAndSharedVersionReviewUseManualCandidateInNextGeneration`, TRX; real SQLite/file persistence and deterministic provider. |
| P6-02 Reopen/edit/compare/navigate versions | Edit proposal with bold Unicode text and another paragraph; read the saved buffer through a new service facade, checkpoint, previous/next version, inspect provenance/facts/warnings/application state. | Buffer and manual version survive persistence; original retained, ancestry and independent application status visible. Comparison uses captured source and reviewed candidate. Both actual common editors preserve marks/Unicode and IndexedDB restores buffer/review after reload. Passed. | Draft tests, `browser/draft-result.json`, `draft-web-wide.png`, `draft-device-narrow.png`; static production Razor/interop fixtures for browser. |
| P6-03 Selection and changed destination | Capture exact common quality text selection, pin quote/offset/fingerprint; submit wrong quote/fingerprint/range/candidate; refresh a different scene or same location with changed source. | Lost/forged selections refuse before provider. New-scene shared Send uses the fresh target without the old draft as parent; stale same-location revision refuses. Original draft/target remain intact. No guessed selection or silent rebase. Passed. | Server/workflow/shared Send tests; actual browser selection mapping in both factories. |
| P6-04 Exploration/clarification/cancellation | Ask about a candidate; provider returns Explore/Clarify; stop a later revision after draft-check status. | Existing candidates remain byte-equivalent; Cancelled partial output creates no draft/application. Existing unknown-operation/usage and late-stream guards remain. Passed. | Draft tests and authenticated P4 cancellation/recovery/transport regressions. Provider intent/quality is deterministic here, live gate below. |
| P6-05 Unsupported content and private lifecycle | Attempt unsupported style/script/malformed/mismatched HTML; empty checkpoint; change document/account then deliver a late edit callback. | Unsafe snapshots refuse without replacing originals. Empty buffer remains recoverable when checkpoint refuses. Old private callback is not adopted into another review. Proposal editor is read-only during generation and no-op open does not create a manual version. Passed. | Draft tests; both real factory rejection/read-only browser checks; P1–5 privacy/navigation regressions. |

Combined verification: 495 passed, zero failed/skipped; server/web Release and Windows
Debug builds passed with zero warnings/errors. Existing LocalStoryboard test analyzer
warning remains. Existing browser tests also passed: six IndexedDB groups, 109 editor
checks, nine context groups, eight projection comparisons, both conversation renders
and four workspace groups. These are scoped checks, not full live acceptance.

## Issue ledger

| ID | Severity | Flow | Summary and acceptance regression | Status |
|---|---|---|---|---|
| P6-I01 | P1 | P6-01 | Workspace follow-ups never passed a parent draft. Send now checkpoints and carries the exact reviewed/manual candidate; both host handlers and real server request inspection verify the manual plain/HTML input. | Fixed and fixture-verified |
| P6-I02 | P1 | P6-02 | Manual edits had no durable UI workflow. Separate recoverable edit buffer, immutable authored checkpoint and persistent reviewed-candidate selection now survive reload and version navigation. | Fixed; module/persistence verified |
| P6-I03 | P1 | P6-03 | A refreshed target ID could detach a follow-up from its original destination. Same-location revision retains original target and requires unchanged stamp; different-scene send uses its fresh target without retargeting earlier prose. | Fixed; production shared Send/workflow verified |
| P6-I04 | P1 | P6-05 | Async editor capture/late callback could adopt into a later scope. Captured candidate fingerprint/scope/epoch and immediate identity invalidation retain private originals; document/account callback tests pass. | Fixed and fixture-verified |
| P6-I05 | P2 | P6-02 | Warnings were only available from the newest generation. Generated drafts now retain contradictions, inherited by manual checkpoints and shown with each version. | Fixed and fixture-verified |
| P6-I06 | P2 | P6-02 | No-op editor normalization could be mistaken for a manual edit. Flush uses the actual edit counter; untouched open does not checkpoint normalized content. | Fixed; shipped editor no-op regressions retained |
| P6-G01 | — | All | Full signed-in interactive web/native sample journey and live configured provider quality/intent/selection-following/settlement have not been exercised. | Pending external acceptance |
| P6-G02 | — | P6-02 | Remote-only session discovery/import and concurrent cross-device recovery are P9 integration work; scoped cached history remains available. | Pending P9 |

Fixture corrections during verification: browser insertion of an existing revision-2
buffer must first insert its revision-1 base; synthetic synchronization must return the
latest composer-persisted cache, like the real adapters. Neither fixture bypasses CAS.

## Interactive gate reproduction

Use disposable synthetic standalone and project manuscripts: multiple Chapter 1 pages,
Chapter 2, synopsis, notes and an explicitly selected supporting document. Run the
isolated rebuilt desktop executable from the status and a local signed-in web editor
at `/documents/{DocumentId}` against a configured backend/test account.

1. Save/synchronize and explicitly review fresh server context. Ask for a first section
   of Chapter 2 based on Chapter 1. Verify prose is in the middle, explanation on the
   right, source/target/facts/warnings visible, and manuscript unchanged.
2. Edit the proposal with rich marks and Swedish/Unicode text, close/reopen Brainstorm
   and restart/reload. Verify retained buffer/checkpoint and no manuscript autosave of
   proposal prose. Use previous/next versions and compare captured source/current candidate.
3. Select a passage, choose Use selected passage in next follow-up, then request a
   more suspicious protagonist while preserving another paragraph. Verify the request
   uses the edited reviewed candidate/quote and the new version follows its manual parent.
   Check actual provider quality and paragraph preservation separately from transport success.
4. Ask why she would approach the stranger; confirm exploration does not revise prose.
   Use an ambiguous reference; confirm a narrow clarification rather than an invented range.
   Change the selected scene: next requests use its newly reviewed destination, while
   old proposals retain their original destination. A stale same-location revision refuses.
5. Stop during revision, disconnect/reload and inspect. Prior complete versions survive;
   incomplete/unknown output is not a candidate and inspection never triggers paid replay.
6. Change account/backend/document during edit/generation and deliver delayed work.
   Earlier private content must not appear in the new review; original cache remains.

No Keep/apply control is supplied in P6. Prompt 7 implements approved manuscript
application/history/undo; prompt 8 implements planning application. SQL Server,
preexisting SQLite multi-manuscript migration-index, packaged/MSIX and speech gates
remain as recorded in the main status. Rebuild matching client/server for P6 JSON fields;
strict older backends refuse new fields with local data retained.
