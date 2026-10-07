# Storyboard results and panel widths — 2026-10-07

The shared Storyboard Insights component now renders subplot continuity reports and POV balance findings below the four AI command buttons, alongside the existing next-scene and missing-scene responses. Previously the first two reports appeared in the Insights section above the commands.

Both desktop and web Storyboard hosts now provide a horizontal resize divider before Storyboard Insights and Scene Detail. Drag left to widen the adjacent panel, or focus its divider and use left/right arrows (Shift for larger steps), Home/End, or double-click to reset. Escape cancels a drag. Width preferences use separate browser-storage keys and survive collapse/expansion. Storage restrictions do not prevent resizing. Widths are constrained to reserve board space, and dividers are hidden in the existing stacked layout at viewport widths of 1200px and below.

| ID | Flow | Acceptance | Evidence / result |
|---|---|---|---|
| SB-PANELS-01 | Run an Insights AI command | AI response follows every command button | Actual shared component render with fake subplot and POV responses passed in `LocalStoryboardTests` |
| SB-PANELS-02 | Resize Insights and Scene Detail | Drag/keyboard resize works; reset/cancel, width storage, collapse/expansion, window resize, and oversized saved widths preserve usable panels | Production CSS/JS browser fixture passed on both hosts; native and authenticated web acceptance pending |

Validation:

- `Tests/storyboard-layout.mjs`: 42 headless Edge layout cases, including desktop/web at seven viewport sizes with selection, errors/bulk actions, and collapsed Insights. Wide-screen cases exercise real pointer and keyboard interactions using the production resize module. Additional checks cover storage restoration, collapse/expansion, responsive transitions, and oversized saved widths. Browser requests are fulfilled by a local fixture; no backend or AI provider is contacted.
- Focused .NET run: 23 passed across `LocalStoryboardTests` and `LocalStoryboardPanelTests`, with no failures or skips. The existing xUnit2031 warning is outside this change.
- Desktop and web builds passed with zero warnings/errors. Build outputs are isolated from running processes. The first .NET test attempt encountered a server DLL lock; rerunning with isolated `BaseOutputPath` passed.
- `git diff --check` passed.
- Evidence: `artifacts/uat/storyboard-layout/results.json`, `storyboard-panels.trx`, `desktop-resized.png`, and `web-resized.png`. The desktop resize screenshot was visually inspected.

Run from the repository root:

```powershell
$env:PLAYWRIGHT_MODULE = 'C:\Users\Johan\.cache\codex-runtimes\codex-primary-runtime\dependencies\node\node_modules\playwright'
node Tests/storyboard-layout.mjs
dotnet test Tests/WriterApp.Tests/WriterApp.Tests.csproj --no-restore -p:BaseOutputPath=C:/Users/Johan/source/repos/WriterApp/artifacts/storyboard-panels/build/ --filter 'FullyQualifiedName~LocalStoryboardTests|FullyQualifiedName~LocalStoryboardPanelTests'
dotnet build WriterApp.Desktop/WriterApp.Desktop.csproj --no-restore -o WriterApp.Desktop/bin/storyboard-panel-validation -v minimal
dotnet build WriterApp.Client/WriterApp.Client.csproj --no-restore -p:BaseOutputPath=bin/storyboard-panel-validation/ -v minimal
```

The running desktop app uses its prior Debug build and has not been restarted. Native WebView interaction and authenticated browser acceptance have not been established by these checks.
