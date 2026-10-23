# Automatic mobile writing preparation

The device writing tools save pending editor and planning changes before online work. Consistency checks, scene/synopsis/storyboard coaching, story-fact loads and updates, writing previews, translations and AI quality revisions use `DeviceSyncEngine.SynchronizeForAiAsync` to await synchronization and validate the returned document. A signed-in, connected author can run these tools on a local manuscript: preparation enables its cloud copy on demand. Opening the tools alone does not upload the manuscript.

Preparation stops before canon/AI on a failed save, offline state, missing sign-in, sync rejection, unresolved conflict, cloud deletion, cancellation or account change. Conflicts in consistency and story-reference panels offer **Review both versions**. Proposal review and Apply remain explicit. Cloud-link metadata arriving during a writing preview does not cancel that request or discard its result.

## Verification on 2026-10-11

- 262 focused .NET tests passed across consistency panels, writing panels, canon, translation, quality and device synchronization. Added coverage for automatic first upload of local documents/projects, uploading the newest writing and synopsis, unchanged-source reuse, save failure, conflicts, rejected uploads, deletion, offline/sign-out, cancellation/account changes and preview metadata refresh.
- Android Debug APK rebuilt successfully with embedded assemblies and installed in place with `adb install --no-incremental -r`; app data retained.
- `node Tests/android-automatic-sync.mjs baseline` captured hashes before the APK update. `node Tests/android-automatic-sync.mjs verify` exercised Check consistency in the actual Android WebView. The retained manuscript has a genuine unresolved cloud conflict. Preparation displayed that conflict and its review link in the viewport, stopped before analysis, and preserved the editor text and authored content of all ten local document files.
- Emulator evidence: `artifacts/android-automatic-sync/results.json` and `consistency-preparation-conflict.png`. Successful upload/preparation is covered with the real sync engine against a synthetic API; a successful live-provider consistency result and physical-device/iOS execution were not exercised in this verification.

The current manuscript's conflict still requires author review before online checks can proceed. This change does not select one of the conflicting versions automatically.
