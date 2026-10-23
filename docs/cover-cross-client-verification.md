# Cover generation: desktop and web verification — 2026-10-15

Windows desktop and the rebuilt local web client both completed live OpenAI cover generation, each returning four visible concepts. The original saved covers and manuscript contents were left unchanged. The corrected desktop application and normal development backend are running from isolated build outputs.

## Defects found and fixed

1. **A visible description could be missing from the submitted brief.** The shared `CoverBrief` updated its description only on `change`/blur and rebuilt other field changes from its last parent-supplied `Value`. A quick style change could overwrite a pending description. The initial web lighthouse request produced generic fantasy art. A local synthetic provider then confirmed that its prompt omitted `User description` despite the visible textarea containing the lighthouse text. The editor now commits description changes on `input` and combines consecutive events using its current local brief. This shared repair applies to web, desktop and Android.
2. **Web transport errors and cancellation were unhelpful.** Generation/edit preview requests now distinguish explicit Cancel from timeout, show actionable connection/invalid-response messages, retain existing previews, and do not automatically replay generation. The web cover client has its own three-minute HTTP budget, retaining the existing account/error and manuscript request handlers. Busy generation and brief-switch buttons are disabled, and progress/error feedback has status/alert semantics.
3. **Invalid asset mappings could discard the preceding preview.** All returned images and asset identities are now validated before replacing existing concepts or an edit review.
4. **The existing publishing-cover test harness lacked newly required host services.** Registered the standard unavailable share/print implementations in that test so its offline cover, export and sign-out assertions execute again. Production publishing behavior was unchanged.

## Acceptance evidence

| Surface | Flow and result | Evidence |
|---|---|---|
| Native Windows desktop | Opened existing signed-in synchronized manuscript, entered `Unanswered love`, generated four concepts through the actual MAUI WebView. Backend returned HTTP 200 in 57.3 seconds. No project-cover save. | Earlier normal backend log: `artifacts/cover-connection/backend-fixed.log`; `artifacts/cover-cross-client/desktop-concepts.png` |
| Native Windows restart | Closed the first verification instance cleanly, launched the build with the shared input fix, reopened the same manuscript and Cover studio. All four cached concepts survived and no saved cover was present. | `artifacts/cover-cross-client/desktop-concepts-reopened.png` |
| Web brief reproduction | Used the actual browser form against an isolated development author/database and a local synthetic image provider. Before repair the visible description was omitted; after the identical fill/style/generate sequence it was included verbatim. No live provider calls for this comparison. | `provider-request-before.json`, `provider-request-after.json` under `artifacts/cover-cross-client` |
| Corrected web live generation | Lighthouse description, Literary, Hopeful, Painted, Warm. HTTP 200 in 59.8 seconds. Four 1024×1024 lighthouse concepts loaded; second-concept selection was verified without saving a cover. | `web-final-live.log`, `web-acceptance-final.json`, `web-concepts-final.png` under `artifacts/cover-cross-client` |
| Failure and recovery | Header/body interruption, timeout, Cancel, invalid assets, and deliberate subsequent generation preserve prior previews and do not trigger an automatic retry. | `Tests/WriterApp.Tests/CoverGenerationWebTests.cs`; `artifacts/cover-cross-client/final-tests.log` |
| Input integrity | Consecutive description/style/genre/mood/palette events preserve the accumulated brief before a parent parameter refresh. | `Tests/WriterApp.Tests/CoverBriefTests.cs`; same final test log |

Final focused regression run: **85 passed**, including cover generation, device panels, edits, explicit selection/save/recovery, ownership/source checks, offline publishing/sign-out, missing storage before provider dispatch, and the new failure/input cases. This is not a full-solution test result. An earlier exploratory broad name-match run stalled and was stopped; the stale publishing-test service registration it exposed was corrected and is covered by the passing final run.

Backend, Windows desktop and Android builds passed with zero warnings/errors. Test compilation reports the existing NuGet-audit/analyzer warnings. No deployed-production or physical-device acceptance is claimed.

## Running and built artifacts

- Normal backend: PID **53388**, `http://localhost:5387`, `artifacts/cover-cross-client/backend-input-fixed/BlazorApp.dll`. Existing configured SQL database and native authentication remain in use. SHA-256 `697389168C3DE7260C485F09705796ADD120FBD95E2C57615D1E886221B4F1DF`.
- Updated Windows app: PID **47084**, `artifacts/cover-cross-client/desktop-fixed/WriterApp.Desktop.exe`. Its assembly SHA-256 is `F5C5852382994AE0942E8F6FB164C9FD690B75882ACB5ACF0CBB26A37FA1DEC8`. The generated concepts are left available for review in the normal local cache.
- Android APK with the shared brief repair: `artifacts/cover-cross-client/android-fixed/com.prosa.writer.android-Signed.apk`, SHA-256 `D84CC5B5381FB531676D6F5ACE400AF7DDA4A9C230C033564548D7FD04D6B414`. Installation was attempted, but `adb devices` showed no connected emulator/device. The previously installed Android build remains the earlier verified version until this APK is installed; the newly found input fix has not been rechecked in an installed Android runtime.
- The isolated web host on port 5416 and synthetic provider on port 15416 were stopped after acceptance. The synthetic author, generated verification assets and database remain only under the ignored `artifacts/cover-cross-client` directory. No customer entitlement was changed.

Check current PID/path before stopping an owned development process; these runtime IDs are a point-in-time handoff. Isolated output leaves normal build directories available.
