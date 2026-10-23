# Android tone preview feedback — 2026-10-11

The installed emulator reproduced the reported apparent no-op. The manuscript was in `LocalSyncState.Conflict`; `LocalWriting.Prepare` rejected it before any provider request. Its generic synchronization error appeared after recommended tools, help and custom controls, outside the narrow screen at the Preview tone change button.

The shared device writing panel now places progress, cancellation, errors and reviewed proposals immediately after the preview action. Custom-prompt feedback stays after its input. On render, the response scrolls into view without changing editor focus. A sync conflict has a specific explanation and a `Review both versions` link to the existing conflict workspace. Synchronization checks still prevent AI requests against unresolved copies; neither version is chosen automatically.

Validation:

- 129 focused `LocalWritingPanelTests` and `LocalWritingTests` passed, including conflict feedback, zero provider calls, unchanged local writing, custom prompt ordering, review, Apply and history.
- Debug Android build passed; APK installed with `adb install -r`, preserving data.
- `node Tests/android-writing-feedback.mjs baseline` before update and `verify` afterward passed on emulator-5554. The real WebView showed the conflict error beside Preview tone change inside the viewport, with the recovery link. Editor and all document-file hashes remained unchanged.
- Evidence: `artifacts/android-writing-feedback/results.json` and `artifacts/android-uat/tone-conflict-visible.png`.

The author must review local/cloud copies and choose the linked version before retrying. No live AI request or conflict resolution was performed during acceptance. The build reported NuGet vulnerability-feed connectivity warnings, with no compile errors.
