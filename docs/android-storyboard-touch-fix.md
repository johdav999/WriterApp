# Android storyboard touch dragging — 2026-10-14

Product profile: Prosa MAUI Debug, author role, Pixel 7 API 34 emulator
`emulator-5554`, 412 × 839 CSS viewport. Existing manuscript/project data was
preserved. Mutation checks used a new local project, Android Storyboard touch UAT.

| ID | Severity | Defect | Acceptance | Status |
|---|---|---|---|---|
| AND-SB-01 | P1 | Dragging a scene grip immediately cancels on Android | Reorder, move to empty/occupied chapters, cancel safely, retain saved order after restart | Verified in the rebuilt installed Android app |

## Root cause and fix

The device storyboard already enabled the shared pointer drag controller, and
the scene grip already had `touch-action:none`. HTML dragging was disabled as
intended. The failure was in the pointer-capture lifecycle.

Android implicitly captures the touched grip or its dots. After movement passes
the drag threshold, `storyboard.js` explicitly transfers capture to the board.
The grip then emits `lostpointercapture`, which bubbles to the board's listener.
That listener previously canceled every matching pointer ID, including this
normal transfer. The live baseline recorded:

1. Grip dots receive capture.
2. Dots lose capture while the drag is active.
3. Board receives capture after the gesture has already been cleared.
4. Board loses capture; no placement callback is reached.

The lost-capture listener now cancels only when the event originated on the board
itself. Descendant capture handoff leaves the gesture active. Existing movement
threshold, destination hit testing, indicators, horizontal edge scrolling,
single-flight saving, and move validation/persistence remain in use. Genuine
board capture loss, pointer cancellation, Escape, blur, and disposal still cancel.

Use the dotted grip to touch-drag a scene. Ordinary swipes outside the grip retain
the browser's normal scrolling behavior.

## Verification

- Added a regression for Android's bubbling capture transfer. It failed against
  the original controller and passes after the fix. Actual board capture loss
  still cancels. All 16 storyboard/outline JavaScript tests pass.
- Android standalone Debug APK built with zero warnings and errors; installed
  using `adb install --no-incremental -r`. The installed `storyboard.js` matches
  the repository source exactly.
- Real WebView touch input reordered two synthetic scenes, moved a scene before
  a scene in another chapter, and canceled a gesture without saving. A native
  `adb shell input swipe` from the grip moved a scene into an empty chapter.
- The final order survived route reopening and an Android process restart.
  All synthetic section IDs and complete page objects remain unchanged.
- Every pre-existing document file SHA-256 matches the baseline. The original
  storyboard was restored after testing. No existing scenes were rearranged.
- Captured and visually inspected the synthetic board after its scenes moved.
  Evidence is under `artifacts/android-storyboard-touch/`: `baseline.json`,
  `results.json`, `before.png`, `fixed-test-project.png`, `original-restored.png`.

The native check is `Tests/android-storyboard-touch.mjs`. Run `baseline` against
the affected build on its storyboard before installing, then `verify` after the
APK update. Verification creates and retains one local test project. After
restarting the Android app, run `restart` to verify persisted order and return to
the original storyboard. Tests do not trigger AI actions or enable cloud sync.
Physical-device, packaged Release, and live cloud-sync acceptance are separate.

```powershell
node --test Tests/storyboard-pointer-drag.test.mjs Tests/outline-drag.test.mjs
node Tests/android-storyboard-touch.mjs baseline
dotnet build WriterApp.Android/WriterApp.Android.csproj -c Debug -p:EmbedAssembliesIntoApk=true -p:BaseOutputPath=artifacts/storyboard-touch/android-build/ --no-restore
# Install the signed APK from the Android project's output above, and launch it.
node Tests/android-storyboard-touch.mjs verify
# Restart the app, then check persisted order.
node Tests/android-storyboard-touch.mjs restart
```

Installed APK SHA-256:
`339a43e10bb81c4b2bf3eea3fec859ed136c46d79501c57b50d9fd0707376ae2`.
