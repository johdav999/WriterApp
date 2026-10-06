# Storyboard subplot continuity repair — 2026-10-04

Scope: the editor's desktop Storyboard coaching panel and the shared desktop/web Storyboard Insights action. Changes remain in the existing checkout alongside other ongoing work.

| Issue | Cause | Repair | Evidence |
|---|---|---|---|
| SC-01: uninformative empty results | The desktop editor serialized a flat, unordered node list with nested PascalCase cards and JSON-encoded subplot tags. The provider relied primarily on tags and had no explicit insufficient-context outcome. The checked web context also encoded tags as strings and sorted chapters without respecting part order. | One shared ordered chapter/scene context, real tag arrays, saved summaries and key events; provider distinguishes issues, no supported issues, and insufficient context. Summaries may support inferred threads; absent tags alone are not an error. | Desktop request and checked server regressions in `SubplotContinuityTests`, `LocalStoryboardTests`, and `WebCheckedSourceServerTests`. |
| SC-02: raw JSON comparison and generic controls | Storyboard analysis fell through to the manuscript text comparison. The request panel had almost no scoped styling. | Dedicated responsive report, coverage counts, grouped subplot findings, readable issue labels, affected-scene references, recommendation cards, source disclosure, styled focus notes and primary action. Raw manuscript comparisons and cloud-version identifiers are removed from this panel. | Actual `LocalAiPanel.RunAi` and shared preview rendering with synthetic provider responses; headless Edge captures at 320, 420 and 729 pixels. |
| SC-03: missing board action and misleading empty/error handling | The shared board had a continuity handler but no button. Its parser collapsed malformed and valid empty reports into the same error. | Restore the button and use the same report component for empty, insufficient, populated and malformed results. Clear previous report at the start of another check. | Shared Insights action regression and existing desktop storyboard tests. |

Verification: 54 focused .NET tests; 9 responsive checks using real server-rendered Razor markup and compiled scoped CSS in headless Edge. Rendered HTML, screenshots, and layout results are in `artifacts/uat/subplot-continuity/`. These checks use synthetic provider output and do not establish live provider quality or native WebView acceptance.

The shared UI, device library, web client and server compile in isolated output at `artifacts/subplot-fix/bin/`. Existing running desktop/server processes were left open. The running app must load a rebuilt desktop binary to show the changes; the configured backend must receive the server prompt/context changes. Native relaunch and deployed provider validation remain open.

Additional manuscript excerpts are not included. Automatic approval review rejected extending the provider payload with saved manuscript writing without explicit approval; the optional approval request is pending. The completed repair operates on storyboard planning metadata already used by this feature.

Reproduce browser verification after exporting the rendering fixtures with `WRITERAPP_SUBPLOT_EVIDENCE` set to `artifacts/uat/subplot-continuity`: run `node Tests/subplot-panel-layout.mjs` with `PLAYWRIGHT_MODULE` pointing to the installed Playwright package.
