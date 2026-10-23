# Android inline annotations — 2026-10-12

Annotations now reserve a resizable pane below the manuscript on compact screens.
Both regions remain interactive. Select writing and choose **Add comment / task**;
the composer captures that passage. Tap a highlight to open its annotation, use
**Previous / Next**, **All**, or **New**, and resolve/reopen or relink a note while
selecting text above. **Tools** returns to the general tools drawer. Close restores
the normal editor controls. Drag the grip or use **More space / Less space**.
Desktop retains the docked tools presentation.

The shared controller removes modal inertness and focus trapping only for this
annotation presentation. It uses the visual viewport height when the keyboard is
open. The same planning component remains mounted, preserving unfinished drafts
and existing navigation/save guards. The editor JS interop now sends the annotation
array as one argument, including empty updates; previously params-array covariance
spread it into separate arguments and prevented highlights. Android legacy and
WebView key Back routes now use the existing keyboard → panel → save/navigation
callback, alongside the AndroidX dispatcher.

Profile: local Debug MAUI Android app, Pixel 7 API 34 emulator (`emulator-5554`),
author, freshly created **Android annotations UAT** project. No provider requests.
Build: `dotnet build WriterApp.Android/WriterApp.Android.csproj -c Debug -p:EmbedAssembliesIntoApk=true --no-restore`.
Install with `adb install -r` to retain app data. All document hashes immediately
before and after the final APK replacement match.

| ID | Flow and acceptance | Result |
|---|---|---|
| ANN-A01 | Select a unique passage, open composer, type with native keyboard; manuscript and composer stay visible and interactive | Emulator verified |
| ANN-A02 | Add comment and task; highlights appear immediately; navigate and resolve/reopen | Emulator verified |
| ANN-A03 | Drag panel grip and use resize buttons without covering the text | Emulator verified |
| ANN-A04 | Create scene note, select text above, link it without closing panel | Emulator verified |
| ANN-A05 | Back hides keyboard first, closes panel next; tapping highlight opens annotation while remaining in document | Emulator verified |
| ANN-A06 | Edit text with annotations visible, undo, then background-save and restart process; exact prose and three annotations persist | Emulator verified |

Evidence: `artifacts/android-annotations/results.json`, `comment-keyboard.png`,
`text-and-annotation.png`, `note-relinked.png`, `highlight-opens-panel.png`,
`final-text-and-comment.png`, and the before/after-final-install hash receipts.
Final Android build passes with zero warnings/errors. Twenty-five focused
annotation/planning/interop tests and seven shared panel-controller checks pass.

Repeat: create a fresh empty project named **Android annotations UAT**, open its
manuscript, run `node Tests/android-annotations.mjs seed`, then
`node Tests/android-annotations.mjs verify` and
`node Tests/android-annotations-restart.mjs`. Set `PROSA_ADB` and
`PROSA_ANDROID_SERIAL` for a different SDK/emulator. The tests refuse ordinary
manuscripts and seed only blank test writing. The verify run creates annotations
in this fixture, edits and undoes its prose, and tests native Back and keyboard.

Physical devices, predictive gesture Back, and live cloud synchronization were
not tested in this pass. Emulator acceptance does not establish those results.
