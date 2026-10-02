# Shared client UI verification — 2026-09-28

Implementation is complete for the bounded shared-presentation scope. Release acceptance remains open for the checks below. This report does not assert complete desktop/web feature parity.

## Implementation and regression checks

The web and device hosts now consume the same shell, library header/cards, editor workspace/heading/toolbar/status, focus/context controls, navigator row, right-panel shell, AI comparison and icons from `WriterApp.UI.Shared`. Existing client CSS is authoritative in that library, with library/editor selectors contained by explicit root classes. Host orchestration and persistence are retained. See [architecture and capability differences](device-development.md#shared-web-and-device-presentation).

Baseline: 544 server/shared tests and 12 shipped-editor browser checks. Two focused tests were added for focus/context coordination and resetting shell state when leaving a document. A thirteenth real-editor harness test covers formatting-state callbacks, toolbar commands, undo and edit locks. Both generated editor bundles were rebuilt.

Builds passed with warnings treated as errors and zero warnings: server/web Release solution, Windows Release host (also Debug), and iOS Debug `iossimulator-x64` managed assembly. This is not an iOS app bundle or simulator run. The final server/shared suite passes 546 tests. The Chrome shipped-editor harness passes all 13 checks, including content preservation, unsafe links, selection mapping, AI apply/undo and keyboard/save behavior.

The server output was locked by a running process, so final validation used `-p:BaseOutputPath=artifacts/ui-final/`; no build configuration was changed to bypass warnings. Native builds used normal output directories. Windows CI now includes shared UI/editor/config paths and builds the bundled assets before building the host. Linux CI remains blocking; Azure landing workflow filters are unchanged.

## Live observations

- Native Windows library and editor rendered the shared branding, toolbar icons, document cards, page navigator, writing canvas, right panel and bottom save status. Existing synthetic Unicode content and bold formatting survived opening in the refactored UI.
- Appended `Shared UI check: Räksmörgås 日本語.` to the existing synthetic UAT document. Continued typing after the autosave interval without clicking the editor again; focus was retained. Ctrl+S produced `Offline · Saved locally`. Existing local services performed the save.
- Web library and scene routes rendered with shared assets after correcting the root asset URL for the `/app` mount. Toolbar commands initialized and focus/context toggles changed their state labels. Table/image and other web-only controls remain in web composition.
- Live web initialization exposed duplicate ProseMirror plugin instances from nested dependency copies. The web Vite build now uses the same deduplication as the device build. Reload initialized a working editor with no corresponding renderer error.
- A synthetic web edit reached `All changes saved`, but reloading the onboarding fixture restored starter text. Persistent web saving is therefore **not accepted** by this run; it needs a normal authenticated document outside the onboarding fixture. Authentication registrations and backend authorization were not changed.

The web server used an isolated local SQLite schema created with `EnsureCreated`, Development authentication and AI disabled. Initial migration-based fixture setup hit an existing duplicate-table error. This run verifies neither database upgrades nor external authentication. No Azure database or live AI service was used.

## Evidence and limits

Local, ignored evidence is in `artifacts/uat/shared-ui/`:

- `desktop-editor-first-pass.png`: first shared-editor rendering.
- `desktop-editor-saved.png`: synthetic Unicode edit, retained formatting and local-save status.
- `web-editor.png`: shared web scene editor with context collapsed.

Earlier native baseline evidence is preserved in `artifacts/uat/release-1/` and the historical [Release 1 report](release-1-uat.md). A comparable before-refactor web screenshot could not be captured in the initial local setup. Do not treat first-pass screenshots as before-refactor evidence. Native capture was approximately 2566×1016; the browser returned its wide viewport despite a requested 1280×720 override. That override was reset. Exact 1280×720 and 1920×1080 paired comparisons remain unverified, as does phone/tablet visual acceptance.

Native UI testing was interrupted by input in the desktop session; no further input was sent after the refreshed capture no longer showed the target app. This pass did not repeat app restart, Unicode native import/export, native focus/context toggles or every library-management action. Prior UAT results remain historical evidence, not new passes. AI comparison has shared escaped-text presentation and existing automated safety coverage, but a live authenticated AI preview was not exercised.

## Remaining release gates

1. Native restart/reopen, import/export, focus/context and keyboard-formatting UAT on the final build; compare both hosts at the requested viewport sizes with equivalent text.
2. Normal authenticated web save/reload and project/navigation regression checks outside onboarding; investigate the fixture reset independently if reproducible in real documents.
3. Credential-dependent account, paid sync/conflict, expiry/entitlement and all five live AI proposal workflows. Preserve existing gates in the Release 1 report.
4. Signed-package installation/update and real iOS/Mac testing. iOS native adapters remain future work.

No deployment, remote publication, authentication-registration change, certificate trust or package installation was performed.
