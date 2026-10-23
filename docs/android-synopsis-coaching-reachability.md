# Android synopsis coaching reachability — 2026-10-12

Profile: installed Development Debug APK, Pixel 7 API 34 emulator (`emulator-5554`), existing disposable document. Flow: Writing tools → Story → Synopsis → swipe the coaching pane to its run button. No coaching request was submitted.

| ID | Severity | Root cause | Acceptance | Result |
|---|---|---|---|---|
| SYN-ANDROID-01 | P1 | Device host's `.desktop-context-ai` shrinks and clips the synopsis form, hiding overflow from the surrounding scroll pane | A touch swipe exposes the run button within the viewport; the button receives input; authored data remains unchanged | Verified in rebuilt installed Android APK |

Before: `.rp-trailing-body` had a 343px viewport and 343px scroll height. Its AI wrapper shrank to 319px despite containing a 726px form, with `overflow:hidden`. The run button was below the viewport and setting the pane's scroll position had no effect. This explains why a visible scrollbar did not provide access to the action. The earlier component-only layout checks omitted the device host wrapper's scoped styles.

The device host now assigns `is-synopsis-coaching` for the synopsis mode. Its scoped CSS prevents wrapper shrinking and permits overflow to participate in the existing pane's scrolling. Other AI modes keep their existing layout rules.

After: the same 343px pane reports 750px scroll content; the wrapper retains its full 726px height. CDP touch swipes scroll the pane by 407px. The `Improve synopsis field` button is visible at y=703–745 inside the pane and the 867px WebView viewport; center hit testing confirms it receives input. The manuscript HTML hash and all 11 local document file hashes match the baseline recorded before the preserve-data APK update.

Validation:

- Android Debug build succeeded with zero warnings/errors and embedded assemblies.
- Five production-CSS browser fixture scenarios passed: 390×844, 412×867, 844×390, 900×900, 1440×900. Each reproduces the original clipping, verifies run and review action reachability, keyboard scrolling and absence of horizontal overflow.
- Six existing panel-resize unit checks passed.
- Native captures were visually reviewed. Evidence: `artifacts/synopsis-android/native/{baseline.json,results.json,form-top.png,run-reachable.png}` and `artifacts/synopsis-android/browser/`.

Reproduce:

```powershell
# Set PLAYWRIGHT_MODULE if Playwright is outside the normal Node module path.
node Tests/synopsis-coaching-reachability.mjs
# With the Development Debug app running in an existing document:
node Tests/synopsis-coaching-android.mjs baseline
dotnet build WriterApp.Android/WriterApp.Android.csproj -c Debug -p:EmbedAssembliesIntoApk=true -p:BaseOutputPath=artifacts/synopsis-reachability/
# Install the APK with adb install --no-incremental -r, then launch it.
node Tests/synopsis-coaching-android.mjs verify
```

APK: `WriterApp.Android/artifacts/synopsis-reachability/Debug/net10.0-android/com.prosa.writer.android-Signed.apk`; installed in the emulator. Physical-phone installation and live coaching provider responses remain unverified.
