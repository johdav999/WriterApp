# Refactoring prompt — Share the existing Prosa client GUI with desktop

Use the following prompt after the Release 1 desktop implementation. This is a GUI and architecture refactor, not a request to implement every web feature on desktop. It supersedes the earlier recommendation to put all reusable UI in `WriterApp.Device.Shared`: cross-host presentation belongs in a library shared by web and device hosts; device services remain in `WriterApp.Device.Shared`.

```text
Refactor this repository so the Windows desktop app uses the existing web client's GUI through shared Razor components and assets. The web client is the visual and interaction reference. Preserve the desktop's local-first behavior and prepare the shared presentation layer for later iOS use.

Deliver the implementation, not only a plan. Work in small, buildable stages and complete the bounded scope below. Do not introduce a new design or maintain copied web and desktop versions of the same presentation components.

1. Inspect and establish a baseline

Read repository instructions and the current web/device architecture. Inspect at least:
- WriterApp.Client/Layout/MainLayout.razor and its CSS.
- WriterApp.Client/Pages/DocumentsList.razor and its CSS.
- WriterApp.Client/Pages/DocumentEditor.razor, its code-behind and CSS.
- WriterApp.Client/Components/Editor/PageEditor.razor and SectionEditor.razor.
- Client toolbar, icons, navigation/structure components, context panel, AI proposal UI, shared styles, fonts and branding assets.
- WriterApp.Client/src/tiptap-editor.ts, tiptap-commands.ts, device-editor.ts and their build configurations.
- WriterApp.Device.Shared/Layout/DeviceLayout.razor, Pages/Home.razor, Pages/DocumentWorkspace.razor, Components/DeviceTextEditor.razor and registered services.
- Windows/iOS host startup, routing and host-page asset loading.
- docs/device-development.md and docs/release-1-uat.md.

Identify where web presentation is coupled to API loading, cloud saves, authentication, entitlement checks, browser storage or project/scene behavior. Establish the actual test/build baseline and preserve unrelated work. Capture comparable web and desktop screenshots using synthetic document content before changing the UI. If either surface cannot run, record the constraint and continue safe implementation; do not claim visual parity from source inspection.

2. Extract a shared presentation library

Create a plain net10.0 Razor class library, such as WriterApp.UI.Shared, referenced by WriterApp.Client and WriterApp.Device.Shared. Keep dependencies acyclic: neither shared library should reference WriterApp.Client or a native host, and WriterApp.UI.Shared must not require device storage services or MAUI.

Extract the existing client markup, scoped CSS, reusable visual components, icons, branding and required static assets into that library. Update the web client to consume these extracted components too. Preserve its current appearance and behavior. Correct static asset URLs, CSS isolation, JavaScript imports and component namespaces for both hosting models.

The shared scope must cover the application shell, document-library presentation, editor workspace, section/page navigation, formatting toolbar, save/status presentation, focus/context-panel interactions and supported AI proposal presentation. Preserve the client's density, hierarchy, typography, toolbar design and panel placement. Desktop-specific sync, recovery, import/export and diagnostics controls should fit that shell; move maintenance details into an appropriate settings/menu surface instead of placing them above the writing area.

Keep existing web-only features working in their current host through composition or explicit capability inputs. Unsupported desktop features must not appear as working controls or navigate to missing routes. List those feature differences; visual reuse does not authorize a full project/storyboard/search/translation implementation.

3. Separate presentation from host behavior

Keep host route pages and orchestration where appropriate; use small typed view models, parameters, EventCallbacks or narrowly scoped interfaces at real dependency boundaries. Avoid a giant universal service, scattered platform conditionals, or dozens of interfaces without a demonstrated use.

The web host retains its API-backed document loading/saving, existing authentication and established feature behavior. The desktop host retains its local repository, autosave coordinator, recovery journals, native account service, sync engine, AI service, native file transfer and diagnostics. Shared visual components should receive state and actions without owning HTTP persistence, filesystem paths, cookie login redirects or token storage.

Map local and server IDs explicitly at adapter boundaries. Opening, editing, navigating or formatting a desktop document must not require a cloud document ID, account, entitlement check or network request. Distinguish local save success from cloud sync success in the shared status presentation. Do not weaken backend authorization to make a reused button work.

4. Reuse the editor safely

Use the existing client editor presentation and shared TipTap commands. Inspect extension/schema differences before consolidating JavaScript. Share the editor core where compatible, with explicit host configuration/callbacks for persistence, lifecycle and capabilities; thin host entry points are acceptable when necessary.

Bundle all editor code, CSS, icons and fonts required by desktop locally. Do not load the deployed website into the WebView or introduce CDN/development-server dependencies. Do not copy the client's API autosave or browser-only recovery into the desktop editor.

Preserve selection mapping, formatting state, undo/redo, link safety, sanitization, AI preview/Apply/Dismiss, and the desktop focus fix: ordinary autosave must not toggle contenteditable or blur the editor. Preserve save-before-navigation, failed-close protection and edits arriving during pending saves. If a content schema is unsupported, retain the existing read-only/preservation behavior rather than silently dropping content. Do not migrate document files or reset stored writing as part of a visual refactor.

5. Preserve device behavior and iOS readiness

Keep signed-out/offline create, open, edit, autosave, recovery, rename, duplicate, trash/restore and confirmed deletion available. Keep native import/export and diagnostics reachable. Preserve sync queue persistence, conflict copies and explicit resolution, account/backend isolation, expired-session handling and entitlement-loss behavior. Keep all five supported AI actions behind authenticated backend calls and explicit result application.

Use capability-driven composition and responsive layouts so shared UI also compiles for iOS. Keep Windows-only code in WriterApp.Desktop. Do not implement iOS authentication, file-picker or lifecycle adapters in this task; document what remains. Match desktop to the web interface at comparable viewport widths, while allowing responsive rearrangement on future phones/tablets.

6. Validate both hosts and close regressions

Add meaningful focused regression coverage for changed adapters, content/save boundaries and host capability behavior. Extend the existing real-editor browser harness where editor integration changes. Reuse existing storage/sync/auth tests; do not replace them with mocks that bypass the behavior being protected.

Build the server/web solution in Release, Windows host, and iOS managed assembly with warnings treated as errors. Run the full server/shared test suite and shipped-editor harness. Rebuild tracked editor assets if their source changes. Verify project/asset changes are covered by Linux and Windows CI; add the new library and relevant asset/config paths to workflow filters as needed. Preserve blocking checks and the Azure landing deployment's Prosa.Landing-only filter.

Exercise the real web client and Windows app with equivalent synthetic writing. Compare screenshots at 1280x720 and 1920x1080 (or record unavailable sizes), including library, editor toolbar, section/page navigation, focus mode, context panel and AI preview. Verify branding, fonts, spacing, icons and control alignment. Repeat native signed-out edit/autosave/reopen, Unicode import/export, keyboard shortcuts and autosave-focus checks. Verify existing web save/auth flows with an approved test environment when available. Report credential-dependent checks as blocked rather than substituting an assertion that they passed.

Do not deploy Azure, change authentication registrations, trust certificates, install an MSIX, force-push history or publish remotely as part of this refactor. These actions are outside its scope. Existing release gates remain open until independently verified.

Acceptance criteria

- Web and desktop consume the same extracted presentation components and core visual assets for the bounded library/editor scope; the result is not two styled copies.
- The desktop shell and supported editor interactions match the established web GUI at comparable viewport sizes, with documented capability-specific differences.
- Web routes, project/scene features, authentication and API-backed saving continue to work.
- Desktop ordinary editing and durable local saves work without network/account and retain autosave, recovery, conflict safety and native integrations.
- Shared UI has no dependency on a web host, MAUI or Windows-only APIs; the iOS managed build remains viable.
- Tests and builds pass without compiler/analyzer warnings; no unsafe content conversion or new silent-save/conflict behavior is introduced.
- docs/device-development.md explains the new project/dependency structure and extension points. Update docs/release-1-uat.md with this pass's evidence and outstanding gates without overwriting historical results.
- Final report lists what was extracted, where host adapters remain, before/after evidence, validation performed, unsupported desktop features and any remaining release blockers. Do not claim full feature parity or release readiness from visual parity alone.
```
