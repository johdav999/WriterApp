# Focused Brainstorm draft screen — 2026-10-09

## Scope and implementation

Implemented the reference image's draft-first layout in the shared web and Windows desktop UI. Work stays in the existing checkout and preserves the ongoing, uncommitted brainstorming implementation.

The proposal has its own `EditorWorkspace.Proposal` slot. The manuscript editor remains mounted behind the Manuscript navigation control, retaining pending content and its existing save lifecycle. Outline starts collapsed. On narrower screens, Outline, Manuscript, Brainstorm, and Conversation select one pane at a time.

`BrainstormingProposalPreview` now has Draft, Compare, and Details views. Switching views hides presentation without destroying the proposal editor. The compact toolbar uses the existing shipped editor command and formatting-state APIs in both hosts. Story facts are collapsed; version navigation, provenance, application inspection/undo/recovery, and new-section naming live in Details. Selected-passage follow-up and replacement review remain under More draft actions. Insertion review and saving a version remain visible in the footer. Confirmation shows the exact candidate and still requires explicit approval.

The conversation panel uses quieter assistant messages with an anchored composer. At phone widths, the global search header is hidden while Brainstorm is open; Back to writing restores the normal editor and global search. Existing product colors and application navigation are retained.

## Product profile and evidence

- Product: Prosa, shared Blazor web/Windows desktop writing UI.
- Role: author using the built-in Development LocalDev identity and a disposable migrated SQLite fixture.
- Entry point: `/app/documents/{documentId}/sections/{sectionId}`, then Brainstorm → History → Find synced conversations → Open saved conversation.
- Data: synthetic restored draft from `BrainstormingAcceptanceTests`, edited with Arctic scene prose in the real browser editor. No live model calls or real account data used.
- Builds: isolated `BaseOutputPath=artifacts/brainstorming-focused/build/` for server/client and tests, `artifacts/brainstorming-focused/native-build/` for desktop.
- Evidence root: `artifacts/brainstorming-focused/` (ignored artifacts). The owned loopback acceptance server was stopped after verification.

## Issue ledger

| ID | Severity | Finding | Acceptance / evidence | Result |
|---|---|---|---|---|
| BFD-01 | P2 | Proposal stacked above the complete manuscript editor and redundant controls | Sole visible draft editor; retained mounted manuscript; `browser/focused-wide.png`, workspace regression and two-factory browser checks | Verified in rebuilt web UI and shared handlers |
| BFD-02 | P2 | Facts, provenance, versions and multiple application actions overwhelm prose | Draft/Compare/Details retain edits and composer; collapsed facts; compact toolbar uses real commands; `browser/result.json`, `component-browser/draft-result.json` | Verified |
| BFD-03 | P2 | Primary review action disappears below long prose | Independent content scroll; footer visible with 18 repetitions of scene text; review/cancel leaves manuscript unchanged; `browser/long-draft.png`, `browser/review-insertion.png` | Verified in rebuilt web UI |
| BFD-04 | P2 | Stacked phone header/footer leaves no useful prose space; composer can overlap planning | Compact global header and bounded composer; 900×800 and 390×844 captures with no horizontal overflow; `browser/draft-390.png`, `browser/conversation-390.png` | Verified in rebuilt web UI |
| BFD-05 | P3 | New-first-section draft incorrectly reports another destination | Compare active section to the before-section anchor for create-section targets; focused fixture shows correct status, truly different destinations retain the notice | Verified in shared tests and web UI |

## Validation

- All 408 brainstorming .NET tests passed (`all-tests.log`, `brainstorming.trx`). After final layout refinements, the 63 draft/keep/workspace tests passed again (`final-tests.log`), followed by the final shell regression (`shell-tests.log`).
- Server/client compiled through the test builds. Windows desktop Debug build passed with zero warnings/errors (`desktop-build.log`). The test project reports two existing analyzer warnings unrelated to this change.
- `Tests/brainstorming-focused.mjs`: actual rebuilt application, wide/tablet/phone layouts, persistent editing across tabs and panes, save checkpoint, real heading command, long-draft scroll, exact insertion review, cancellation, and unchanged original manuscript. Zero generation requests, browser exceptions, or failed stylesheets.
- `Tests/brainstorming-drafts.mjs`: both shipped Web and Device editor factories; compact heading/undo/redo controls, generation read-only controls, rich Unicode and selection mapping, real IndexedDB round-trip, and refusal of unsupported markup.
- `Tests/brainstorming-workspace.mjs`: retained manuscript HTML, resize bounds, pointer/keyboard resizing, responsive panes and composer focus restoration. This harness uses static production Razor and synthetic pane bindings; actual interactive navigation is covered separately by the rebuilt-app test.
- `git diff --check` passed.

## Remaining acceptance boundary

Native Windows interactive acceptance and live provider/microphone playback were not performed. Desktop compilation and testing the Device editor factory in Edge do not establish that a running installed desktop binary has been updated. No user-owned desktop session was closed or replaced. The reference image is a design concept; the screenshots above are implementation evidence.
