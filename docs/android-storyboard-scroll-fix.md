# Android storyboard return scrolling — 2026-10-14

Product profile: Prosa MAUI Debug, author role, Pixel 7 API 34 emulator
`emulator-5554`, 412 × 839 CSS viewport. Existing local project and manuscript
files are preserved; this check makes no writing changes or AI requests.

| ID | Severity | Defect | Acceptance | Status |
|---|---|---|---|---|
| AND-SB-02 | P1 | Swipes inside a storyboard panel cannot return to the page heading after reaching the panel's upper edge | Native swipes hand off to the page in both directions across the stacked panels; empty and populated Scene Detail remain reachable | Verified in rebuilt, installed Android Debug app |

## Root cause

The shared storyboard stacks its board, Insights, and Scene Detail panels below
1200px, making `.content` the outer page scroller. The panels retain their own
bounded vertical scroll areas, with `overscroll-behavior-y: contain`. When an
overflowing panel reaches its upper or lower boundary, containment prevents the
gesture from scrolling the surrounding page. Swiping again inside that panel
continues to be trapped.

The installed baseline reproduced this using native Android input over Insights
at `scrollTop=0`: the page remained at `843.81px` before and after a downward
swipe. This distinguishes the problem from storyboard dragging and pointer
capture. The empty, non-overflowing Scene Detail area did allow page scrolling
in the baseline; the overflowing Insights area reproduced the trap.

## Fix

`WriterApp.Android/wwwroot/android.css` sets `overscroll-behavior-y: auto` for
the three storyboard scroll areas when their layout stacks at 1200px or below.
This lets vertical gestures continue on the outer page at either panel boundary.
The board retains horizontal scrolling and horizontal containment, and the scene
grip retains its existing touch-drag behavior. Desktop styles are unchanged.

## Regression and evidence

`Tests/android-storyboard-scroll.mjs` has two modes. Open an existing storyboard
with no scene selected, then run `baseline` on the affected APK. After building
and updating the installed APK, run `verify` on the same route. The test records
native WebView bounds, sends swipes through `adb shell input swipe`, and checks
actual scroll movement. JavaScript scroll positioning is only precondition setup.

```powershell
node Tests/android-storyboard-scroll.mjs baseline
dotnet build WriterApp.Android/WriterApp.Android.csproj -c Debug -p:EmbedAssembliesIntoApk=true -p:BaseOutputPath=artifacts/storyboard-scroll/android-build/ --no-restore
# Update the emulator with the resulting signed APK using adb install --no-incremental -r.
node Tests/android-storyboard-scroll.mjs verify
node --test Tests/storyboard-pointer-drag.test.mjs Tests/outline-drag.test.mjs
```

Evidence: `artifacts/android-storyboard-scroll/`, including `baseline.json`,
`results.json`, screenshots, and `build.log`. Verification compares the installed
Android stylesheet with repository source and checks all pre-existing document
file hashes before and after the gestures and scene selection.

Verified results:

- Standalone Debug APK build: zero warnings and errors; installed with
  `adb install --no-incremental -r`. Installed `android.css` matches source.
- Native return swipes move the page from `843.81px` to `483.81px` over Insights,
  where the baseline stayed at `843.81px`.
- All three panel boundaries pass in both directions. Empty Scene Detail and a
  populated scene inspector both permit returning up the page. Repeated native
  swipes reach the heading at page `scrollTop=0`; screenshots visually inspected.
- WebView can latch the gesture that reaches an inner edge to that panel. The
  regression allows that gesture plus one fresh swipe, recording each attempt.
  The board's lower boundary took two; all other boundary checks took one.
- All 23 existing document file SHA-256 hashes are unchanged after installation,
  scrolling, and scene selection. No existing scenes were moved or edited.
- All 16 existing storyboard/outline JavaScript drag checks pass.

APK SHA-256: `3154995AB89D253AB30E4EFFCACC373CE853442544306E6E822E1B671021F33D`.

Physical-device and packaged Release acceptance are separate from this emulator
check.
