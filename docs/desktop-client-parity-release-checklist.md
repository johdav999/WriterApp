# Desktop parity acceptance — prompt 12

Date: 2026-09-29. Scope: the existing working tree after parity prompts 1–11, including uncommitted work. No commit, push, Azure deployment, installation, authentication registration, payment or feedback delivery was performed.

## Release decision

**Candidate for an internal Windows local-first preview; not approved as a public, cloud-enabled or fully client-equivalent release.** No unresolved data-loss or broken-save regression was observed in the exercised flows after the fixes below. This is bounded evidence, not certification of every failure path. Paid-account sync, real AI, native authentication and packaged installation still require the live gates below. Managed iOS compilation is not mobile GUI acceptance.

## Environment and reproducible checks

- Actual Windows MAUI executable, isolated `ProsaValidationDataDirectory=artifacts/parity-p12-native-data`; no existing user manuscripts used. Native captures are **2566×1016**, not the requested browser dimensions.
- Actual web client and backend on `http://localhost:5394`, Development, isolated SQLite `artifacts/parity-p11-backend.db`, migrations disabled at startup, AI and Stripe disabled. Synthetic Development entitlement is not a real authenticated paid account. The existing header/profile discrepancy (Not signed in versus synthetic Standard profile) is not production-auth evidence.
- Actual Device.Shared components in the temporary browser host `artifacts/parity-p12-ui` on `http://localhost:5387`, using the same isolated local document directory. Native file/auth adapters are unavailable in this host. It establishes component behavior and browser viewport layout, not Windows host acceptance.
- Synthetic project on both hosts: `Parity P12 — Island voyage`; writing: `Parity P12 — Räksmörgås 日本語 café. The island returns. Bold voyage.` Entire paragraph bold. Matching synopsis logline: `A navigator returns to an island that vanished.`

Final commands, run from the repository root:

```powershell
dotnet build BlazorApp.sln -c Release --no-restore -warnaserror -v:q
dotnet test Tests/WriterApp.Tests/WriterApp.Tests.csproj -c Release --no-build --no-restore -v:q
dotnet build WriterApp.Desktop/WriterApp.Desktop.csproj -c Release --no-restore -warnaserror -v:q
dotnet build WriterApp.iOS/WriterApp.iOS.csproj -c Release -t:Compile --no-restore -warnaserror -v:q
pwsh -NoProfile -File scripts/check-no-forced-auth-nav.ps1
```

Results: **756 tests passed, zero failed/skipped**; all three builds **zero warnings/errors**; authentication-navigation guard passed. The shipped-editor browser harness passed **17/17** checks. No editor JavaScript changed during this prompt. Logs: [suite](../artifacts/parity-p12-tests-final.log), [solution](../artifacts/parity-p12-build-final.log), [Windows](../artifacts/parity-p12-windows-final.log), [iOS managed](../artifacts/parity-p12-ios-final.log), [editor checks](../artifacts/uat/parity-p12/editor-harness.txt). Evidence under ignored `artifacts` is machine-local; archive it with a release candidate before cleaning that directory. Existing generated TipTap bundle trailing whitespace remains outside this prompt's changes.

## Executed workflows

| Flow | Result and boundary | Evidence |
|---|---|---|
| Native empty library → project → writing | Created project, wrote Unicode, applied bold, autosaved and Ctrl+S saved | [Saved editor](../artifacts/uat/parity-p12/native-editor-saved.jpg) |
| Native close → new process → reopen | Library and exact bold/Unicode writing retained | [Restarted library](../artifacts/uat/parity-p12/native-library-restarted.jpg), [reopened editor](../artifacts/uat/parity-p12/native-editor-reopened.jpg) |
| Native notes → component host restart | Original Unicode scene notes retained; summary/synopsis subsequently saved and retained across browser-host restart | [Native planning](../artifacts/uat/parity-p12/native-planning-saved.jpg), [reopened synopsis](../artifacts/uat/parity-p12/device-synopsis-reopened-1920.png) |
| Web project → scene → formatting → reload | Exact Unicode/bold writing retained; synopsis also retained after backend restart | [Web editor](../artifacts/uat/parity-p12/web-editor-1920.png), [web synopsis](../artifacts/uat/parity-p12/web-planning-1920.png) |
| Local search and preview | Japanese query found one page; result selected correct writing; preview highlighted without changing prose | [Preview](../artifacts/uat/parity-p12/device-preview.png) |
| Local structure | Added sections; final title-input fix enables Save while still focused and a single click persists the section | [Section creation](../artifacts/uat/parity-p12/device-section-single-click.png) |
| Shared tabs / keyboard | Device categories now follow web order: Writing, Story, Navigator, Notes & Tasks, History; device Synopsis follows. Right arrow from Writing focused/selected Story. Helper text matches content | [Device editor](../artifacts/uat/parity-p12/device-editor-1920.png) |
| Stale native export | Browser host changed same local document; native rejected export of old revision. Reopen loaded latest writing | [Rejected stale export](../artifacts/uat/parity-p12/native-stale-export-rejected.jpg) |
| Native HTML Save As → Open dialog import | Actual dialogs saved and imported HTML; Unicode/bold and section order retained. Import creates a standalone flattened document, not a project backup | [Export](../artifacts/uat/parity-p12/native-export.html), [save success](../artifacts/uat/parity-p12/native-export-saved.jpg), [round trip](../artifacts/uat/parity-p12/native-import-roundtrip.jpg) |
| Signed-out account | Clear configuration/capability guidance, local writing accessible; no payment or feedback sent | [Native account](../artifacts/uat/parity-p12/native-account.jpg), [web account](../artifacts/uat/parity-p12/web-account-1920.png) |

Local operation with an unavailable backend was exercised. OS-wide network disconnection, authenticated reconnect, expired-token callback and live two-account conflict resolution were **not** exercised. Their automated tests do not close the live gates.

## Regression ledger

| ID | Severity | Reproduction / cause | Acceptance / status |
|---|---|---|---|
| P12-01 | P2 | Paid synthetic profile with AI disabled and zero budget showed “monthly AI limit” in web editor | Fixed shared `ShouldShowAiLimitMessage` to require enabled AI/UI; 3 regression cases plus existing paid exhaustion case pass; rebuilt web banner absent |
| P12-02 | P2 | Device tabs used different order and Story/Notes/Synopsis inherited navigator helper text | Fixed category order and context-specific helpers; native/browser captures and arrow-key navigation verified |
| P12-03 | P2 | New section title left Save disabled until blur, requiring a second click | Fixed `oninput` binding; typed title, observed enabled button without blur, saved with one click |
| P12-04 | P1 | Rapid synopsis typing in server-hosted component harness lost characters (`A nrn vanished.` instead of full sentence); manual value/oninput binding reapplied intermediate values | Replaced manual text bindings with Razor bind get/set oninput in shared synopsis/scene fields and device notes. Same rapid typing retained full text; saved synopsis/summary survived host restart; notes append retained exact text. Native/WebAssembly execution of this exact rapid-keystroke case remains a host-specific follow-up |

## Visual acceptance and remaining differences

See [current gap matrix](desktop-client-gap-analysis.md). Browser-device editor and account captures exist at 1280×720 and 1920×1080; wide library/project/planning captures and compact planning/library captures supplement them. The real web editor and account have both 1280×720 and 1920×1080 evidence; web planning has 1920×1080 and project has 1280×720 evidence. Image dimensions were checked from the saved files. Missing browser comparisons: web library at both sizes, web planning at 1280×720, web project at 1920×1080 and device project at 1280×720. Consult actual image dimensions rather than inferring acceptance from a requested viewport or historical filename. Exact native 1280×720/1920×1080 comparisons and a complete ten-pair screen set remain open.

Shared shell, branding, editor toolbar, panel tabs, scene cards and account presentation reduce divergence. Device project/planning/account forms still use plainer controls and spacing than web; no pixel-equivalence claim. The 1280 planning surface scrolls; editor panels wrap categories. Full keyboard traversal, screen-reader announcements, high-DPI/small native windows, extreme titles and long-book scrolling remain required. Local/cloud durability badges, recovery, native dialogs and Settings are intentional host differences.

Project tree scenes and raw document sections are distinct: adding a section in the editor does **not** automatically create a project scene. The acceptance run retained added sections in writing/export, while the tree still showed its original scene. Before promising unified organization, provide explicit section-to-scene reconciliation or route project manuscript creation through the project tree. Do not silently delete or duplicate unbound writing.

## Remaining gates and next actions

1. **Native authentication and cloud acceptance:** configure an approved staging native client/callback and two test accounts. Create/sync a project from Windows; edit it in web; disconnect backend, edit locally, restart, reconnect and retry. Test rename/rename, move/delete and edit/delete conflicts, inspect preserved versions and resolve each direction. Expire credentials, revoke entitlement, switch account and sign out/restart; verify local writing remains and no account data crosses boundaries. Record server/local IDs, revisions and assertions without tokens.
2. **Backend rollout:** review/backup before migrations. SQLite migrations `20260928191657_AddProjectSynchronization` and `20260929052912_AddPlanningSynchronization`; SQL Server equivalents `20260928191709_AddProjectSynchronizationSqlServer` and `20260929052914_AddPlanningSynchronizationSqlServer`. Apply to a staging copy first, verify project/planning change feeds, concurrency/tombstones and older document-client rejection/compatibility. Deploy compatible project/planning sync contracts and `/api/ai/status` document-version capability with required request/response version checks before enabling desktop cloud AI. See prompt 7–10 notes in [UAT](desktop-client-parity-uat.md); no deployment was performed here.
3. **AI/provider:** approved AI-enabled staging account; run translation, consistency, style, scene-card, synopsis and storyboard actions. Verify preview is inert, explicit typed Apply, reopen, stale revisions, cancellation, quota and expiry preserve prose. Fake-response safety tests are insufficient to approve provider behavior. Verify prompt copy boundaries; automatic prompt conflict sync and complete cloud history remain feature gaps.
4. **Publishing and failure paths:** native DOCX import append/replace/reopen, cover PNG picker, dialog cancellation/overwrite, injected disk/write failure and failed-close recovery. Open DOCX/EPUB in independent readers and test large manuscripts/cover PDF limits. This run closes native HTML round-trip and stale-export checks; historical PDF evidence does not close these remaining branches.
5. **UI/accessibility:** finish every paired screen at both requested viewports, actual native resizing/high DPI, long titles, focus order and screen-reader checks. Recheck rapid planning typing in the final native and web hosts. Resolve raw-section/project-scene UX before claiming organization parity.
6. **Distribution/mobile:** signed Windows install/update/uninstall smoke tests with isolated data, restore after upgrade, release evidence archive. iOS needs native adapters, Mac/Xcode, signing/provisioning and simulator/device acceptance. No mobile release recommendation follows from managed compilation.
7. **Scope gaps:** richer editor schema, full advanced panels/issue cards, complete cloud history, expanded publishing controls, cover studio and onboarding require separate implementation and acceptance. Use the matrix's next actions; do not show nonfunctional controls to suggest parity.

The staging gates require configuration and authorized test accounts absent from this session. They remain release prerequisites rather than reasons to alter production registrations or deploy Azure during this task.
