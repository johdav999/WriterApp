# Android manuscript scrolling — 2026-10-14

## Cause

On phone widths (800px and below), `WriterApp.Android/wwwroot/android.css` expands
the editor and main writing column to content height for a continuous page layout.
The shared compact-panel rules in `WriterApp.UI.Shared/wwwroot/editor.css` set
`.app-shell:has(.editor-shell[data-responsive-panels]) .content` to
`overflow:hidden`. That selector outranked Android's `.app-shell .content`
`overflow:auto` even though android.css loads last.

In the installed Android WebView, `.content` was 787px high with 1136px of content.
Its computed overflow was hidden; the expanded `.editor-main` and paper viewport
had no scroll range of their own. Actual native swipes left scrollTop unchanged.
Programmatic scrolling could still move the hidden-overflow container, explaining
why the screen could already be partway down while the author's swipes did nothing.
The preceding header acceptance checked layout and navigation but did not exercise
manuscript swipes.

## Change

Android's phone stylesheet now explicitly makes `.content` scrollable for the
responsive writing workspace, with a selector that takes precedence over the
shared clipping rule. This preserves the continuous phone layout and allows
swipes starting over the manuscript to reach the header and bottom save/status
controls. Brainstorm panes and the open annotation split view retain their shared
scrolling containers. The change is limited to Android phone widths.

## Acceptance ledger

| ID | Severity | Flow | Acceptance | Status |
|---|---|---|---|---|
| SCROLL-001 | P1 | Write in Android project manuscript | Native swipes move the page both ways; top/bottom reachable; focus and panel transitions retain scrolling; writing unchanged | Verified in installed Android Debug WebView |

## Reproduce and verify

Use the installed Debug Android app on `emulator-5554`, with an existing project
manuscript open. The harness records route, editor HTML and all local document
hashes before reinstalling; it does not write manuscript text or call a provider.

```powershell
node Tests/android-editor-scroll.mjs baseline
# Rebuild and install the embedded-assemblies Debug APK with adb install -r.
node Tests/android-editor-scroll.mjs verify
```

Evidence is under `artifacts/android-editor-scroll/`; build log:
`artifacts/android-editor-scroll-build.log`. The tall-paper check temporarily
increases the mounted paper's minimum height to 2200px without changing content.

The standalone x64 Debug APK was rebuilt with embedded assemblies using the
isolated `artifacts/android-writing-header-build` output, installed in place and
relaunched. Build succeeded with zero errors and 10 NU1900 vulnerability-feed
warnings. Native `adb shell input swipe` checks passed in both directions for the
original writing screen, tall paper, focus mode and the screen after annotations.
The page end was reachable, modal tools still isolated writing, and annotations
retained the bounded interactive split view. Editor HTML and all 22 local document
files matched their pre-update hashes. Source syntax and focused whitespace checks
also passed. The initial panel-transition replay needed to wait for the asynchronous
focus-mode update before opening tools; the completed replay passed.

Screenshots: `writing-down.png`, `tall-paper-down.png`, `focus-down.png`,
`after-annotations-down.png` and `after.png`. Measured results: `results.json`.

Physical phone and iOS scrolling are outside this Android emulator check.
