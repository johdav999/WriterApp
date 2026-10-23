# Android writing-tools scrolling — 2026-10-14

Profile: Prosa MAUI Debug app, author role, existing local manuscript, Pixel 7 API
34 (`emulator-5554`), 412 × 839 CSS viewport. Scope: Style & quality Coach,
Writing tools, Consistency Coach, panel dismissal/reopening, and document integrity.
Baseline checkout: `4ed39d5` plus the existing uncommitted device/editor work.

| ID | Severity | Defect | Acceptance | Result |
|---|---|---|---|---|
| AND-SCROLL-01 | P1 | Phone writing tools clip overflowing content and ignore swipes | Native swipes scroll both directions and reach the bottom; Back remains visible; tool switching/reopening preserves writing | Verified in the rebuilt, installed Android app |

The legacy `max-width:980px` stacked layout sets `.prosa-editor .rp-scroll` to
`overflow:visible`. The responsive drawer instead constrains the flex layout to
the viewport, with `.rp-shell` retaining `overflow:hidden`. In the reported
installed process, `.rp-scroll` was 758 px high with 1,787 px of content. Its
content overflow was clipped inside `.rp-shell`, and neither outer scrollable
container had a scroll range. A native upward swipe left scrollTop at zero.

The shared compact-drawer CSS now explicitly restores vertical `auto` scrolling
and hides horizontal overflow on `.rp-scroll`. This applies to responsive editor
panels below 1200 px, including compact annotations. The existing flex constraints
keep the drawer heading and Back to writing control visible.

## Verification

- Standalone Debug Android build: zero warnings and errors. Installed with
  `adb install --no-incremental -r`, preserving application data, then relaunched.
- Native `adb shell input swipe` checks passed in both directions for Style &
  quality Coach, Writing tools, and Consistency Coach. Repeated native swipes
  reached each pane's bottom. Style & quality passed again after Android Back and
  reopening. Example scroll positions: Style & quality 0 → 840 → 35, bottom 1046.
- All saved document SHA-256 values, manuscript HTML hash, and document route
  match the pre-update baseline. Checks made no writing edits or AI requests.
- The installed WebView's shared CSS exactly matches the repository file:
  SHA-256 `81eafd6d1e77690b324128d527fd28635e01ab61fa4cd261fd9dc50ae57c97b5`.
- Eleven browser scenarios passed, including mouse-wheel scrolling in both
  directions at phone, tablet, desktop, and compact annotation layouts. The
  fixture now includes the production `.rp-shell > .rp-scroll > .rp-content`
  nesting; its earlier flat layout did not expose the clipping defect.

Evidence: `artifacts/android-tools-scroll/` contains `baseline.json`,
`results.json`, `installed-css.json`, native captures and `browser/results.json`.
Native device claims are limited to the installed emulator. Physical-device
gestures and live provider behavior were not exercised.

## Reproduce

Record `baseline` on the affected installed build and screen before updating:

```powershell
node Tests/android-tools-scroll.mjs baseline
dotnet build WriterApp.Android/WriterApp.Android.csproj -c Debug -p:EmbedAssembliesIntoApk=true -p:BaseOutputPath=artifacts/tools-scroll/android-build/ --no-restore
adb -s emulator-5554 install --no-incremental -r WriterApp.Android/artifacts/tools-scroll/android-build/Debug/net10.0-android/com.prosa.writer.android-Signed.apk
adb -s emulator-5554 shell monkey -p com.prosa.writer.android -c android.intent.category.LAUNCHER 1
node Tests/android-tools-scroll.mjs verify
# Set PLAYWRIGHT_MODULE when Playwright is outside the normal Node module path.
node Tests/responsive-panels.mjs
```

Installed APK SHA-256:
`269d6c30652f6493cf62e22bc773dd14db67969f8f3477396d12d497c443647f`.
