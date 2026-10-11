# Brainstorm manuscript placement

Updated: 2026-10-10. Implemented in the existing checkout; earlier phased work is retained.

## Outcome and root cause

The original target resolver selected a section's first page with offsets 0/0.
The shared Keep transform prepended `proposal + source`, and both editor mappings
required page-start insertion. Cursor position controlled only selected-passage
replacement. This explains the reported text appearing at the manuscript opening.

Review & keep now opens an author-facing placement review shared by web and desktop:

- **Continue this section** defaults to the end of its last page, retaining all earlier pages.
- **Create a new section** requires a title and an explicit section to place it after.
  A linked section creates a sibling scene at the corresponding outline position;
  a standalone or unlinked section creates manuscript structure without inventing project nodes.
- **Choose a place in the manuscript** selects a section, page, and top-level passage boundary.
  The picker shows actual saved excerpts and paginates long pages in groups of 12.

The preview shows a readable chapter/section/page destination, surrounding excerpts,
and the exact rich draft highlighted as an addition. Approval uses **Add here**.
Replacement remains an explicit separate action. Existing explicitly prepared
new-first-section drafts retain their checked chapter position and offer a title field.
Confirmed additions expose **View in manuscript** and **Undo**.

## Implementation and compatibility

`BrainstormingKeepReview.Placement` is optional and omitted from JSON when absent,
preserving serialized legacy reviews and their immutable receipt hashes. The draft's
original generation target remains unchanged. A separate reviewed placement resolves
the effective destination from the saved writing snapshot. The review hash pins that
placement along with the candidate, source, and resulting HTML.

The server and local persistence, history, guarded undo/redo, scene mirroring, and
navigation use the effective destination. Section creation adjusts actual sibling
orders rather than assuming order zero. Standalone insertion/undo needs no project.
Rich editor preflight maps an explicit block boundary to ProseMirror positions in
both rebuilt bundles. Source changes, missing/foreign destinations, unsupported
mapping, or uncertain outcomes retain the existing refusal/recovery boundaries.
Selecting another location never reads an incidental live cursor.

Deploy updated web/client, server, and desktop binaries together. No database migration
is needed. An older backend rejects the new optional placement payload under its
strict JSON contract; it must not silently fall back to prepending.

## Product profile and acceptance

Product: Prosa web and Windows desktop, author manuscript review.
Evidence: `artifacts/brainstorming-placement/`; production web UI at 1540×1000 and
390×844, actual web/device rich-editor modules, C# production host callbacks,
migrated disposable SQLite server data and file-backed local writing.
The browser uses the built-in LocalDev test identity and a restored synthetic draft.
It sends no provider requests. Only this session's acceptance servers were stopped.

| ID | Severity | Flow / acceptance | Evidence | Result |
|---|---|---|---|---|
| BP-01 | P1 | Default continuation goes after the final page; first page and unrelated prose stay intact. | Placement tests, `browser/continue-wide.png` | Verified |
| BP-02 | P1 | Named section appears after the chosen section; actual node and section orders survive guarded undo/redo, duplicate approval and restart. | Server/local placement tests; production browser approval, undo/redo and navigation | Verified |
| BP-03 | P1 | Selected page boundary preserves formatting, Unicode and unrelated blocks; a stale/forged/foreign position cannot apply. | Placement tests; actual web/device mapper checks in `browser/result.json` | Verified |
| BP-04 | P2 | Empty title blocks approval; keyboard controls and narrow layout remain usable. Preview is visible with compact desktop options and a fixed approval footer. | Production browser replay; `browser/new-section-wide.png`, `browser/paragraph-narrow.png` | Verified |

Validation:

- Frontend `npm run build --prefix WriterApp.Client`: both shipped editor bundles rebuilt.
- 525 broader Brainstorming/history/sync/content regressions passed, zero failures/skips:
  `artifacts/brainstorming-placement/regressions.trx`.
- After final presentation/copy changes, 46 focused placement, production host and
  workspace checks passed, zero failures/skips: `final-focused.trx`.
- Release server/web build and Debug Windows desktop build passed with zero warnings/errors.
  Test compilation reports the two existing framework/analyzer warnings in unrelated tests.
- `Tests/brainstorming-placement.mjs`: actual production UI, validation, rich mapping,
  persisted approval, undo/redo, and View in manuscript passed; zero page errors,
  failed CSS requests or provider sends. Report: `browser/result.json`.

A native Windows interaction and live model generation were not exercised. Native
parity evidence covers the shared production UI, desktop host callbacks, actual device
editor mapping, file-backed persistence and a successful native build.

## Reproduction and runtime handoff

Use isolated outputs to avoid replacing the author's running binaries:

```powershell
dotnet test Tests/WriterApp.Tests/WriterApp.Tests.csproj -p:BaseOutputPath=artifacts/brainstorming-placement/build/ -p:NuGetAudit=false --filter 'FullyQualifiedName~BrainstormingPlacement|FullyQualifiedName~BrainstormingKeepHost|FullyQualifiedName~BrainstormingWorkspace'
dotnet build BlazorApp.csproj -c Release -p:BaseOutputPath=artifacts/brainstorming-placement/release-build/ -p:NuGetAudit=false
dotnet build WriterApp.Desktop/WriterApp.Desktop.csproj -c Debug -p:BaseOutputPath=artifacts/brainstorming-placement/native-build/ -p:NuGetAudit=false
```

To create a fresh browser fixture, set `WRITERAPP_BRAINSTORMING_PLACEMENT_FIXTURE=1`
and `WRITERAPP_BRAINSTORMING_ACCEPTANCE_DB` to a new file inside the evidence directory,
then run `IntegratedFixtureHasOwnedMultiPageChaptersStandaloneAndExplicitConflictingNotes`.
The fixture refuses to overwrite existing databases. Launch it with
`scripts/start-brainstorming-acceptance.ps1` at port 5399, after checking the listener.
Set `PLAYWRIGHT_MODULE`, `BRAINSTORMING_WEB_SEED` to its adjacent JSON, and run
`node Tests/brainstorming-placement.mjs`. This test mutates only the disposable fixture.
Stop the recorded owned host after the replay and before rebuilding that output.

The isolated Windows executable is at
`WriterApp.Desktop/artifacts/brainstorming-placement/native-build/Debug/net10.0-windows10.0.19041.0/win-x64/WriterApp.Desktop.exe`.
The author's existing app was not closed or replaced. A normal rebuild/relaunch or
launching the updated isolated executable is required to activate these changes there.
