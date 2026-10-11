# Brainstorm follow-up after Keep

Date: 2026-10-09. Same WriterApp checkout; preserve every unrelated uncommitted change.

## Confirmed root cause

The screenshot reports “Writing changed. Save, synchronize and review server
context explicitly before sending,” while the chat still says “Context ready”.
Read-only metadata confirms one completed Draft request, one generated draft, a
committed local Keep receipt, two conversation turns, no pending generation and a
retained 162-character follow-up composer. The second message did not reach the
model. The author had applied the first draft to the manuscript; the checked writing
snapshot consequently became stale. The guard correctly rejected that snapshot,
but the chat's readiness flag was independent of manuscript changes and remained
true, hiding its preparation action.

The mounted reproduction also exposed the next blocker: after new context was
prepared for the kept manuscript, the old draft remained selected as an unapplied
revision parent. Its original immutable target source differs from the new checked
source. Treating it as a revision parent would incorrectly reuse stale provenance.

## Changes

- The native parent supplies its current authored-writing fingerprint to the shared
  workspace. Readiness is invalidated when that fingerprint changes after review,
  including Keep, undo/redo and ordinary edits. Metadata-only sync acknowledgments
  do not change that fingerprint.
- The shared pre-send callback invalidates readiness if the host's final flush or
  writing check fails. Both hosts then expose preparation instead of keeping a
  misleading ready state. Pending writing and the composer remain intact.
- On explicit fresh preparation, a committed draft whose physical destination is
  unchanged but whose source revision has advanced remains visible as the saved
  original and is no longer automatically used as an unapplied revision parent.
  The new proposal uses the newly checked manuscript context and target. Original
  drafts, targets, context records and Keep receipts are not rewritten.

Unkept proposal revisions still require their original source/destination. A changed
scene does not silently retarget an old draft. There is no keyword/regex routing,
automatic model send, automatic manuscript apply or replay of a completed operation.

## Issue ledger and verification

| Issue | Evidence | Correction | Acceptance |
|---|---|---|---|
| BS-SEND-01: stale ready state after Keep | Author screenshot; retained cache metadata; actual mounted native host | Bind readiness to authored writing and invalidate after a rejected host preflight | Automated mounted flow passes; physical native interaction remains open |
| BS-SEND-02: kept draft retained as stale revision parent after preparation | Mounted first-send/Keep/refresh/second-send reproduction | Fresh context uses kept manuscript writing for a new proposal; retain original immutable draft | Automated authenticated HTTP and native-host flow passes; no live model replay performed |

853 combined regressions passed with zero failures/skips. The extended four-case
mounted regression uses the production device authentication handler, local save,
Keep workflow/history, sync v5 controllers, actual server context preparation and
authenticated conversation transport. A synthetic provider completes both sends.
It verifies two provider calls, four turns, two drafts, an unchanged first draft,
one manuscript history entry and no second automatic manuscript application.
Sign-out, reauthentication, source revocation and rejected-token invalidation are
checked after the follow-up. Existing unkept changed-source and changed-scene guards
also pass. A separate shared-workspace check verifies that late pre-send rejection
retains the composer and exposes successful fresh preparation with no dispatch.

Evidence: `artifacts/brainstorming-p12-second-send/combined-regressions.trx` and
`artifacts/brainstorming-second-send/reported-failure-metadata.json`.
Browser/build results and final runtime/preservation receipts are recorded alongside
these checks. Synthetic provider results do not establish live model interpretation
or native mouse interaction. No real writing was transmitted diagnostically.

All ten browser suites and server/client Release plus isolated desktop Debug builds
passed. The static Keep fixture was updated to supply its actual selected section
and assert that the comparison remains hidden during the existing approval review;
the old check tried to click content intentionally hidden by that review. No
production proposal tab/layout change was made. Fourteen focused checks and six
final Keep-host fixture checks passed after the test updates.

The running author-owned normal desktop (PID 30456) and backend (PID 34768) were
preserved. A separate Debug desktop using **normal Prosa data**, with an empty
`ProsaValidationDataDirectory`, was built with zero warnings/errors at:

```text
WriterApp.Desktop/artifacts/brainstorming-second-send/user-build/Debug/net10.0-windows10.0.19041.0/win-x64/WriterApp.Desktop.exe
```

Close the old Prosa window before launching this rebuilt executable. From the
repository root, the guarded launcher is:

```powershell
pwsh -File .\artifacts\brainstorming-second-send\start-fixed-prosa.ps1
```

It refuses to replace/stop an existing desktop process and starts no backend or
model request. No new executable was launched during verification. The normal
desktop launch folder still holds the previous build until the author closes it
and rebuilds that folder; restarting that old executable alone does not load this
fix. The existing backend can remain running for this native correction.
`final-verification.json` records hashes, normal-data configuration, ten browser
reports, all 19 unchanged original cache files, preserved checkout entries and zero
agent-started/stopped hosts or diagnostic provider calls. `git diff --check` passed.

## Use and handoff

After keeping a proposal, prepare current context for the next send. On desktop,
confirm Save, sync & prepare context at the displayed backend. The follow-up text
stays in the composer; preparation does not send it. Then explicitly Send. Review
and Keep the new proposal separately. Preserve any pending/uncertain operation and
inspect it before retrying. The original successful scene is already saved.

Do not stop author-owned hosts or leave an agent listener on localhost:5387 at
handoff. Running application files cannot be replaced until those applications
are closed; use isolated build output for verification. Report the exact executable
that includes the correction rather than assume an open application has loaded it.

```text
Continue in C:\Users\Johan\source\repos\WriterApp with all uncommitted work intact. Read docs/brainstorming-second-send-uat.md and the current implementation-status handoff. Preserve the native authored-writing fingerprint binding and shared failed-preflight readiness invalidation; Keep/undo/edits must expose fresh context preparation without losing the composer. Explicit preparation of the kept manuscript must retain the old draft/target/receipt immutably and use current manuscript context for the next proposal, rather than silently reuse that kept candidate as an unapplied revision parent. Unkept revisions and changed destinations retain their existing guards. Latest combined regressions:853 passed, including mounted production first-send/Keep/sync/fresh-context/second-send with real device authentication and HTTP/controllers but a synthetic provider. No diagnostic live model request or real manuscript mutation was performed. Preserve the retained author draft and follow-up; do not replay them automatically. Rebuild/restart the intended desktop before claiming native acceptance, and preserve author-owned hosts/port5387. Collect explicit author-driven fresh preparation and follow-up acceptance, update exact evidence and keep broader P12 native/provider/audio/account/physical-exchange gates separate.
```
