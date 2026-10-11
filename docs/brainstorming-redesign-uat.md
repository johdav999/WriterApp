# Brainstorm right-panel redesign — 2026-10-08

## Scope and design

Author workflow in the shared `BrainstormingWorkspace`, conversation and context components, consumed by both the web editor and Windows desktop. Existing uncommitted brainstorming implementation was preserved in the same checkout.

ImageGen references and exact prompts: [design references](design/brainstorming/reference-prompts.md). Two images cover empty chat, active discussion, context, and history. The implementation uses existing Prosa navy tokens rather than the first reference's exploratory teal. Generated assistant text is illustrative, never seeded into the product.

Chat is the primary view. Context and History remain mounted behind navigation so switching views preserves the composer, source choices, and review state. Prompt cards populate and persist a draft without sending or overwriting existing text. Audio, planning, and request details use progressive disclosure. Selecting a discussion item for planning opens its review. Reopening still revokes context readiness; manuscript writes retain the existing approval workflows.

The composer sits below the scrolling discussion. The inherited tablet/phone shell previously expanded vertically, moving it off screen; height containment is now scoped to an active Brainstorm workspace. Audio can expand within a bounded scroll area.

## Issue ledger

| ID | Severity | Finding | Acceptance and evidence | Result |
|---|---|---|---|---|
| BR-01 | P2 | Session/recovery/setup controls overwhelmed the main task | Chat opens first; Context/History expose secondary workflows; starters populate without dispatch | Verified in rebuilt web UI |
| BR-02 | P2 | Small composer below lengthy setup content | Full-width textarea and Send visible; conversation scroll independent; 1600x1000, 900x800, 390x844 | Verified in rebuilt web UI |
| BR-03 | P2 | Audio and diagnostics competed with writing | Audio collapsed initially and keyboard reachable; request details and per-message planning actions disclosed on demand | Web UI plus real Razor audio fixtures |
| BR-04 | P2 | View reorganization could lose draft/review state | Switch all views, preserve exact draft and selected destination, prepare context, reopen and require review again; zero generation requests | Shared handler tests plus rebuilt web UI |

## Verification

- All 392 brainstorming .NET tests pass. Existing empty-state assertions were updated; mounted component preservation is covered in the workspace regression.
- Web/server and Windows desktop Debug builds pass with zero warnings/errors using isolated `BaseOutputPath` directories.
- `Tests/brainstorming-redesign.mjs` exercises the actual rebuilt web editor against a disposable SQLite database and built-in LocalDev identity. Checks empty chat, prompt starter, context settings/preparation, History, reopen, audio disclosure, active-view semantics, draft retention, and viewport bounds. No AI request was sent; no browser exceptions or failed stylesheet loads were recorded.
- Existing conversation, dictation, and voice browser fixtures pass after adapting their assertions to the audio disclosure. These include safe text rendering, keyboard focus, narrow layouts, synthetic microphone lifetime, and speech interruption handling.
- Evidence: `artifacts/brainstorming-redesign/brainstorming-regressions.trx`, `all-tests.log`, `web-build.log`, `desktop-build.log`, `browser/result.json`, `browser/chat-panel.png`, `browser/context-panel.png`, `browser/history-panel.png`, and wide/narrow/phone captures. Audio fixture results are in `component-browser/`.
- The acceptance server was agent-owned and stopped after capture. No user-owned desktop process was closed or replaced.

## Limits and next acceptance slice

Native Windows interactive acceptance and live provider/microphone playback were not performed for this visual redesign. The shared production components compile into desktop, but this does not establish acceptance of a running installed binary. Next native check: launch the rebuilt desktop with a retained manuscript and verify tab switching, draft retention, long conversation scrolling, and review actions at the user's preferred panel width. References are design assets; browser screenshots are implementation evidence.
