# Android cover generation repair — 2026-10-15

The subsequent [desktop/web verification](cover-cross-client-verification.md) found and fixed a shared brief-input defect and supersedes the running-backend path/PID below. The evidence below records the earlier Android acceptance.

The reported message, `unexpected end of stream on com.android.okhttp.Address@…`, is a failed Android HTTP connection before a complete response was received. At inspection, port forwarding was intact (`adb reverse tcp:5387 tcp:5387`) but the development backend had no listener. The original connection's exact shutdown cause was not retained in available logs.

Restarting the backend and retrying the original brief exposed a second, independently confirmed blocker: OpenAI returned four concepts, but SQL Server rejected retention with `Invalid object name 'CoverAssets'`. The three existing cover migrations were absent from the configured development database. Startup auto-migration is disabled. Other unrelated migration gaps were left untouched.

## Repairs

- Applied only `20261004072915_OwnedCoverAssetsSqlServer`, `20261004085422_OwnedCoverEditSavesSqlServer`, and `20261004090303_OwnedCoverEditProposalsSqlServer`. Reviewed SQL creates the three cover tables/indexes and records their migration IDs; it does not update or delete author data.
- Check required cover storage before sending a potentially chargeable generation request. Missing storage now returns 503 before provider dispatch.
- Translate cover transport failures during response headers, stream opening, and stream reading. Android's legacy `WebException` is handled alongside `HttpRequestException` and `IOException`; AI-access preflight failures receive equivalent guidance.
- Distinguish timeouts from explicit Cancel. Preserve concepts and the saved cover, and never automatically replay generation requests.

## Verification

| Issue | Acceptance | Evidence | Result |
|---|---|---|---|
| COV-001: raw Android transport message | Drop the native HTTP connection; show recovery guidance without leaking the okhttp address. Restore normal backend routing and reconnect successfully. | `artifacts/cover-connection/native-failure.json`; `Tests/android-cover-connection.mjs failure` | Passed on installed Pixel 7 API 34 emulator |
| COV-002: missing cover storage after provider dispatch | Missing `CoverAssets` prevents provider calls. Existing migrations install the required schema. | `CoverAssetMigrationTests.MissingCoverStorageBlocksBeforeChargeableProviderDispatch`; `artifacts/cover-connection/schema/cover-schema.sql`; `schema/apply.log` | Passed |
| COV-003: original generation flow | Generate the retained original brief through the installed WebView; four images load and concepts are cached without saving a project cover. | `native-generation.json`, `native-acceptance.json`, `backend-fixed.log` under `artifacts/cover-connection`; `artifacts/android-uat/cover-connection-concepts.png` | Passed with live OpenAI; HTTP 200, 63.5 seconds; four 1024×1024 PNG concepts |

Focused suite: **62 passed**, covering cover generation/edit lifecycle, interrupted headers/body, Android WebException, AI-access failure, timeout/cancel, cache retention and missing storage before dispatch. Backend and Android builds completed with zero errors or warnings. Test compilation also reports existing analyzer/NuGet audit warnings.

The first diagnostic provider request produced concepts that could not be retained because the table was missing. The successful request after repair retained four concepts. Neither request saved a project cover.

## Installed runtime

- Android APK: `artifacts/cover-connection/android/com.prosa.writer.android-Signed.apk`, SHA-256 `FC300B66F0D1AD5753DB259AA785E83C5CE1CEEB8F21E995C27879014CA7802F`. Installed in place, preserving app data and native identity selection.
- Backend: `artifacts/cover-connection/backend-fixed/BlazorApp.dll`, SHA-256 `DBAD148C7587AF67DEFE24E01439EB7CB2AA63CC1B817140959CE1F8B80FD6E8`. Started for this repair as PID **46348**, Development, port **5387**, using existing configured services. Isolated output keeps normal builds available. Check current PID/path before stopping this process to launch another backend on the same port.
- Emulator forwarding was restored to `tcp:5387 → tcp:5387` after the failure fixture. The fixture never records bearer tokens or request contents.

This verifies the installed emulator and live development generation flow. Physical-device and deployed production acceptance were not exercised.

For a repeat native check, open Cover studio and run `node Tests/android-cover-connection.mjs capture`, then `failure`. `generate` explicitly makes a live image-generation request and leaves results as concepts for review.
