# Android acceptance — 2026-10-11

Prosa Android native implementation is ready for emulator/internal review. Production identity, stable release credentials, live services and physical-device acceptance remain open. Implementation/setup: [android-development.md](android-development.md).

## Sign-in configuration correction — 2026-10-11

The initial emulator APK intentionally embedded empty `native-auth.json` settings
and `ProsaAndroidSignatureHash=unconfigured`. Consequently the account service
reported unconfigured identity and correctly disabled the browser sign-in button.
The native sign-in adapter itself was registered.

The development installation now imports ignored public local settings bound to
its actual APK signer and the existing Prosa native public client. Debug rebuilt
with zero warnings/errors and was installed in place after background saving.
The same author document route restored. Mounted WebView verification confirms
`signInButtonPresent=true`, `signInDisabled=false`, `missingConfiguration=false`.
Evidence: `signin-before.json`, `signin-after.json`, `signin-state.png` and
`artifacts/android-signin-build.log`.

The exact tenant change is prepared in `identity-registration-change.json`:
append the certificate-bound Android callback to the existing native client's
public redirects while retaining its Windows loopback callback and permissions.
Automatic approval review initially required explicit user authorization for
the shared registration change. The user subsequently approved the exact callback;
`-RegisterCallback` applied it and Graph read-back confirmed registration and
preservation of existing delegated permissions. The Windows loopback redirect
remains present. Android's package resolver routes the exact callback to MSAL's
BrowserTabActivity. Clicking the enabled native button opens Chrome's first-run
screen; the author must finish browser setup and enter their own Prosa credentials.
The emulator Development backend port is forwarded to the PC using adb reverse.
The helper performs read-only preflight/local setup by default; `-RegisterCallback`
is the explicit cloud mutation step. No customer credentials were entered or printed.

## Session profile

- Base revision: `4ed39d54b84348c247def4a07fad3f017c300e6a`, with the Android implementation in the working tree.
- Host: Windows, .NET SDK 10.0.400, MAUI 10.0.20, Android SDK 36, JDK 21.
- Runtime: existing Pixel 7 API 34 AVD, x64, launched headless/read-only with no snapshot save. Test app package `com.prosa.writer.android`, version 0.1.0 / code 1, Development, native identity unconfigured.
- Role: local synthetic author; no account or provider calls. Created synthetic documents only. Existing desktop/backend processes and author storage were preserved.
- Phone viewport: approximately 412 × 867 CSS pixels after native safe-area handling. Tablet layout: temporary emulator display 1920 × 1200 at density 240, approximately 1280 × 744 CSS pixels. Display overrides restored afterward. This is a layout simulation, not a second physical device.
- Evidence directory: `artifacts/android-uat/`; smoke runner: `Tests/android-native-smoke.mjs`.

## Executed flows

| Flow | Expected and observed | Result / evidence |
|---|---|---|
| Install / offline launch | Debug x64 APK installs through adb. Local library opens without a backend or identity registration. | Pass; build log `artifacts/android-build-final.log` |
| Create / write / restart | Type `Android emulator acceptance — Räksmörgås 日本語. Local writing survives background and restart.`; background, force-stop, relaunch. Exact text and document route restore. | Pass; `write-restart.json`, `writing-restarted.png` |
| Back / keyboard | First Back closes the active soft keyboard and keeps the document. Subsequent Back flushes writing and returns to the library. | Pass; `keyboard.png`, `keyboard-dismissed.png`, `back-library.png` |
| Native file save | Export HTML to Android Downloads through the system create-document picker; bytes include the synthetic Unicode manuscript. | Pass; `native-export.html`, `native-file-saved.png` |
| Native import | Choose the saved HTML through Android's document provider. A new local document opens with the exported manuscript text. Export title/section headings are also imported as content. | Pass; `import-picker.png`; observed new local document |
| System PDF | Select PDF, choose Save as PDF in Android printing, choose Downloads and save. App reports terminal print completion. Title page and section page render correctly; text extraction includes Swedish and Japanese characters. | Pass; `native-print.pdf`, `native-print-utf8.txt`, `pdf-page2.png` |
| Native sharing | Share HTML opens Android's chooser with the export attached. Back dismisses the chooser; no recipient selected and no send performed. | Pass; `share-sheet.png`, `native-dialog.xml` |
| Phone / tablet layout | Writing uses phone width with a horizontal formatting strip; system bars do not cover controls. Context tools are reachable beneath writing. Tablet retains navigation and side-by-side context. No document-root horizontal overflow observed. | Pass; `phone-writing.png`, `context-panel.png`, `tablet-writing.png` |
| Native audio | Debug instrumentation records through AudioRecord, produces valid mono 16 kHz PCM16 WAV, then stops. Installed offline English and Swedish voices are detected. Emulator sample is silent/synthetic. | Pass; 13,356 WAV bytes, 0.416 seconds in initial probe; no transcription/provider requests |
| Host / regression tests | Account selection, callbacks, expiry, cancellation, backend isolation, sign-out failure/race, WAV bounds, iOS identity and local publishing regressions. | 57 passed in final targeted run (19 Android cases); `testresults/android-final.trx`. Earlier broader save/recovery run: 210 passed, `artifacts/android-tests.log` |
| Packaging validation | Configuration, version bounds, callback scope, unsigned-bundle rejection and expected APK signer validation. | 7 Python tests passed |
| Release build | ARM64 Release AAB builds with assembly trimming and zero warnings/errors; jarsigner verifies its development-key signature. Debug instrumentation absent from Release manifest. | Pass; `artifacts/android-release-build.log`; this is not production signing or store acceptance |

PDF extraction may reorder font runs for mixed Latin/CJK text unless the reader uses layout-aware extraction. The rendered page and character preservation were verified. Print destination, paper options and filesystem permissions belong to Android and its chosen provider.

## Issue ledger

| ID | Severity | Finding | Fix and verification | Status |
|---|---|---|---|---|
| AND-001 | P1 | Restored navigation could detach the library while its JavaScript drop handler initialized. | Ignore detached/null roots; repeated writing/process restart succeeds without the startup exception. | Closed |
| AND-002 | P1 | Initial canvas PDF was rasterized and text could not be searched. | Replaced with retained offline WebView + native print spooler; saved PDF has searchable Unicode text and correct rendered pages. Superseded `native-export.pdf` is not final PDF evidence. | Closed |
| AND-003 | P2 | Desktop rail consumed phone writing width; context drawer was hidden by shared responsive CSS. | Android-scoped layout, horizontal navigation/formatting strip and accessible stacked context; phone/tablet captures inspected. | Closed |
| AND-004 | P2 | .NET 10 edge-to-edge layout covered header controls with Android status bars. | ContentPage explicitly uses `SafeAreaEdges=All`; keyboard and system-bar captures pass. | Closed |
| AND-005 | P1 | Concurrent sign-out could wait behind sign-in and lose its in-memory signed-out flag if both cache removals failed. | Reset flag under the identity gate; focused concurrency regression proves silent restoration stays disabled. | Closed |

## Remaining acceptance

1. Configure the approved Android public-client registration, API authorization and installed certificate callback. Verify system-browser sign-in/cancellation, silent refresh/relaunch, account switching, sign-out, expired/denied access and SecureStorage on the real installation. Portable tests establish selection contracts; no live Entra sign-in was performed.
2. Configure protected CI environment variables and stable signing secrets. Exercise direct APK or Play AAB mode against the real signer. GitHub workflows were added locally; no remote workflow, store upload or publication was performed. Play re-signing uses its app signing certificate for callbacks, distinct from the upload key.
3. Install on representative physical phones/tablets, including minimum API 26 and a current target-platform device. Check keyboard/cutouts, rotation, low-memory termination, storage-provider cancellation/failure, upgrades preserving data, native sign-in return, microphone denial/interruption/background, English/Swedish audible playback and long PDF pagination.
4. Verify reachable HTTPS backend, live AI/transcription and cloud sync with approved test credentials. Do not treat emulator audio or local synthetic identity tests as live provider acceptance.

## Reproduce

Build/install using the Android development guide. Create a blank synthetic document, then run smoke modes `write`, `open`, `layout`, `keyboard`, `context`, `publish`, `html`, `pdf`, `share` or `import` as appropriate. Native dialogs remain user/provider-controlled; `native-inspect` records labels/bounds, and `save-dialog` confirms an enabled SAVE button. The destructive `write` mode refuses nonempty content without the synthetic marker. Never run lifecycle replacement checks against author-owned writing.

Use a fresh output directory for release packaging. Keep secrets out of screenshots/logs and do not upload the whole build directory. The read-only emulator session's test data is disposable; use a normal Android Studio emulator session for personal interactive testing.
