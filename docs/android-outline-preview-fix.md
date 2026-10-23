# Adding outline items from reading preview

The embedded project Navigator remained actionable while the document was in
reading preview. Its `BeforeContextWorkAsync` callback rejected that state through
`PanelOperationsBlocked`, and Projects translated the rejection into the misleading
message "Save the current writing and planning before changing structure."

Both embedded project Navigators now use `BeforeOutlineWorkAsync`: it exits reading
preview through the existing preview transition, then runs the normal planning and
writing save handshake. Recovery, deleted-document, active-operation and actual
save-failure guards remain in place.

Regression harness: `node Tests/android-outline-preview.mjs baseline` reproduces
the old failure using a new synthetic project on the installed Android Debug app.
After rebuilding and installing with `adb install -r`, run the same script with
`verify`. It checks adding a part from preview, a subsequent scene addition,
persistence after navigation, and hashes of all pre-existing local documents.
Evidence is saved under `artifacts/android-outline-preview/`.

Verified on 2026-10-16 in the installed Debug WebView on `emulator-5554`:
baseline reproduced the reported message; all four verification checks passed
after an in-place APK update. Build completed with zero warnings and errors.
Physical-device and cloud-sync behavior were not part of this local regression.
