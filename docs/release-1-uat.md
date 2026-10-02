# Release 1 Windows UAT — 2026-09-27

**Decision: NO-GO for distribution.** This pass fixes and verifies three issues, but does not close Prompt 12. Native package installation/upgrade, authenticated staging flows, and GitHub Windows CI remain required. No data loss, authentication bypass, secret exposure, or silent conflict was observed in the exercised cases; untested cases prevent a release-wide assurance.

## Product profile and evidence

- Product: Prosa, Windows MAUI Blazor Hybrid, version `0.1.0` / package `0.1.0.1`.
- Baseline: `c8abbb1`, branch `codex/fix-baseline-debt`, plus the Prompt 12 changes accompanying this report.
- Host: Windows build 26200, .NET SDK 10.0.400, runtime 10.0.11, MAUI 10.0.20.
- Native surface: unpackaged Debug executable at `WriterApp.Desktop/bin/Debug/net10.0-windows10.0.19041.0/win-x64/WriterApp.Desktop.exe`; captured window approximately 2566 × 1016 pixels. Display scaling was not varied.
- Role: signed-out author. Synthetic document `UAT 12 — Räksmörgås 日本語`; no customer data or production service mutations. No native auth environment configuration or staging test accounts were supplied.
- Flows: local writing/restart; library lifecycle; import/export; diagnostics; package lifecycle; account/sync/AI; accessibility and stress.
- Evidence: native Windows screenshots and exported files in ignored `artifacts/uat/release-1/`; .NET TRX; browser harness using the shipped editor bundle. Captures stay local and are not committed. Preserve that directory separately if evidence needs to accompany a release review.

## Validation matrix

Native means exercised through the Windows app. Automated means service/storage or browser-harness coverage, not an installed-app result.

| Flow | Evidence and result | Remaining native acceptance |
|---|---|---|
| First launch, create, Unicode writing | **Native pass:** empty library, create synthetic document, edit Swedish/Japanese text and bold formatting. | Repeat in the installed package and signed-in state. |
| Autosave, orderly close, restart | **Native pass:** text and bold persisted after Alt+F4/relaunch; focus survived autosave after UAT-001 fix. | Forced termination with unsaved recovery; network-disabled restart (this run used no account/backend, but did not disable the device network). |
| Multi-section/page editing | **Automated pass:** `LocalEditorSessionTests.MultiplePagesSaveLocallyAndReopenWithoutServer`. | Native multi-section fixture and page switching; verify structure authoring expectations. |
| Essential formatting, keyboard | **Browser pass:** formatting, safe links, undo/redo, Ctrl+S, Windows shortcuts, legacy conversion and unsupported content safeguards. Native bold/Ctrl+A and editor focus checked. | Full keyboard-only traversal; all toolbar actions inside Windows WebView; high DPI and smaller windows. |
| Rename/duplicate/trash/restore/delete | **Native partial:** Duplicate created a separately listed copy with a success message. **Automated pass:** library/storage lifecycle and stale revision protection. | Native rename, inspect copy content/independence, trash, restore, confirmed permanent delete. Only disposable UAT documents may be removed. |
| Import/export, Unicode filename | **Native pass:** append UTF-8 text through Open picker preserved original writing; HTML export through Save picker produced expected formatted Unicode content. | New-document and replace modes, HTML import, plain-text export, cancel/error and overwrite dialogs in native host (covered at service level where applicable). |
| Diagnostic export | **Native pass:** Save picker exported JSON-lines app/runtime/environment and sync counts; no synthetic document prose in output. **Automated pass:** log rotation/redaction. | Repeat after live sync/auth failures. |
| Sign-in/out, expired session | **Automated pass:** device/native bearer authentication tests. **Native blocked:** missing staging registration/configuration and test accounts. | System-browser login/logout, expiration/refresh and account switching. |
| Paid sync, two-client conflict, plan loss | **Automated pass:** persisted queue/restart, idempotency, conflict copies, deletion, auth/entitlement failures and validation classification. | Two real staging clients, reconnection, expired token and revoked plan; compare both copies and server content. |
| Rewrite, expand, shorten, summarize, custom | **Automated/browser pass:** five request mappings, authenticated transport, preview/apply/undo, cancellation, quota/error mapping and inert results. | Each action against configured staging provider, Apply/Dismiss/Undo and cancel; free/signed-out refusal. |
| Unavailable/slow backend; validation, entitlement, quota | **Automated pass:** transport failure classification, cancellation, retry and content preservation. Local native writing worked without a configured account/backend. | Controlled staging failures and visible native feedback; network latency and reconnect while typing. |
| Long documents and common display sizes | Not run natively. Unicode content/filenames passed. An attempted resize did not change the captured window; it is not a responsive-layout pass. | Long manuscript, selection/formatting/save latency, 1280 × 720 and 1920 × 1080, 100/150/200% scale. |
| Package install, launch, upgrade, uninstall | **Build pass:** test-signed MSIX generated. **Install blocked:** Windows rejected untrusted signing chain (`0x800B0109`). | Trusted install; higher version signed with same identity; data/recovery/queue retention; backup and uninstall behavior. |
| CI and deployment | Local builds/tests pass. Azure workflow inspected: only `Prosa.Landing/**` triggers push/PR deployment; this change contains no landing files. | GitHub Windows and Linux checks on the final PR. No push or Azure deployment was performed in this pass. |

## Issue ledger

| ID | Severity / type | Expected versus observed | Fix / owner and acceptance | Status |
|---|---|---|---|---|
| UAT-001 | P1 defect, writing | Autosave should preserve typing focus. Toggling `contenteditable` off/on blurred the editor; Ctrl+A subsequently selected the page. | Workspace now keeps editing enabled during ordinary saves; navigation/replacement still freezes the editor. Repeat the focus steps below; concurrent successful/failed saves retain latest writing. | **Verified native fix** |
| UAT-002 | P1 defect, packaging | Script should build a signed package. `WindowsPackageType=Package` was rejected; file-based signing then failed certificate import; symbol tool discovery produced a warning. | Use installed SDK's `MSIX` value; temporarily import signing key into CurrentUser/My and sign by thumbprint; clean up in finally; resolve `mspdbcmf.exe` through installed VS; publish with warnings as errors. | **Verified build fix**, installation remains open |
| UAT-003 | P2 usability defect, workspace | Writing should be easy to reach. Expanded AI and transfer panels placed the editor below multiple scrolls. | Put editor first and collapse optional panels using native details/summary controls. Confirm editor visible and panels expandable. | **Verified native fix** |
| UAT-004 | P1 release gate, packaging | Installed app must launch and upgrade without losing local writing. | Release owner: approve appropriate certificate trust on a test machine or provide a trusted signing identity; retain that identity for a higher-version upgrade. | **Blocked** |
| UAT-005 | P1 release gate, backend | Authenticated paid sync and AI must work on the real host and refuse unauthorized access. | Backend owner: supply staging URL, native registration/configuration location and paid/free test-account instructions; rehearse migrations and live failures. Never put passwords/tokens in this report. | **Blocked** |
| UAT-006 | P1 release gate, CI | Windows package and Linux suite must pass on the final change. | Repository owner: publish review branch/PR and capture workflow run URLs/artifact. | **Not run remotely** |
| UAT-007 | P1 validation gap, native coverage | Entire requested matrix must have native evidence. | QA: complete remaining native acceptance cells, especially forced-close recovery, lifecycle, long manuscript and display/keyboard checks. | **Open** |

## Reproducible regression cases

### Autosave focus and recovery

1. Launch the Debug app signed out. Create/open the synthetic document and click into the editor.
2. Type `Räksmörgås 日本語`, wait more than two seconds without clicking elsewhere, then press Ctrl+A. Only editor text must be selected. Press Ctrl+B and keep typing after another autosave.
3. Close using Alt+F4, relaunch, open the document. Verify text and bold formatting, then append more text after an autosave without refocusing.

Before fix: Ctrl+A selected surrounding labels/page text. After fix: selection stayed within the editor. Capture: `artifacts/uat/release-1/UAT-001-autosave-focus.png`.

The new `LocalAutosaveTests.TypingDuringFailedAutosaveKeepsNewestWritingForRecoveryAndRetry` holds a primary write pending, accepts newer Unicode text, fails the write, checks the newest recovery record and unchanged primary document, then retries successfully. Existing `EditDuringPendingSaveRemainsRecoverableAndFinalFlushCommitsIt` covers successful concurrent saves. These protect storage behavior; the native focus replay guards the WebView-specific defect.

### Append import, export and diagnostics

1. In the synthetic document, expand Import and export; choose Append, select `import-å.txt` and verify both original prose and imported paragraphs remain.
2. Export HTML using the native picker to `Räksmörgås-日本語.html`. Inspect the exported Unicode content and supported formatting.
3. Expand Updates and diagnostics; export to `native-diagnostics.txt`. Inspect the JSON-lines output for app/runtime information and absence of prose/credentials.

Evidence under `artifacts/uat/release-1/`: `UAT-import-append.png`, `import-å.txt`, `Räksmörgås-日本語.html`, `native-diagnostics.txt`. The HTML export preceded the append import; it verifies the original writing, not a post-import round trip.

### Package build and trust boundary

Run `./scripts/windows/Build-ProsaTestMsix.ps1` after Release restore. The script imports a newly generated signing key only into CurrentUser/My, signs the package, and removes that key-store entry; it does not install or trust the certificate. Evidence: `artifacts/uat/release-1/package-build.log`.

Final package: `artifacts/windows-msix/WriterApp.Desktop_0.1.0.1_x64_Test/WriterApp.Desktop_0.1.0.1_x64.msix`.
SHA-256: `213335B35F06722639DDAFBFD4169AD8B99C6873D98B9CD700214336014D4A76`.
Public certificate: `artifacts/windows-msix/signing/Prosa.TestSigning.cer`, subject `CN=Prosa Development`, thumbprint `FED23EFBF67F93BBCC767C3D81D5C847E0294D24`. A fresh script invocation replaces these test artifacts and identity material; do not rebuild between approval and trust verification without rechecking the certificate.

Automatic approval review rejected importing a previous build's certificate into LocalMachine/TrustedPeople because machine-wide persistent trust was not explicitly authorized. The permitted CurrentUser/TrustedPeople alternative still failed installation (`0x800B0109`, activity `c2dc7390-4d74-000a-be05-afc3744ddd01`). That per-user trust entry was removed. No machine trust change or successful installation occurred. The final package above was subsequently rebuilt with warnings as errors; its installation has not been attempted. An approved trust change must name this exact certificate, be limited to the test machine, and have a cleanup plan after installation/upgrade testing.

## Release checklist

- [x] Server/shared suite: **544 passed**, no skips; `artifacts/uat/release-1/release-1.trx`.
- [x] Shipped editor harness: **12 passed** at `http://127.0.0.1:5179/WriterApp.Client/tests/device-editor.html` (`npm run test:device` to repeat).
- [x] Release server solution, Windows Debug host, Windows Release MSIX publish, and iOS managed Debug assembly build with zero warnings. iOS result does not include an Apple app bundle or simulator run.
- [x] Native signed-out editing, autosave focus, orderly restart, Unicode import/export and diagnostic export as bounded above.
- [x] Azure landing workflow path filter excludes the files in this change; no deployment performed.
- [ ] Trusted package install/launch, stable-identity upgrade with data retention, and uninstall rehearsal after backup.
- [ ] Live staging authentication, paid/free sync, conflict, expiry, plan loss and all five AI actions/failure states.
- [ ] Remaining native lifecycle, forced-close recovery, keyboard, display-size/DPI and long-document matrix.
- [ ] Final GitHub Windows package and Linux server CI pass, with recorded run URLs.
- [ ] Release owner signs off after reviewing all remaining evidence; no unresolved integrity/security defects.

Installation/configuration/backup and troubleshooting: [device development](device-development.md), [Windows package guide](windows-desktop-release.md). Repeat this report against the actual release candidate; Debug and browser evidence alone cannot authorize distribution.

## Shared client UI refactor — 2026-09-28

The web and desktop now consume `WriterApp.UI.Shared` presentation components and locally bundled visual assets. Shared host-independent UI replaces the separate desktop shell and toolbar. Existing device persistence, recovery, synchronization, authentication, AI and transfer services remain in place. See [the shared UI verification report](shared-ui-uat.md) for build/test evidence, native and browser observations, screenshot paths, feature differences and outstanding acceptance gates. This pass does not close credential, packaging, exact-viewport or iOS release gates, and does not overwrite the historical results above.
