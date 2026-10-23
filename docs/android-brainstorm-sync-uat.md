# Android Brainstorm cloud-sync recovery

Date: 2026-10-11. Surface: Debug Android MAUI Blazor Hybrid, Pixel 7 API 34 x64 emulator, Development backend. Build/run procedure: [Android development](android-development.md). Role: signed-in author with an existing local-only manuscript and a pending Brainstorm message.

| ID | Severity | Flow | Evidence | Acceptance | Status |
|---|---|---|---|---|---|
| AND-BRAIN-001 | P1 | Prepare Brainstorm after sign-in | User screenshot; `artifacts/android-uat/brainstorm-sync-baseline.json` | Cloud-sync opt-in is reachable in the conversation; saving precedes upload; successful sync retries preparation in the same conversation; no automatic send; pending message survives | Recovery UI verified in emulator; save/upload and preparation recovery verified by component tests |

## Cause and change

Sign-in does not enable cloud synchronization for every local manuscript. Server generation requires synchronized, checked story sources. The original Brainstorm screen disabled Send and referred to sync controls in the hidden manuscript status bar.

The device host now supplies the existing document sync controls directly in Brainstorm while the manuscript has no cloud copy or needs synchronization. Explicit enable/retry saves pending manuscript/planning writing and checkpoints any proposal before upload. A successful synchronization refreshes metadata and schedules context preparation for the current conversation. Preparation and synchronization never send the pending message. Save failures, entitlement/connection errors and conflicts remain visible and retain local writing.

An unlinked manuscript reports “Saved only on this device” instead of the sync engine's unrelated global completion message. Scoped recovery styles include nested device controls and override the workspace's fieldset reset.

## Recheck

1. Open an existing local-only manuscript while signed in. Open Brainstorm, then Conversation. Type a message and wait for its local checkpoint.
2. Run `node Tests/android-native-smoke.mjs brainstorm-sync-baseline` before updating the installed build.
3. Build and install the Debug x64 APK with `adb install --no-incremental -r`; preserve app data. Open Brainstorm and reopen the original saved conversation from History if necessary.
4. Run `node Tests/android-native-smoke.mjs brainstorm-sync`. The enable action must be enabled and visibly reachable. Compare the baseline and final `composerHash` and `composerLength`; no message content is written to the JSON evidence.
5. The author chooses **Enable cloud sync for this document/project**. After successful sync, context preparation should run automatically, **Context ready** should appear, and Send should enable for the retained message. Sending remains an explicit author action.

Set `PROSA_ADB` to the installed adb executable and `PROSA_ANDROID_SERIAL` if the emulator differs. Evidence is under the ignored `artifacts/android-uat` directory.

Automated regressions: `OpeningAutomaticallyPreparesCheckedContextOnceAndRetainsMessage` exercises recovery after failed preparation without sending or replacing the conversation. `BrainstormCloudSyncSavesPendingWritingBeforeUploadAndStopsOnSaveFailure` exercises save failure and successful upload of newly saved writing through the real sync engine with an in-memory backend. Existing account/context/sync guards run alongside these checks.

Actual cloud opt-in and provider generation on the author's manuscript are left to the author's explicit UI actions. Emulator verification and fixture tests do not establish physical-phone or live-provider acceptance.

## Recorded result

- 52 focused .NET regressions passed: `artifacts/android-brainstorm-tests-final.log`.
- Final Debug x64 APK built with zero errors and was installed in place. Build warnings were package vulnerability-feed access warnings: `artifacts/android-brainstorm-build-final.log`.
- Actual Android conversation exposes a visible, enabled and styled cloud-sync opt-in action: `artifacts/android-uat/brainstorm-sync.png` and `.json`.
- The original saved conversation was reopened after installation. Its pending 192-character message has the same SHA-256 fingerprint as the pre-update baseline. No provider request was sent and no cloud opt-in was performed on this manuscript during the recheck.
