# Responsive editor panels — 2026-10-11

## Product profile and behavior

Prosa web/WASM and the shared MAUI device editor; author role. Baseline repository
revision `4ed39d5` with existing Android/sync changes retained. The reported phone
failure was an unconditional `display:none` below 980px, overriding the button's
collapsed state.

The shared editor now owns Projects & outline and Writing tools presentation.
At widths below 1200px, the editor starts with both drawers closed; only one can
open. Projects & outline slides over the left side. Writing tools uses a full
screen sheet up to 600px and a right overlay on larger compact windows. At 1200px
and above, both panels can dock, collapse and resize while reserving manuscript
space. Docked visibility uses `prosa.editor-panels.v1`; widths use the existing
panel resize preferences, including `prosa.panel-width.editor-left`.

`EditorWorkspace` retains editor/panel instances when visibility changes.
`editor-panels.js` coordinates transient drawer state, host focus/context callbacks,
ARIA, background inertness, focus trapping/restoration, text selection and the
compact account menu. Host code retains document saves, selected tools and
navigation. Page changes close compact navigation after the successful selection.
Brainstorm continues using its dedicated pane controls. Embedded navigator control
IDs are scoped in the left panel so both navigator copies can coexist.

Escape, the close/back buttons, and the tablet backdrop dismiss a drawer. Android
Back keeps the existing keyboard-first behavior, then dismisses an open drawer,
then follows the existing save/navigation lifecycle. Opening a panel explicitly
exits focus mode. Leaving focus restores docked preferences. Compact interactions
do not overwrite those preferences.

## Issue ledger and acceptance evidence

| ID | Severity | Flow / issue | Acceptance | Result |
|---|---|---|---|---|
| PAN-01 | P1 | Phone tools button has no visible effect | Open tools from writing and operate a tool tab | Verified in production browser controller, rebuilt web client and Android emulator |
| PAN-02 | P2 | Navigation consumes phone space / outline is inaccessible | Start closed, open left drawer, choose a section and return to writing | Verified in browser fixture and rebuilt web client; native outline verified |
| PAN-03 | P2 | Panel dismissal loses place or focus | Preserve manuscript selection/scroll, draft input and selected tool; trap focus; Escape/Back return to writing | Browser selection/scroll/input checks pass; native Back and tool selection pass |
| PAN-04 | P2 | Compact state changes desktop layout preferences | Resize between phone/tablet/desktop and reload; restore docked visibility/width | Verified in browser fixture and rebuilt web client |
| PAN-05 | P2 | Two mounted navigators reuse control IDs | Mount both desktop navigators with distinct IDs and label targets | Verified in rebuilt web client |

Evidence is under `artifacts/responsive-panels/` (ignored session artifacts):

- `browser/results.json`: six production-controller scenarios at 390×844,
  900×900 and 1600×1000; no browser errors. Screenshots of tools, outline and docks.
- `application/results.json`: four real Blazor-client scenarios, using a SQLite
  backup of the existing disposable LocalDev fixture at localhost:5403. Includes
  Notes, focus, reload persistence, both navigator IDs, Brainstorm enter/leave,
  successful section navigation, and unchanged manuscript HTML. No browser errors.
- `android/results.json`: installed standalone Debug APK on Pixel 7 API 34,
  `emulator-5554`. Tools and Notes open; native Back closes the drawer without
  leaving the document; projects/outline and focus-to-tools work. All six document
  file SHA-256 values and manuscript HTML hash match the pre-update baseline.
  `android/baseline.json` contains the comparison receipt; native screen captures
  are in the same directory. Installation used `adb install -r` to preserve data.

## Builds and regressions

Web/server, Windows desktop and Android builds pass. Android uses
`-p:EmbedAssembliesIntoApk=true`; omitting that property creates a fast-deployment
Debug APK that cannot be installed by itself. The initial packaging mistake was
corrected; the emulator runs the standalone build. Cached NuGet vulnerability
metadata reports NU1900 because nuget.org is unavailable. These are build warnings,
not compiler failures.

The final focused .NET run passes 57 tests (layout, workspace, Brainstorm and local
storyboard), and all six panel-resize unit checks pass. The extended suite recorded
2679 passes and two failures before the owned run was stopped; it is **not** a
complete green suite. Separately rerunning those failures produced:

- `DeviceStoryTabRegressionTests` standalone Synopsis case: expects the old
  no-project explanation, but the existing standalone synopsis panel now renders
  authored synopsis/notes. The panel implementation was not changed by this work.
- `RemoteCoverPublishingPageTests`: its service fixture does not register
  `IDeviceFileShare`, required by the pre-existing Android publishing changes.

Neither failing surface was edited to alter its behavior in this panel task.
No provider calls, account changes or authored-document mutations were made during
panel verification. The owned web acceptance host is stopped after verification.

## Reproduce / use the builds

```powershell
# Supply PLAYWRIGHT_MODULE if Playwright is not on the normal Node module path.
node --test Tests/panel-resize.test.mjs
node Tests/responsive-panels.mjs

# Actual web client requires the documented disposable fixture and localhost host.
node Tests/responsive-panels-app.mjs

# Native baseline before a preserve-data APK update; verify after launching it.
node Tests/responsive-panels-android.mjs baseline
node Tests/responsive-panels-android.mjs verify
```

Final outputs:

- Web: `artifacts/responsive-panels/final-web-build/Debug/net10.0/`.
- Windows: `WriterApp.Desktop/artifacts/responsive-panels/desktop-build/Debug/net10.0-windows10.0.19041.0/win-x64/WriterApp.Desktop.exe`.
- Android: `WriterApp.Android/artifacts/responsive-panels/android-build/Debug/net10.0-android/com.prosa.writer.android-Signed.apk` (also installed in the emulator).

Physical Android/iOS, screen-reader interaction and native Windows interactive
acceptance were not exercised. Emulator and browser checks do not establish those
device-specific acceptance gates.
