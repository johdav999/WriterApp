# Synopsis coaching panel repair — 2026-10-06

Profile: Prosa Windows desktop Story → Synopsis panel, author role, synthetic local manuscript and mock AI responses. Changes are in the current checkout, alongside existing uncommitted work. Evidence: `artifacts/uat/synopsis-coaching/`. No user manuscript or account data changed during verification.

The screenshot's inline selectors, small notes textarea and oversized repeated review headings came from the synopsis branch of `LocalAiPanel` lacking dedicated layout rules. Shared coaching feedback also used default heading sizing and viewport-based comparison columns. The pinned coaching pane used automatic overflow while custom scrollbar styling applied only to the outer panel scrollbar.

| ID | Severity | Finding | Acceptance | Result |
|---|---|---|---|---|
| SYN-COACH-01 | P2 | Inline, unstyled coaching controls and inconsistent hierarchy | Labeled, full-width controls with visible focus; responsive columns based on pane width | Verified in compiled Razor/Edge fixture |
| SYN-COACH-02 | P2 | Long review needs discoverable vertical scrolling | Visible scrollbar; mouse and PageDown scrolling; complete result and Apply/Dismiss reachable in narrow/short panes | Verified in compiled Razor/Edge fixture |
| SYN-COACH-03 | P2 | Repeated large headings and cloud revision dominate review | One compact review title, expandable source details, readable current/suggested text with changes highlighted | Verified in compiled Razor/Edge fixture |

The lower pinned pane now explicitly scrolls vertically and shares the panel's scrollbar styling. The synopsis component retains its full content height within that pane. Comparison text participates in the pane's scrolling rather than introducing extra nested text scrollbars. Controls use shared design tokens; current/proposed columns stack when the pane is narrow. The existing reviewed-field Apply, history, cancellation and source/revision validation remain in their host services.

Validation:

- 59 focused .NET tests passed: `LocalSynopsisPanelTests`, `LocalSynopsisCoachingTests`, `WebCheckedSynopsisTests`. Includes all ten reviewed-field Apply/undo paths, cancellation, stale-source rejection and long outputs in all three modes.
- 19 Headless Edge layout scenarios passed using compiled production Razor markup, generated isolated styles and production editor CSS. Widths 640/360/280, short pane height 420, phone viewport 390; evaluate/questions/suggest and long outputs. Verifies mouse/keyboard scrolling, focus, reachable actions, pane-based comparison columns and no horizontal overflow. Browser renderer errors: zero.
- Existing Story resize harness: 9 checks passed, including upper/lower scrolling, short-window clamping and narrow layouts.
- Windows Release build passed with warnings treated as errors: zero warnings/errors. Output: `WriterApp.Desktop/artifacts/synopsis-coaching-desktop/Release/net10.0-windows10.0.19041.0/win-x64/`.
- The focused test build reports one existing xUnit2031 warning in unrelated `LocalStoryboardTests.cs:75`.

Evidence: `final/before.png`, `final/after-640.png`, `final/after-360.png`, `final/review-360.png`, `final/browser-results.json`, and `story-regression/results.json` beneath the evidence directory. Screenshots were visually reviewed with visible browser scrollbars enabled.

Reproduce from the repository root:

```powershell
New-Item -ItemType Directory -Force artifacts/uat/synopsis-coaching/final | Out-Null
$env:WRITERAPP_P08_EVIDENCE = Join-Path $PWD 'artifacts/uat/synopsis-coaching/final'
dotnet test Tests/WriterApp.Tests/WriterApp.Tests.csproj --filter 'FullyQualifiedName~LocalSynopsisPanelTests|FullyQualifiedName~LocalSynopsisCoachingTests|FullyQualifiedName~WebCheckedSynopsisTests' -p:BaseOutputPath=artifacts/synopsis-coaching-bin/
# Set PLAYWRIGHT_MODULE to the installed Playwright package path.
node Tests/synopsis-coaching-layout.mjs
```

The browser harness optionally reads baseline compiled HTML and CSS preserved under `baseline/` during this session. Those ignored files are needed only for its before capture; regression checks run without them. Native restart/reopen of the rebuilt Windows app and authenticated live AI remain unverified. The currently running/installed app was not replaced. This report establishes the production component layout and existing interaction invariants, not packaged/native acceptance or live-provider quality.
