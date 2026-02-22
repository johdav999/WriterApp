# Feature Inventory

## 1) Executive Summary
- Blazor Web App server with InteractiveServer components plus hosted WASM client under `/app` (feature flag `WriterApp:WasmClient:Enabled`).
- Root `/` redirects to `/app/documents` in server host.
- Server Documents landing page at `/documents` with create/open, legacy migration checks, and debug panel in dev.
- Documents/Sections/Pages persisted in SQLite via EF Core (`AppDbContext`, repositories).
- Pages are stored in DB and served via API; editor uses continuous section editing with visual page breaks (client + server).
- TipTap-based editor with toolbar, bubble menu, context menu, focus mode, zoom, panel toggles.
- Notes are stored per page; outlines stored per document (API-backed).
- AI actions routed via server endpoints (`/api/ai/actions/*`) and server-side providers (OpenAI + mock), quotas enforced.
- Exporting to Markdown/HTML/PDF supported via `ExportService` in server editor UI.
- Admin plan assignment API and admin UI page present.
- Diagnostics: circuit logging, client event log, auth debug endpoint, legacy migration logging.

Biggest gaps / unfinished areas
- WASM client does not yet expose full server layout/nav parity beyond Documents and Editor pages (partial shell).
- AI history persistence is in-memory store (`InMemoryAiActionHistoryStore`) with TODO for DB.
- Legacy migration relies on localStorage and can be blocked by auth (observed unauthorized in logs); not fully robust for production.
- Streaming AI is implemented in orchestrator but not wired to HTTP endpoints.
- WASM client still includes template pages (Counter/Weather/Editor placeholder) not integrated with product flow.
- Section/page reordering UI is not present in WASM editor (API supports page move).

## 2) Product Areas (feature list)

### A. App shell & navigation
| Feature | Status | Where | Notes/Constraints |
|---|---|---|---|
| Server app shell with header/nav | Complete | `Components/Layout/MainLayout.razor`, `.razor.css` | Documents/Synopsis/Editor nav; focus mode hides nav. |
| Server landing page (/documents) | Complete | `Components/Pages/Landing.razor` | Creates documents, lists docs; dev debug panel. |
| Root redirect to WASM (/app/documents) | Complete | `Program.cs` `app.MapGet("/")` | Redirects regardless of auth; auth enforced by APIs. |
| Hosted WASM client under `/app` | Complete | `Program.cs` `UseBlazorFrameworkFiles("/app")` + `MapFallbackToFile` | Feature flag `WriterApp:WasmClient:Enabled`. |
| WASM app shell | Partial | `WriterApp.Client/Layout/MainLayout.razor` | Minimal nav; shares layout styling. |
| Feature flag for WASM hosting | Complete | `Program.cs`, `appsettings*.json` | `WriterApp:WasmClient:Enabled`. |

### B. Authentication & authorization
| Feature | Status | Where | Notes/Constraints |
|---|---|---|---|
| EasyAuth integration | Complete | `Application/Security/EasyAuthAuthenticationHandler.cs`, `Program.cs` | Prod default scheme. |
| FakeAuth for dev | Complete | `Application/Security/FakeAuthAuthenticationHandler.cs`, `Program.cs` | Dev default scheme. |
| User ID resolver | Complete | `Application/Security/UserIdResolver.cs` | Requires oid claim. |
| Admin policy | Complete | `Program.cs` policy `AdminOnly` | Uses role or bootstrap OID env vars. |
| Auth debug endpoint | Complete | `Program.cs` `/api/debug/auth` | Requires auth. |
| Auth me endpoint | Complete | `Program.cs` `/api/auth/me` | Requires auth. |

### C. Documents
| Feature | Status | Where | Notes/Constraints |
|---|---|---|---|
| List documents | Complete | `Controllers/DocumentsController.cs` `GET /api/documents` | Auth required; returns `DocumentListItemDto`. |
| Get document detail | Complete | `Controllers/DocumentsController.cs` `GET /api/documents/{id}` | Auth required. |
| Create document | Complete | `Controllers/DocumentsController.cs` `POST /api/documents` | Returns `DocumentCreateResponse` with default section/page. |
| Update document title | Complete | `Controllers/DocumentsController.cs` `PUT /api/documents/{id}` | Auth required. |
| Server landing open document | Complete | `Components/Pages/Landing.razor` | Navigates to editor route. |
| WASM documents list | Complete | `WriterApp.Client/Pages/DocumentsList.razor` | Styled to match server landing. |

### D. Sections
| Feature | Status | Where | Notes/Constraints |
|---|---|---|---|
| List sections for document | Complete | `Controllers/SectionsController.cs` `GET /api/documents/{id}/sections` | Auth required. |
| Create section | Complete | `Controllers/SectionsController.cs` `POST /api/documents/{id}/sections` | Used in WASM editor. |
| Update section | Complete | `Controllers/SectionsController.cs` `PUT /api/documents/{id}/sections/{sectionId}` | Auth required. |
| Select section in editor | Complete | `Components/Pages/DocumentEditor.razor(.cs)` | Navigation to section route. |
| Add section (WASM) | Complete | `WriterApp.Client/Pages/DocumentEditor.razor(.cs)` | Adds section with default title. |
| Reorder sections | Partial | API not present; no UI | Not found. |
| Delete section | Partial | Server Home has delete logic; no API | `Components/Pages/Home.razor` has delete in legacy flow, not in DB-backed API. |

### E. Pages
| Feature | Status | Where | Notes/Constraints |
|---|---|---|---|
| List pages for section | Complete | `Controllers/PagesController.cs` `GET /api/sections/{id}/pages` | Auth required. |
| Create page | Complete | `Controllers/PagesController.cs` `POST /api/sections/{id}/pages` | Auth required. |
| Update page | Complete | `Controllers/PagesController.cs` `PUT /api/pages/{id}` | Auth required. |
| Delete page | Complete | `Controllers/PagesController.cs` `DELETE /api/pages/{id}` | Auth required. |
| Move page | Complete | `Controllers/PagesController.cs` `POST /api/pages/{id}/move` | Auth required. |
| Continuous section editing | Complete | `Components/Pages/DocumentEditor.razor(.cs)` and `WriterApp.Client/Pages/DocumentEditor.razor(.cs)` | Combines pages into one content string; page breaks visual only. |
| Page break overlay | Complete | `wwwroot/js/tiptap-editor.js`, `DocumentEditor.razor(.css)` | Lines render with overlay; labels removed. |
| Page list UI | Partial | `Components/Editor/PageList.razor` | Removed from left panel in current UX. |

### F. Editor (TipTap)
| Feature | Status | Where | Notes/Constraints |
|---|---|---|---|
| TipTap editor integration | Complete | `wwwroot/js/tiptap-editor.js`, `Components/Editor/PageEditor.razor`, `WriterApp.Client/Components/Editor/PageEditor.razor` | JS interop, selection/bubble/context menu. |
| Toolbar commands (bold/italic/etc.) | Complete | `Components/Pages/DocumentEditor.razor(.cs)` + CSS | Also in WASM editor. |
| Selection bubble | Complete | `DocumentEditor.razor` | Small bubble with formatting actions. |
| Context menu | Complete | `DocumentEditor.razor` + JS | Background styling applied. |
| Focus mode | Complete | `LayoutState` + `DocumentEditor.razor.cs` | Hides panels; layout class. |
| Hide panels / collapse drawers | Complete | `LayoutState`, `DocumentEditor.razor.cs` | Panel toggles. |
| Zoom | Complete | `LayoutState` `EditorZoomPercent`, `DocumentEditor.razor.cs` | CSS variable `--editor-font-scale`. |
| Autosave | Complete | `Components/Editor/PageEditor.razor(.cs)` | Debounced save via API. |
| Status bar (word count + page) | Complete | `PageEditor.razor` + JS page metrics | Updates on scroll/edit. |

### G. Notes & Outline
| Feature | Status | Where | Notes/Constraints |
|---|---|---|---|
| Page notes storage | Complete | `Controllers/PagesController.cs` `GET/PUT /api/pages/{id}/notes` | DB table `PageNotes`. |
| Document outline storage | Complete | `Controllers/DocumentOutlineController.cs` `GET/PUT /api/documents/{id}/outline` | DB table `DocumentOutlines`. |
| Notes/Outline UI (server editor) | Complete | `Components/Pages/DocumentEditor.razor` | Right drawer tabs. |
| Notes/Outline UI (WASM editor) | Complete | `WriterApp.Client/Pages/DocumentEditor.razor` | Uses same endpoints. |

### H. AI
| Feature | Status | Where | Notes/Constraints |
|---|---|---|---|
| AI provider abstraction | Complete | `AI/Abstractions/*`, `AI/Core/*` | Router, executor, orchestrator. |
| OpenAI provider | Complete | `AI/Providers/OpenAI/*`, `Program.cs` | API key via `OPENAI_API_KEY`. |
| Mock AI providers | Complete | `AI/Providers/Mock/*`, `Application/Commands/MockAiTextService.cs` | Used for dev/testing. |
| AI status endpoint | Complete | `Program.cs` `/api/ai/status` | Enforces auth and quotas. |
| AI action execution endpoint | Complete | `Controllers/AiActionsController.cs` `POST /api/ai/actions/{actionKey}/execute` | Uses orchestrator + history store. |
| AI actions list endpoint | Complete | `Controllers/AiActionsController.cs` `GET /api/ai/actions` | Returns descriptors. |
| AI history endpoint | Complete | `Controllers/AiActionsController.cs` `GET /api/ai/actions/history` | In-memory store. |
| AI UI (server editor) | Complete | `Components/Pages/DocumentEditor.razor(.cs)` | Actions, history, change details. |
| AI UI (WASM editor) | Partial | `WriterApp.Client/Pages/DocumentEditor.razor(.cs)` | Uses server endpoints; matches action presets. |
| AI quotas | Complete | `AI/Core/AiUsagePolicy.cs`, `Program.cs` | Per user quota enforcement. |

### I. Exporting
| Feature | Status | Where | Notes/Constraints |
|---|---|---|---|
| Export Markdown/HTML | Complete | `Application/Exporting/*`, `Components/Pages/DocumentEditor.razor.cs` | Client-side download via JS interop. |
| Export PDF (print) | Complete | `DocumentEditor.razor.cs` | Uses HTML export + print. |
| WASM export UI | Partial | `WriterApp.Client/Pages/DocumentEditor.razor` | Buttons wired; uses server API for export content? (uses same component logic; verify). |

### J. Admin & subscriptions
| Feature | Status | Where | Notes/Constraints |
|---|---|---|---|
| Plans/entitlements | Complete | `Data/Subscriptions/*`, `Application/Subscriptions/*` | EF-backed. |
| Admin plan assignment API | Complete | `Program.cs` `/api/admin/users/{userId}/plan/{planKey}` | Requires AdminOnly policy. |
| Admin UI | Partial | `Components/Pages/Admin/PlanAssignments.razor` | Server-only UI. |
| Usage metering | Complete | `Application/Usage/*` | Used by AI quota. |

### K. Diagnostics & logging
| Feature | Status | Where | Notes/Constraints |
|---|---|---|---|
| Circuit logging | Complete | `Application/Diagnostics/Circuits/CircuitLoggingHandler.cs` | Registered in `Program.cs`. |
| Client event log | Complete | `Application/Diagnostics/ClientEventLog.cs` | Used on Landing debug panel. |
| Legacy migration diagnostics | Complete | `Landing.razor`, `DocumentStorageService.cs` | Extensive logging. |
| Auth debug endpoint | Complete | `Program.cs` `/api/debug/auth` | Auth required. |

### L. Data & persistence
| Feature | Status | Where | Notes/Constraints |
|---|---|---|---|
| SQLite EF Core storage | Complete | `Data/AppDbContext.cs`, `Program.cs` | Dev path `writerapp.db`, prod `/home/site/data/writerapp.db`. |
| Migrations applied on startup | Complete | `Program.cs` | `Database.MigrateAsync()`. |
| Repositories for documents/sections/pages | Complete | `Data/Documents/*`, `Application/Documents/*` | CRUD used by controllers. |
| Legacy migration from localStorage | Partial | `Application/State/LegacyDocumentMigrationService.cs` | Best-effort; can be blocked by auth. |

## 3) Confirmed Routes & Endpoints

### App routes (server)
- `GET /` -> redirect to `/app/documents` (`Program.cs`).
- `GET /documents` -> Landing page (`Components/Pages/Landing.razor`).
- `GET /documents/{DocumentId}` -> Document redirect (`Components/Pages/DocumentRedirect.razor`).
- `GET /documents/{DocumentId}/sections/{SectionId}/pages/{PageId}` -> Server DocumentEditor (`Components/Pages/DocumentEditor.razor`).
- `GET /doc` and `/doc/{documentId}` -> legacy Home editor (`Components/Pages/Home.razor`).
- `GET /synopsis` and `/synopsis/{DocumentId}` -> Synopsis (`Components/Pages/Synopsis.razor`).
- `GET /admin/plan-assignments` -> Admin page (`Components/Pages/Admin/PlanAssignments.razor`).
- `GET /welcome` -> Index (`Components/Pages/Index.razor`).
- `GET /Error` -> Error page (`Components/Pages/Error.razor`).

### App routes (WASM client under /app)
- `GET /app` -> redirect to `/app/documents` (client `Documents.razor`).
- `GET /app/documents` -> Documents list (`WriterApp.Client/Pages/DocumentsList.razor`).
- `GET /app/documents/{DocumentId}/sections/{SectionId}` -> WASM editor (`WriterApp.Client/Pages/DocumentEditor.razor`).
- `GET /app/editor` -> placeholder (`WriterApp.Client/Pages/Editor.razor`).
- `GET /app/counter` -> template page (`WriterApp.Client/Pages/Counter.razor`).
- `GET /app/weather` -> template page (`WriterApp.Client/Pages/Weather.razor`).
- `GET /app/documents/{DocumentId}/sections/{SectionId}/pages/{PageId}` -> editor shell placeholder (`WriterApp.Client/Pages/EditorShell.razor`).

### API endpoints (auth required unless noted)
- `GET /api/documents` -> list docs (`DocumentsController`).
- `GET /api/documents/{documentId}` -> doc detail (`DocumentsController`).
- `POST /api/documents` -> create (`DocumentsController`).
- `PUT /api/documents/{documentId}` -> update title (`DocumentsController`).
- `GET /api/documents/{documentId}/sections` -> list sections (`SectionsController`).
- `POST /api/documents/{documentId}/sections` -> create section (`SectionsController`).
- `PUT /api/documents/{documentId}/sections/{sectionId}` -> update section (`SectionsController`).
- `GET /api/sections/{sectionId}/pages` -> list pages (`PagesController`).
- `POST /api/sections/{sectionId}/pages` -> create page (`PagesController`).
- `PUT /api/pages/{pageId}` -> update page (`PagesController`).
- `DELETE /api/pages/{pageId}` -> delete page (`PagesController`).
- `POST /api/pages/{pageId}/move` -> move page (`PagesController`).
- `GET /api/pages/{pageId}/notes` -> get notes (`PagesController`).
- `PUT /api/pages/{pageId}/notes` -> save notes (`PagesController`).
- `GET /api/documents/{documentId}/outline` -> get outline (`DocumentOutlineController`).
- `PUT /api/documents/{documentId}/outline` -> save outline (`DocumentOutlineController`).
- `GET /api/ai/actions` -> list AI actions (`AiActionsController`).
- `GET /api/ai/actions/history?documentId=...` -> AI history (`AiActionsController`).
- `POST /api/ai/actions/{actionKey}/execute` -> execute AI action (`AiActionsController`).
- `GET /api/ai/status` -> AI quota/status (`Program.cs` minimal API).
- `GET /api/auth/me` -> auth snapshot (`Program.cs` minimal API).
- `GET /api/debug/auth` -> auth debug (`Program.cs` minimal API).
- `POST /api/admin/users/{userId}/plan/{planKey}` -> admin assign plan (`Program.cs`, AdminOnly).
- `GET /__ping` -> health (`Program.cs` minimal API).

## 4) Feature Flags & Configuration
- `WriterApp:WasmClient:Enabled` (default false) -> enables hosted WASM under `/app` (`Program.cs`).
- `WriterApp:AI:Enabled` -> enables AI in app (`Program.cs`, `WriterAiOptions`).
- `WriterApp:AI:UI:ShowAiMenu` -> toggles AI menu visibility (`WriterAiOptions`).
- `WriterApp:AI:Providers:OpenAI:Enabled` -> enables OpenAI provider (`Program.cs`).
- `WriterApp:AI:Providers:DefaultTextProviderId` / `DefaultImageProviderId` -> provider routing (`WriterAiOptions`).
- `WriterApp:AI:RateLimiting:RequestsPerMinute` -> AI usage policy (`AiUsagePolicy`).
- `WriterApp:Auth:DevAutoLogin` -> dev auth settings (`appsettings.Development.json`).
- `WriterApp:Auth:AdminEmail` -> admin config (unused in code found).
- `OPENAI_API_KEY` (env) -> OpenAI provider key (`OpenAiKeyProvider`).
- `BOOTSTRAP_ADMIN_ENABLED`, `BOOTSTRAP_ADMIN_OID` (env) -> admin policy bootstrap (`Program.cs`).
- `ConnectionStrings:DefaultConnection` -> SQLite path (`Program.cs`, `appsettings*.json`).

## 5) How to verify (manual)

### App shell & navigation
- Visit `/` and confirm redirect to `/app/documents`.
- Visit `/documents` and confirm server landing loads.
- Toggle focus mode in editor and verify side nav hides.

### Authentication & authorization
- Call `/api/auth/me` while authenticated and unauthenticated.
- Verify `/api/documents` is unauthorized when not logged in.
- Access `/admin/plan-assignments` requires AdminOnly.

### Documents
- On `/documents`, click “Create new document” and verify a doc appears.
- Open a document from the list and confirm it loads in editor.
- Update a document title and verify persistence.

### Sections
- In WASM editor, click “Add section” and verify navigation to new section.
- In server editor, select different sections and verify content changes.
- Update section title via API and verify UI updates.

### Pages
- Call `GET /api/sections/{id}/pages` and verify page list.
- Edit text in editor and confirm `PUT /api/pages/{id}` updates content.
- Page break lines appear as you add content.

### Editor
- Use toolbar actions (bold/italic/list/heading) and verify formatting changes.
- Select text to show bubble menu; right-click to show context menu.
- Zoom in/out and verify editor font scale changes.
- Confirm status bar shows word count and page count updates on scroll.

### Notes & outline
- Open Notes tab, save notes; reload and confirm persistence.
- Open Outline tab, save outline; reload and confirm persistence.

### AI
- Call `/api/ai/actions` and ensure actions listed.
- Execute `/api/ai/actions/rewrite.selection/execute` with payload; verify response.
- In editor AI tab, run an action and apply proposal; check history entry.

### Exporting
- Use document menu to export Markdown/HTML and verify download.
- Use PDF export to open print dialog.

### Admin & subscriptions
- Call admin plan assignment endpoint with AdminOnly user.
- Verify plan assignment appears in admin UI.

### Diagnostics & logging
- Enable debug panel on `/documents?debug=1` and observe logs.
- Check circuit logging in server logs on connect/disconnect.

### Data & persistence
- Verify SQLite DB file exists and migrations applied at startup.
- Confirm pages/sections/documents tables have rows after edits.

## 6) Open Questions / Ambiguities
- [TBD] Whether WASM export flow uses server-side export or local download helpers (needs runtime test).
- [TBD] Whether AI streaming endpoints are intended for UI (or unused).
- [TBD] Whether section reordering is planned (no API/UI found).
- [TBD] Whether legacy Home (`/doc`) is still required or deprecated.
- [TBD] Any planned collaboration features (no hubs found beyond Blazor circuits).
