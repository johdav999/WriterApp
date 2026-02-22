# WriterApp Feature List

## Executive summary
WriterApp is a Blazor-based writing application with a document/section/page model, a TipTap/ProseMirror editor, AI-assisted writing tools (gated by plan entitlements), export workflows (HTML/Markdown/PDF + templates), and synopsis/outline tooling. The primary user flow is: browse documents ? open a document ? manage sections/pages ? write in the editor with autosave, formatting, and pagination ? use context drawers for notes/scene cards/outline/AI ? export or preview. Authentication and plan-based entitlements are enforced server-side, with an admin-only plan assignment page and AI usage limits. Data is stored in SQLite via EF Core migrations; AI history and usage are persisted, and OpenAI support is available when configured. Deployment supports optional WASM client hosting under `/app` with static asset fallbacks and JS interop for the editor bundle.

---

## Implemented features (confirmed in code/UI)

**Authentication, roles, subscriptions, plans**
- Auth ? Login redirect | Where: `Components/RedirectToLogin.razor` | Trigger: navigation to protected UI routes | Notes: redirects to `/account/login` with returnUrl.
- Auth ? Auth status endpoint | Where: `Program.cs` (`/api/auth/me`) | Trigger: client fetch | Notes: requires auth; returns roles + userId.
- Roles ? Admin-only policy | Where: `Program.cs` (policy `AdminOnly`) | Trigger: role check + bootstrap env vars | Notes: supports bootstrap admin via `BOOTSTRAP_ADMIN_*`.
- Plans ? Admin assignment UI | Where: `Components/Pages/Admin/PlanAssignments.razor` (`/admin/plan-assignments`) | Trigger: Admin page buttons | Notes: assigns plan to user; shows latest assignment.
- Plans ? Entitlements | Where: `Data/AppDbContext.cs` | Trigger: seeded plan entitlements | Notes: `ai.enabled`, `ai.monthly_tokens`, `ai.images.cover`, `export.pdf`.
- Plans ? Admin API endpoint | Where: `Program.cs` (`/api/admin/users/{userId}/plan/{planKey}`) | Trigger: admin UI or API call | Notes: requires `AdminOnly`.

**Documents / sections / pages**
- Documents ? List and create | Where: `WriterApp.Client/Pages/DocumentsList.razor` (`/documents`), `Controllers/DocumentsController.cs` | Trigger: load page; “Create new document” | Notes: creates default structure and navigates to first section.
- Documents ? Rename | Where: `WriterApp.Client/Pages/DocumentsList.razor`, `Controllers/DocumentsController.cs` | Trigger: Rename ? Save | Notes: PUT `api/documents/{id}`.
- Documents ? Open redirect | Where: `WriterApp.Client/Pages/DocumentRedirect.razor` | Trigger: navigation to `/documents/{id}` | Notes: resolves first section and redirects.
- Sections ? List, create, rename | Where: `WriterApp.Client/Pages/DocumentEditor.razor`, `Controllers/SectionsController.cs` | Trigger: section panel; add/rename | Notes: per-document section list with order.
- Sections ? Reorder (drag/drop) | Where: `WriterApp.Client/Pages/DocumentEditor.razor`, `WriterApp.Client/src/tiptap-editor.ts` | Trigger: drag handle in section list | Notes: uses drag handlers; order persisted server-side.
- Sections ? Duplicate & delete | Where: `WriterApp.Client/Pages/DocumentEditor.razor`, `Controllers/SectionsController.cs` | Trigger: section menu | Notes: delete uses confirmation modal.
- Pages ? List/create/update/delete/move | Where: `Controllers/PagesController.cs`, `WriterApp.Client/Components/Editor/PageEditor.razor` | Trigger: page list controls in editor UI | Notes: pages are per section; move API exists.
- Page notes | Where: `Controllers/PagesController.cs` (`/api/pages/{id}/notes`), `WriterApp.Client/Pages/DocumentEditor.razor` | Trigger: notes tab ? Save notes | Notes: stored as `PageNoteRecord`.

**Editor UX**
- Formatting toolbar ? Bold/italic/strike/code/heading/blockquote/lists/hr | Where: `WriterApp.Client/Pages/DocumentEditor.razor`, `WriterApp.Client/src/tiptap-editor.ts`, `WriterApp.Client/src/tiptap-commands.ts` | Trigger: toolbar buttons + context menu | Notes: TipTap/ProseMirror commands.
- Focus mode | Where: `WriterApp.Client/Pages/DocumentEditor.razor`, `WriterApp.md` | Trigger: “Focus mode” toggle + shortcut | Notes: hides panels; layout state stored in localStorage.
- Zoom (editor font scale) | Where: `WriterApp.Client/Pages/DocumentEditor.razor`, `WriterApp.md` | Trigger: Zoom controls | Notes: CSS variable `--editor-font-scale`.
- Autosave | Where: `WriterApp.Client/Components/Editor/PageEditor.razor` | Trigger: debounce on content changes | Notes: 800ms debounce, PUT page content.
- Undo/redo | Where: `WriterApp.Client/src/tiptap-editor.ts`, `WriterApp.Client/Pages/DocumentEditor.razor` | Trigger: toolbar buttons + shortcuts | Notes: uses TipTap commands; AI undo/redo separate.
- Context menu + selection bubble | Where: `WriterApp.Client/Pages/DocumentEditor.razor`, `WriterApp.Client/src/tiptap-editor.ts` | Trigger: right-click or selection | Notes: JS interop exposes selection coords.
- Pagination (page breaks + page count) | Where: `WriterApp.Client/Components/Editor/PageEditor.razor`, `WriterApp.Client/src/tiptap-editor.ts` | Trigger: layout updates | Notes: print layout + gaps, page count displayed.

**Outline & synopsis**
- Synopsis editor | Where: `WriterApp.Client/Pages/Synopsis.razor` (`/synopsis/{DocumentId}`) | Trigger: open synopsis page | Notes: stores outline text in `DocumentOutline`.
- Outline nodes (tree) | Where: `WriterApp.Client/Pages/DocumentEditor.razor`, `Controllers/DocumentOutlineController.cs` | Trigger: outline tab actions | Notes: add/rename/delete/apply outline, fetch nodes.

**Scene cards / structure**
- Scene cards per section | Where: `WriterApp.Client/Pages/DocumentEditor.razor`, `Controllers/SectionSceneCardsController.cs` | Trigger: Scene tab | Notes: fields for narrative purpose, emotional beats, key events, open questions.

**AI features**
- AI actions menu (rewrite/translate/etc.) | Where: `WriterApp.Client/Pages/DocumentEditor.razor`, `Controllers/AiActionsController.cs`, `AI/Actions/*` | Trigger: AI tab or context menu | Notes: gated by plan entitlements and config.
- AI translation (selection/section/document) | Where: `WriterApp.Client/Pages/DocumentEditor.razor(.cs)`, `AI/Actions/Translate*` | Trigger: Translate modal | Notes: supports duplicate/replace flows.
- AI scene coaching (suggest/refine/open questions) | Where: `WriterApp.Client/Pages/DocumentEditor.razor`, `AI/Actions/Scene*` | Trigger: Scene tab buttons | Notes: shows AI proposal and apply/discard.
- AI outline generation | Where: `WriterApp.Client/Pages/DocumentEditor.razor`, `AI/Actions/GenerateOutlineAction.cs` | Trigger: Outline tab ? Generate | Notes: returns proposal preview and apply/discard.
- AI history + undo/redo | Where: `WriterApp.Client/Pages/DocumentEditor.razor`, `Controllers/AiActionsController.cs`, `Data/AI/*` | Trigger: AI History panel | Notes: applied events persisted; undo/redo endpoints.
- AI usage quotas | Where: `Application/Usage/AiUsageStatusService.cs`, `AI/Core/AiUsagePolicy.cs` | Trigger: AI status API + UI checks | Notes: quota/plan gates and rate limits.
- AI provider support | Where: `AI/Providers/OpenAI/*`, `Program.cs` | Trigger: configured provider + API key | Notes: OpenAI enabled only with env key; mock providers included.

**Exporting**
- Export document (HTML/Markdown/PDF) | Where: `WriterApp.Client/Pages/DocumentEditor.razor`, `Controllers/DocumentExportController.cs` | Trigger: Export dialog | Notes: PDF is “print” path; templates apply to HTML/PDF.
- Export preview + print | Where: `WriterApp.Client/Pages/DocumentEditor.razor`, `Controllers/ExportPreviewController.cs` | Trigger: Preview modal | Notes: iframe preview + print button.
- Export templates CRUD | Where: `WriterApp.Client/Pages/DocumentEditor.razor`, `Controllers/ExportTemplatesController.cs` | Trigger: Manage templates | Notes: create/duplicate/edit/delete presets.

**Storage & persistence**
- SQLite persistence + migrations | Where: `Program.cs`, `Data/AppDbContext.cs`, `Migrations/*` | Trigger: app startup | Notes: EF Core migrations run on startup.
- Usage tracking | Where: `Data/Usage/*`, `Application/Usage/*` | Trigger: AI usage metering | Notes: aggregates stored by period.

**Deployment/runtime**
- Server + optional WASM client | Where: `Program.cs` (`WriterApp:WasmClient:Enabled`) | Trigger: app config | Notes: `/app` hosts WASM; server render otherwise.
- TipTap bundle + JS interop | Where: `WriterApp.Client/src/tiptap-editor.ts`, `WriterApp.md` | Trigger: npm build output | Notes: bundle served from `WriterApp.Client/wwwroot/js`.

---

## Partially implemented / WIP

- Outline ? Section linking | Where: `WriterApp.Client/Pages/DocumentEditor.razor.cs` (`EnableOutlineSectionLinking = false`) | Trigger: outline actions | Notes: linking controls exist but flag disables behavior.
- AI history store migration | Where: `Application/AI/AiActionHistoryStore.cs` | Trigger: AI apply | Notes: TODO comment indicates migration to persistent store; EF Core store exists.
- AI cover image generation | Where: `AI/Actions/GenerateCoverImageAction.cs`, UI button labeled “mock” | Trigger: AI menu | Notes: UI labels “mock”; OpenAI image provider requires entitlements/config.
- Translation UX polish | Where: `WriterApp.Client/Pages/DocumentEditor.razor(.cs)` | Trigger: translate modal | Notes: workflow exists; error handling suggests ongoing tuning.

---

## Planned / implied (docs, comments, or stubs)

- Expanded editor layout rules | Where: `WriterApp.md` | Trigger: design guidance | Notes: describes layout constraints not all verified in client UI. [uncertain]
- Additional AI history persistence strategy | Where: `Application/AI/AiActionHistoryStore.cs` | Trigger: TODO | Notes: explicit TODO for persistent store path.
- Future AI/plan telemetry in UI | Where: `WriterApp.md` | Trigger: guidance | Notes: suggests keeping usage telemetry secondary. [uncertain]

---

## Feature tree (2–3 levels)

**Implemented features**
- Documents ? Library ? List/create/rename | Where: `WriterApp.Client/Pages/DocumentsList.razor`, `Controllers/DocumentsController.cs` | Trigger: page load + buttons | Notes: shows last modified + word count.
- Documents ? Navigation ? Open document | Where: `WriterApp.Client/Pages/DocumentRedirect.razor` | Trigger: navigate to `/documents/{id}` | Notes: resolves first section.
- Sections ? Manage ? Add/rename/duplicate/delete | Where: `WriterApp.Client/Pages/DocumentEditor.razor`, `Controllers/SectionsController.cs` | Trigger: section panel + menu | Notes: delete prompts confirmation.
- Sections ? Order ? Drag to reorder | Where: `WriterApp.Client/Pages/DocumentEditor.razor` | Trigger: drag handle | Notes: reorder status indicator visible.
- Pages ? Manage ? Create/update/delete/move | Where: `Controllers/PagesController.cs` | Trigger: editor UI | Notes: move API exists for cross-section page moves. [uncertain: UI controls not located]
- Editor ? Formatting ? Bold/italic/strike/code | Where: `WriterApp.Client/Pages/DocumentEditor.razor`, `WriterApp.Client/src/tiptap-commands.ts` | Trigger: toolbar/context menu/shortcuts | Notes: TipTap-based.
- Editor ? Structure ? Headings/lists/blockquote/hr | Where: `WriterApp.Client/Pages/DocumentEditor.razor`, `WriterApp.Client/src/tiptap-commands.ts` | Trigger: toolbar/context menu | Notes: heading levels 1–6.
- Editor ? UX ? Focus mode/zoom | Where: `WriterApp.Client/Pages/DocumentEditor.razor`, `Application/State/LayoutState.cs` | Trigger: toolbar toggle | Notes: layout state persisted in localStorage.
- Editor ? UX ? Autosave | Where: `WriterApp.Client/Components/Editor/PageEditor.razor` | Trigger: debounce on content change | Notes: 800ms debounce.
- Editor ? UX ? Pagination/page count | Where: `WriterApp.Client/src/tiptap-editor.ts`, `WriterApp.Client/Components/Editor/PageEditor.razor` | Trigger: layout updates | Notes: print layout + page break observer.
- Outline ? Synopsis ? Edit synopsis | Where: `WriterApp.Client/Pages/Synopsis.razor` | Trigger: save button | Notes: stores `DocumentOutline` text.
- Outline ? Tree ? CRUD nodes | Where: `WriterApp.Client/Pages/DocumentEditor.razor`, `Controllers/DocumentOutlineController.cs` | Trigger: outline tab | Notes: apply outline to sections supported.
- Scene cards ? Metadata ? Edit scene fields | Where: `WriterApp.Client/Pages/DocumentEditor.razor`, `Controllers/SectionSceneCardsController.cs` | Trigger: Scene tab | Notes: narrative purpose + beats + events + questions.
- AI ? Actions ? Rewrite/translate/outline/scene coach | Where: `AI/Actions/*`, `Controllers/AiActionsController.cs`, `WriterApp.Client/Pages/DocumentEditor.razor` | Trigger: AI tab/context menu | Notes: gated by entitlements.
- AI ? History ? Undo/redo | Where: `WriterApp.Client/Pages/DocumentEditor.razor`, `Controllers/AiActionsController.cs` | Trigger: AI History buttons | Notes: applies to section edits.
- Export ? Output ? HTML/Markdown/PDF | Where: `WriterApp.Client/Pages/DocumentEditor.razor`, `Controllers/DocumentExportController.cs` | Trigger: Export dialog | Notes: PDF is print-based.
- Export ? Templates ? Manage presets | Where: `WriterApp.Client/Pages/DocumentEditor.razor`, `Controllers/ExportTemplatesController.cs` | Trigger: Manage templates | Notes: create/duplicate/edit/delete.
- Auth ? Plans ? Admin assignments | Where: `Components/Pages/Admin/PlanAssignments.razor` | Trigger: admin UI | Notes: AdminOnly policy.
- Storage ? Persistence ? SQLite EF Core | Where: `Data/AppDbContext.cs`, `Migrations/*` | Trigger: startup migration | Notes: file path differs dev/prod.
- Runtime ? WASM hosting ? Optional `/app` shell | Where: `Program.cs` | Trigger: config `WriterApp:WasmClient:Enabled` | Notes: WASM served from `WriterApp.Client/wwwroot`.

---

## Gap list: most valuable missing features (inferred)
- Document deletion or archiving workflow (no delete endpoint/UI found).
- Search across documents/sections/pages (no search endpoints/UI found).
- Version history or restore for edits (no revision model found).
- Collaboration/sharing features (no sharing endpoints/UI found).
- User-managed plan/billing UI (only admin assignment present).
- Import from common formats (Docx/Markdown) beyond editor paste. [uncertain]
- Offline-first mode or sync conflicts (no client cache/sync logic found).
- Global settings page (preferences appear stored but no settings UI found).
- Export to additional formats (Docx/ePub) (no endpoints found).
- Tagging or metadata for documents (no tags/categories model found).
