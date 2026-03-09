# Prosa Feature Inventory

## 1. Executive summary

Prosa is currently a writing product built around two connected workflows:

- A manuscript workflow for documents, sections, pages, synopsis, export, and AI-assisted revision.
- A project workflow for parts, chapters, scenes, manuscript routing, progress tracking, and scene metadata.

Main capability areas currently present in code:

- Auth, onboarding, and account state
- Document and project creation / lifecycle
- Rich-text editing with autosave and version history
- Navigator / structure management for projects
- AI drafting, rewrite, translation, synopsis coaching, and continuity tooling
- Search, notes, annotations, quality checks, and history
- Export, preview, templates, and presets
- Subscription, quota, Stripe billing, and admin override tooling
- Admin user management, audit, and maintenance endpoints

Source of truth used for this inventory:

- Current client app in `WriterApp.Client`
- Server startup and API routing in `Program.cs`
- API controllers in `Controllers/`
- Application services in `Application/`
- Data models and entitlements in `Data/`

Important repository note:

- The repo still contains older `Components/Pages` implementations alongside the current `WriterApp.Client` app. This inventory treats the newer `WriterApp.Client` + server API flow as the primary Prosa surface and calls out duplicate / legacy areas in section 10.

## 2. End-user features

### Authentication and account

- Feature name: Sign in / sign out via EasyAuth with local-dev bypass
  - What it does: Supports EasyAuth-based login/logout in deployed environments and direct redirect behavior in local development / loopback.
  - Where it appears: `/login`, `/logout`, `/app/login`, `/app/logout`
  - Main files/components/services involved: `WriterApp.Client/Pages/Login.razor`, `WriterApp.Client/Pages/Logout.razor`, `Application/Security/EasyAuthAuthenticationHandler.cs`, `Program.cs`
  - Status: Implemented
  - Notes / constraints / gaps: Development mode bypasses EasyAuth; startup can also use fake auth / local dev auth depending on environment.

- Feature name: Auth gate for protected routes
  - What it does: Wraps most app routes behind authentication and onboarding checks.
  - Where it appears: Global app routing
  - Main files/components/services involved: `WriterApp.Client/App.razor`, `WriterApp.Client/Components/AuthGuard.razor`, `WriterApp.Client/Components/OnboardingGuard.razor`
  - Status: Implemented
  - Notes / constraints / gaps: Public routes are explicitly limited to login/logout/start paths.

- Feature name: Account and plan view
  - What it does: Shows current plan, AI token usage, period start, and upgrade CTA.
  - Where it appears: `/account`, `/app/account`
  - Main files/components/services involved: `WriterApp.Client/Pages/Account.razor`, `WriterApp.Client/State/AuthMeStateService.cs`, `/api/auth/me`
  - Status: Implemented
  - Notes / constraints / gaps: Primarily subscription and usage oriented; not a broader profile editor.

### Onboarding

- Feature name: First-run onboarding intent picker
  - What it does: Lets the user choose what they are writing, then creates a starter project structure and routes into the editor.
  - Where it appears: `/onboarding`
  - Main files/components/services involved: `WriterApp.Client/Pages/Onboarding.razor`, `WriterApp.Client/Services/OnboardingService.cs`, `Controllers/OnboardingController.cs`, `Controllers/ProjectsController.cs`
  - Status: Implemented
  - Notes / constraints / gaps: Intent options include Novel, Short story, Non-fiction, Blog, Other. Starter structures differ by intent.

- Feature name: Onboarding state tracking
  - What it does: Stores onboarding started/completed state, primary writing intent, and step progress per user.
  - Where it appears: Background behavior and onboarding flow
  - Main files/components/services involved: `Controllers/OnboardingController.cs`, `Controllers/UserProfileController.cs`, `WriterApp.Client/State/OnboardingStateStore.cs`
  - Status: Implemented
  - Notes / constraints / gaps: Controller allows steps up to 10, but current visible onboarding page is a one-screen flow.

- Feature name: Guided onboarding overlay inside editor
  - What it does: Shows a walkthrough overlay tied to specific editor targets and onboarding milestones.
  - Where it appears: Main layout / editor
  - Main files/components/services involved: `WriterApp.Client/Layout/MainLayout.razor`, `WriterApp.Client/Components/GuidedWalkthroughOverlay.razor`, `WriterApp.Client/State/OnboardingOverlayStateService.cs`, `WriterApp.Client/Pages/DocumentEditor.razor.cs`
  - Status: Implemented
  - Notes / constraints / gaps: Overlay logic is present and wired; it is product guidance rather than a separate page flow.

### Dashboard / home / landing experience

- Feature name: Start page with Projects and Documents views
  - What it does: Serves as the main entry hub with tabs for project-based and document-based work.
  - Where it appears: `/documents`
  - Main files/components/services involved: `WriterApp.Client/Pages/Documents.razor`, `Controllers/ProjectsController.cs`, `Controllers/DocumentsController.cs`
  - Status: Implemented
  - Notes / constraints / gaps: Supports creating projects, creating documents, resuming project context, and switching between active/archived/trash document views.

- Feature name: Continue writing strip
  - What it does: Shows the most recently edited project and resumes its last writing context.
  - Where it appears: Documents landing, projects view
  - Main files/components/services involved: `WriterApp.Client/Pages/Documents.razor`, `WriterApp.Client/Pages/Projects.razor`, `WriterApp.Client/State/LastOpenedDocumentStateService.cs`
  - Status: Implemented
  - Notes / constraints / gaps: Resume target is also stored in local storage for projects.

- Feature name: Route redirects into last useful workspace
  - What it does: Redirects root/document/project routes into manuscript, first section, or first scene targets.
  - Where it appears: `/`, `/documents/{id}`, `/projects/{id}/manuscript`, `/projects/{id}/scenes/{id}/edit`
  - Main files/components/services involved: `WriterApp.Client/Pages/Documents.razor`, `WriterApp.Client/Pages/DocumentRedirect.razor`, `WriterApp.Client/Pages/ProjectManuscriptRedirect.razor`, `WriterApp.Client/Pages/ProjectSceneRedirect.razor`
  - Status: Implemented
  - Notes / constraints / gaps: Strong routing support exists for both manuscript and scene-first flows.

### Project management

- Feature name: Create, rename, delete, and list projects
  - What it does: Supports project CRUD for manuscript-level workspaces.
  - Where it appears: `/documents` project view and `/projects`
  - Main files/components/services involved: `WriterApp.Client/Pages/Documents.razor`, `WriterApp.Client/Pages/Projects.razor`, `Controllers/ProjectsController.cs`
  - Status: Implemented
  - Notes / constraints / gaps: Delete uses dedicated project deletion service that also cleans related entities.

- Feature name: Create project from current document
  - What it does: Generates a project workspace from the currently open document.
  - Where it appears: `/projects`
  - Main files/components/services involved: `WriterApp.Client/Pages/Projects.razor`, `Controllers/ProjectsController.cs`
  - Status: Implemented
  - Notes / constraints / gaps: Requires a currently selected document.

- Feature name: Project manuscript association
  - What it does: Associates projects with manuscript documents and can create a default manuscript when missing.
  - Where it appears: Project open / manuscript open flows
  - Main files/components/services involved: `WriterApp.Client/Pages/ProjectManuscriptRedirect.razor`, `Controllers/ProjectsController.cs`
  - Status: Implemented
  - Notes / constraints / gaps: Project workflow can be scene-first even if manuscript sections do not exist yet.

### Project structure / navigator / manuscript planning

- Feature name: Navigator tree for parts, chapters, and scenes
  - What it does: Displays hierarchical project structure and allows node creation and navigation in manuscript order.
  - Where it appears: Embedded navigator inside editor and `/projects/{projectId}`
  - Main files/components/services involved: `WriterApp.Client/Pages/Projects.razor`, `Controllers/ProjectsController.cs`
  - Status: Implemented
  - Notes / constraints / gaps: Supports part/chapter/scene node types.

- Feature name: Add, rename, duplicate, delete, and reorder project nodes
  - What it does: Full structural editing for project nodes.
  - Where it appears: Projects workspace / navigator
  - Main files/components/services involved: `WriterApp.Client/Pages/Projects.razor`, `Controllers/ProjectsController.cs`
  - Status: Implemented
  - Notes / constraints / gaps: Drag/drop reorder is implemented and has diagnostic logging toggles.

- Feature name: Open scene into scene editor flow
  - What it does: Opens a scene node into the document editor through scene-content routing.
  - Where it appears: `/projects/{projectId}/scenes/{sceneNodeId}`, project navigation actions
  - Main files/components/services involved: `WriterApp.Client/Pages/DocumentEditor.razor`, `WriterApp.Client/Pages/ProjectSceneRedirect.razor`, `Controllers/ProjectsController.cs`, `Controllers/ProjectSceneContentController.cs`
  - Status: Implemented
  - Notes / constraints / gaps: Scene content is linked back to document sections/pages.

- Feature name: Outline templates for project structures
  - What it does: Lets users save a project tree as a reusable template, list templates, apply a template to a document outline, and delete templates.
  - Where it appears: Projects modal and related API
  - Main files/components/services involved: `WriterApp.Client/Pages/Projects.razor`, `WriterApp.Client/Program.cs`, `Controllers/OutlineTemplatesController.cs`
  - Status: Implemented
  - Notes / constraints / gaps: Hidden behind `Workflow:OutlineTemplatesEnabled`.

### Project progress / goals

- Feature name: Project progress dashboard
  - What it does: Shows total words, streak, today/week counts, structural counts, drafted/planned scene counts, and coach suggestions.
  - Where it appears: `/projects/{projectId}` progress tab
  - Main files/components/services involved: `WriterApp.Client/Pages/Projects.razor`, `Controllers/ProjectsController.cs`, `Application/Documents/ProjectGoalsService.cs`, `WriterApp.Client/Services/CoachRecommendationService.cs`
  - Status: Implemented
  - Notes / constraints / gaps: Hidden behind `Workflow:GoalsEnabled`.

- Feature name: Writing goals and milestones
  - What it does: Supports daily/weekly targets, timezone, milestone creation/deletion/completion.
  - Where it appears: Project progress tab
  - Main files/components/services involved: `WriterApp.Client/Pages/Projects.razor`, `Controllers/ProjectsController.cs`, `Application/Documents/ProjectGoalsService.cs`
  - Status: Implemented
  - Notes / constraints / gaps: Server explicitly checks for required tables when goals are enabled.

- Feature name: Writing session tracking
  - What it does: Starts/stops a writing session, captures duration, words delta, notes, and recent sessions.
  - Where it appears: Project progress tab
  - Main files/components/services involved: `WriterApp.Client/Pages/Projects.razor`, `Controllers/ProjectsController.cs`, `Application/Documents/ProjectGoalsService.cs`
  - Status: Implemented
  - Notes / constraints / gaps: Also feeds progress snapshots shown on project list items.

### Document and content management

- Feature name: Create, rename, archive, trash, restore, and delete documents
  - What it does: Full lifecycle management for documents including archived and trash views.
  - Where it appears: Documents landing
  - Main files/components/services involved: `WriterApp.Client/Pages/Documents.razor`, `WriterApp.Client/Pages/DocumentsList.razor`, `Controllers/DocumentsController.cs`
  - Status: Implemented
  - Notes / constraints / gaps: There are two client files for the `/documents` route; see inconsistencies section.

- Feature name: Section management
  - What it does: Create, rename, duplicate, delete, import, reorder, and navigate sections within a document.
  - Where it appears: Document editor left rail / dialogs
  - Main files/components/services involved: `WriterApp.Client/Pages/DocumentEditor.razor`, `WriterApp.Client/Pages/DocumentEditor.razor.cs`, `Controllers/SectionsController.cs`
  - Status: Implemented
  - Notes / constraints / gaps: Import supports replace/append behavior and syncs linked scene content.

- Feature name: Page management inside sections
  - What it does: Create, update, move, delete, and annotate pages belonging to a section.
  - Where it appears: Document editor page flow
  - Main files/components/services involved: `WriterApp.Client/Components/Editor/PageEditor.razor`, `WriterApp.Client/Pages/DocumentEditor.razor.cs`, `Controllers/PagesController.cs`
  - Status: Implemented
  - Notes / constraints / gaps: Pages are the primary persisted editable units for manuscript text.

- Feature name: Translation-aware document and section duplication
  - What it does: Supports translated document/section variants and switching between linked translations.
  - Where it appears: Document editor translation switcher and AI translation apply flow
  - Main files/components/services involved: `WriterApp.Client/Pages/DocumentEditor.razor`, `WriterApp.Client/Pages/DocumentEditor.razor.cs`, `Controllers/DocumentsController.cs`, `Controllers/SectionsController.cs`
  - Status: Implemented
  - Notes / constraints / gaps: Translation apply supports replace, duplicate section, and duplicate document flows.

### Synopsis

- Feature name: Structured synopsis editor
  - What it does: Lets users edit synopsis fields such as story intent and supporting planning fields without editing manuscript text.
  - Where it appears: `/synopsis`, `/synopsis/{documentId}`
  - Main files/components/services involved: `WriterApp.Client/Pages/Synopsis.razor`, `Controllers/DocumentSynopsisController.cs`
  - Status: Implemented
  - Notes / constraints / gaps: Autosaves changes and tracks current document.

- Feature name: AI synopsis coaching
  - What it does: Runs synopsis evaluation, guiding questions, and field-level alternative suggestion.
  - Where it appears: Synopsis AI sidebar
  - Main files/components/services involved: `WriterApp.Client/Pages/Synopsis.razor`, `Controllers/DocumentSynopsisController.cs`, `AI/Actions/SynopsisEvaluateAction.cs`, `AI/Actions/SynopsisQuestionsAction.cs`
  - Status: Implemented
  - Notes / constraints / gaps: Suggestions can be applied back into a selected synopsis field.

### Document editor

- Feature name: Rich text editor with formatting toolbar
  - What it does: Supports bold, italic, headings, lists, blockquote, links, strikethrough, code, horizontal rules, alignment, indent, tables, and images.
  - Where it appears: Main document editor
  - Main files/components/services involved: `WriterApp.Client/Pages/DocumentEditor.razor`, `WriterApp.Client/Pages/DocumentEditor.razor.cs`, `WriterApp.Client/Components/Editor/PageEditor.razor`, `WriterApp.Client/Components/Editor/EditorFormattingState.cs`, `WriterApp.Client/wwwroot/js`
  - Status: Implemented
  - Notes / constraints / gaps: Table support is relatively extensive, including row/column operations, merge/split, and header toggles.

- Feature name: Focus mode and collapsible side panels
  - What it does: Lets users hide navigation/context panels to focus on writing.
  - Where it appears: Document editor header and layout
  - Main files/components/services involved: `WriterApp.Client/Pages/DocumentEditor.razor`, `WriterApp.Client/State/LayoutStateService.cs`, `WriterApp.Client/Layout/MainLayout.razor`
  - Status: Implemented
  - Notes / constraints / gaps: Panel state is persisted in local storage.

- Feature name: Image insertion from upload or URL
  - What it does: Allows image insertion into manuscript content and removal of selected images.
  - Where it appears: Editor toolbar and context menu
  - Main files/components/services involved: `WriterApp.Client/Pages/DocumentEditor.razor`, `Controllers/DocumentImageAssetsController.cs`
  - Status: Implemented
  - Notes / constraints / gaps: File uploads are routed through document image asset endpoints.

- Feature name: Selection bubble and context menu
  - What it does: Shows selection-aware actions and context commands near the current selection.
  - Where it appears: Document editor
  - Main files/components/services involved: `WriterApp.Client/Pages/DocumentEditor.razor`, `WriterApp.Client/Pages/DocumentEditor.razor.cs`
  - Status: Implemented
  - Notes / constraints / gaps: Includes AI actions in context menu when entitled.

- Feature name: Zoom and print layout controls
  - What it does: Changes visual editor density and supports a print-like view in the editor.
  - Where it appears: Editor toolbar and export/preview flow
  - Main files/components/services involved: `WriterApp.Client/Pages/DocumentEditor.razor`, `WriterApp.Client/Pages/DocumentEditor.razor.cs`
  - Status: Implemented
  - Notes / constraints / gaps: Separate from the final export preview.

- Feature name: Heading numbering support
  - What it does: Applies heading numbering behavior in the editor and export-related workflows.
  - Where it appears: Editor rendering / export behavior
  - Main files/components/services involved: `WriterApp.Client/Components/Editor/PageEditor.razor`, `Application/Documents/HeadingPrefixCountersService.cs`, `Application/Exporting/OutlineOrderResolver.cs`
  - Status: Implemented
  - Notes / constraints / gaps: This is a platform-backed writing aid rather than a dedicated top-level feature.

### Notes, outline, annotations, scene metadata, and quality support

- Feature name: Section / scene notes
  - What it does: Stores notes for the current scene/section with autosave behavior.
  - Where it appears: Right-side story / notes panel in editor
  - Main files/components/services involved: `WriterApp.Client/Pages/DocumentEditor.razor`, `WriterApp.Client/Pages/DocumentEditor.razor.cs`, `Controllers/SceneNotesController.cs`, `Controllers/SectionNotesController.cs`
  - Status: Implemented
  - Notes / constraints / gaps: Uses scene notes when scene context exists and falls back to section notes.

- Feature name: Scene card metadata
  - What it does: Captures narrative purpose, emotional beat, key events, open questions, POV, place, timeline marker, and tags.
  - Where it appears: Right-side scene card panel in editor
  - Main files/components/services involved: `WriterApp.Client/Pages/DocumentEditor.razor`, `WriterApp.Client/Pages/DocumentEditor.razor.cs`, `Controllers/SceneCardsController.cs`, `Controllers/SectionSceneCardsController.cs`
  - Status: Implemented
  - Notes / constraints / gaps: Supports AI-generated scene-card suggestions and apply flow.

- Feature name: Outline editor and outline nodes
  - What it does: Supports a document outline with node metadata, link-to-section, apply-to-sections, and undo/redo on outline edits.
  - Where it appears: Editor context panel / export interactions / outline APIs
  - Main files/components/services involved: `Controllers/DocumentOutlineController.cs`, `Controllers/OutlineTemplatesController.cs`, `WriterApp.Client/Pages/DocumentEditor.razor.cs`
  - Status: Implemented
  - Notes / constraints / gaps: Direct user-facing outline UI exists, but there are also hidden or backend-oriented outline capabilities.

- Feature name: Page annotations and TODOs
  - What it does: Lets users create comments, TODOs, and highlights anchored to text selections, then resolve/reopen them.
  - Where it appears: Editor annotations panel
  - Main files/components/services involved: `WriterApp.Client/Pages/DocumentEditor.razor`, `WriterApp.Client/Pages/DocumentEditor.razor.cs`, `Controllers/PageAnnotationsController.cs`
  - Status: Implemented
  - Notes / constraints / gaps: Anchor updates are persisted when content moves.

- Feature name: Quality checks and issue dismissal
  - What it does: Runs rule-based page quality checks, lists issues, and allows dismiss/reopen and some quick-fix application flows.
  - Where it appears: Style & quality panel in editor
  - Main files/components/services involved: `WriterApp.Client/Pages/DocumentEditor.razor`, `WriterApp.Client/Pages/DocumentEditor.razor.cs`, `Controllers/PageQualityChecksController.cs`, `Application/Documents/Quality/QualityCheckService.cs`, `Application/Documents/Quality/QualityRules.cs`
  - Status: Implemented
  - Notes / constraints / gaps: Scene-level quality endpoints also exist but are not clearly surfaced in the current client.

- Feature name: Story canon / bibles
  - What it does: Stores character, place, and timeline canon snapshots for continuity work.
  - Where it appears: Consistency panel in editor
  - Main files/components/services involved: `WriterApp.Client/Pages/DocumentEditor.razor.cs`, `Controllers/DocumentBiblesController.cs`, `Application/Continuity/BibleRefreshService.cs`
  - Status: Implemented
  - Notes / constraints / gaps: Canon refresh is AI-backed and entitlement-gated.

### Search

- Feature name: Global project search
  - What it does: Searches document text and optional metadata such as outline and scene cards within the current project.
  - Where it appears: Main layout header
  - Main files/components/services involved: `WriterApp.Client/Components/Search/GlobalSearch.razor`, `Controllers/SearchController.cs`, `Application/Search/SearchIndexService.cs`
  - Status: Implemented
  - Notes / constraints / gaps: Search is disabled until a project context can be resolved; index rebuild can be triggered automatically when empty.

### Export

- Feature name: Export dialog with scope selection
  - What it does: Exports full document or scoped content using selected format, section scope, or selection scope.
  - Where it appears: Document editor export dialog
  - Main files/components/services involved: `WriterApp.Client/Pages/DocumentEditor.razor`, `WriterApp.Client/Pages/DocumentEditor.razor.cs`, `Controllers/DocumentExportController.cs`
  - Status: Implemented
  - Notes / constraints / gaps: Supports content selection, title page, TOC, chapter break rules, and selection-specific export inputs.

- Feature name: Export preview and print preview
  - What it does: Renders preview HTML before export and supports print-oriented output.
  - Where it appears: Document editor export preview
  - Main files/components/services involved: `Controllers/ExportPreviewController.cs`, `Controllers/DocumentExportController.cs`, `WriterApp.Client/Pages/DocumentEditor.razor.cs`
  - Status: Implemented
  - Notes / constraints / gaps: Preview includes search, zoom, page counting, and page navigation.

- Feature name: Export templates and presets
  - What it does: Supports saving reusable export templates and export presets and setting project defaults.
  - Where it appears: Document editor export UI
  - Main files/components/services involved: `Controllers/ExportTemplatesController.cs`, `Controllers/ExportPresetsController.cs`, `Controllers/DocumentExportSettingsController.cs`, `WriterApp.Client/Pages/DocumentEditor.razor`, `WriterApp.Client/Pages/DocumentEditor.razor.cs`
  - Status: Implemented
  - Notes / constraints / gaps: Includes duplicate/create/update/delete operations.

### Subscription and billing

- Feature name: Billing checkout and portal access
  - What it does: Allows plan upgrades and billing portal access through Stripe-backed flows.
  - Where it appears: `/billing/checkout`, `/account/billing`, `/upgrade`
  - Main files/components/services involved: `WriterApp.Client/Pages/BillingCheckout.razor`, `WriterApp.Client/Pages/Upgrade.razor`, `Controllers/BillingController.cs`
  - Status: Implemented
  - Notes / constraints / gaps: Availability depends on Stripe configuration.

- Feature name: Upgrade prompts from entitlement failures
  - What it does: Redirects users to upgrade flow when an AI or subscription-gated feature is blocked.
  - Where it appears: Editor, synopsis, account, upgrade page
  - Main files/components/services involved: `Application/Subscriptions/EntitlementDeniedApiError.cs`, `WriterApp.Client/Pages/Upgrade.razor`, `WriterApp.Client/Pages/DocumentEditor.razor.cs`
  - Status: Implemented
  - Notes / constraints / gaps: Feature key is propagated into the upgrade route.

### Usability and supporting UX

- Feature name: Autosave for editor content
  - What it does: Saves page content through coordinated editor save logic and version checkpoints.
  - Where it appears: Document editor
  - Main files/components/services involved: `WriterApp.Client/Services/EditorSaveCoordinator.cs`, `WriterApp.Client/Components/Editor/PageEditor.razor`, `Controllers/PagesController.cs`
  - Status: Implemented
  - Notes / constraints / gaps: Integrated with version history checkpoints.

- Feature name: Recovery drafts
  - What it does: Preserves client-side draft recovery state for editor work.
  - Where it appears: Editor support service
  - Main files/components/services involved: `WriterApp.Client/Services/RecoveryDraftService.cs`, `Program.cs`
  - Status: Implemented
  - Notes / constraints / gaps: This is a support capability rather than a separately marketed surface.

- Feature name: Feedback submission from editor
  - What it does: Sends bug/enhancement feedback including optional diagnostics context.
  - Where it appears: Document editor feedback dialog
  - Main files/components/services involved: `WriterApp.Client/Pages/DocumentEditor.razor.cs`, `Controllers/FeedbackController.cs`
  - Status: Implemented
  - Notes / constraints / gaps: Delivery depends on SMTP configuration.

## 3. Admin features

- Feature name: Admin users page
  - What it does: Central admin UI for user lookup, filters, plan info, token status, override actions, and user detail operations.
  - Where it appears: `/admin/users`
  - Main files/components/services involved: `Components/Pages/Admin/Users.razor`, `Application/Users/AdminUsersService.cs`, `Program.cs`
  - Status: Implemented
  - Notes / constraints / gaps: Page is hidden unless `Admin:EnableAdminApi=true`.

- Feature name: Admin user search, filtering, paging, and CSV export
  - What it does: Filters users by text, plan, subscription status, override state, token ranges, and exports CSV.
  - Where it appears: Admin users page and admin API
  - Main files/components/services involved: `Components/Pages/Admin/Users.razor`, `Application/Users/AdminUsersService.cs`, `Program.cs`
  - Status: Implemented
  - Notes / constraints / gaps: Export is server-generated CSV.

- Feature name: Admin create / update / delete user metadata
  - What it does: Supports pre-provisioning profiles, metadata updates, and deletion controls.
  - Where it appears: Admin users page and admin endpoints
  - Main files/components/services involved: `Components/Pages/Admin/Users.razor`, `Application/Users/AdminUsersService.cs`, `Program.cs`
  - Status: Implemented
  - Notes / constraints / gaps: Delete behavior is guarded by active-subscription checks unless explicitly allowed.

- Feature name: Admin plan override
  - What it does: Sets or clears manual plan overrides and reverts to Stripe/default plan when cleared.
  - Where it appears: Admin users page, `/api/admin/users/{userId}/plan-override`, legacy `/api/admin/users/{userId}/plan/{planKey}`
  - Main files/components/services involved: `Application/Subscriptions/AdminPlanOverrideService.cs`, `Components/Pages/Admin/Users.razor`, `Program.cs`
  - Status: Implemented
  - Notes / constraints / gaps: `Components/Pages/Admin/PlanAssignments.razor` now redirects to the users page.

- Feature name: Admin token reset / token adjustment
  - What it does: Resets token periods and optionally adjusts token usage/budget.
  - Where it appears: Admin users page and admin API
  - Main files/components/services involved: `Components/Pages/Admin/Users.razor`, `Application/Users/AdminUsersService.cs`, `Program.cs`
  - Status: Implemented
  - Notes / constraints / gaps: Token adjustment is hidden behind `Admin:EnableTokenAdjust`.

- Feature name: Admin Stripe sync / resync
  - What it does: Forces Stripe entitlement sync for a user or broader admin resync flows.
  - Where it appears: Admin actions and admin endpoints
  - Main files/components/services involved: `Application/Users/AdminUsersService.cs`, `Program.cs`, `Controllers/StripeWebhookController.cs`
  - Status: Implemented
  - Notes / constraints / gaps: Depends on Stripe configuration.

- Feature name: Admin audit event list
  - What it does: Shows admin actions with time, actor, action, target user, and JSON details.
  - Where it appears: Admin users page audit tab and `/api/admin/audit`
  - Main files/components/services involved: `Components/Pages/Admin/Users.razor`, `Application/Users/AdminAuditService.cs`, `Program.cs`
  - Status: Implemented
  - Notes / constraints / gaps: This is the main support/audit trail surface currently exposed.

- Feature name: Admin DB migration endpoint
  - What it does: Runs database migration endpoint for operational recovery / deployment support.
  - Where it appears: `/api/admin/db/migrate`
  - Main files/components/services involved: `Program.cs`
  - Status: Implemented
  - Notes / constraints / gaps: Admin-only and operational rather than end-user-facing.

- Feature name: Admin scene-content backfill endpoint
  - What it does: Runs scene-content backfill for project scene linkage repair.
  - Where it appears: `/api/admin/scene-content-backfill/run`
  - Main files/components/services involved: `Controllers/AdminSceneContentBackfillController.cs`, `Application/Documents/SceneContentBackfillService.cs`
  - Status: Stub / placeholder / hidden behind flag
  - Notes / constraints / gaps: Exists as admin API only and is gated by `Workflow:SceneContentBackfillAdminEnabled`.

- Feature name: Internal admin reset for Stripe link
  - What it does: Resets stored Stripe linkage through an internal endpoint.
  - Where it appears: `internal/admin/reset-stripe-link`
  - Main files/components/services involved: `Controllers/InternalAdminController.cs`
  - Status: Implemented
  - Notes / constraints / gaps: Internal support endpoint, not a normal UI feature.

## 4. AI capabilities

### User-facing AI actions and tools

- User-visible name: Rewrite selection
  - Internal action key / service name if discoverable: `rewrite.selection`, `AI/Actions/RewriteSelectionAction.cs`
  - What it does: Rewrites selected text using instruction, tone, length, and preserve-terms inputs.
  - Current behavior: Available in editor menus and presets; returns proposal text and records history.
  - Status: Implemented
  - Relevant files: `AI/Actions/RewriteSelectionAction.cs`, `Controllers/AiActionsController.cs`, `WriterApp.Client/Pages/DocumentEditor.razor.cs`

- User-visible name: Translation tools
  - Internal action key / service name if discoverable: `translate.selection`, `translate.section`, `translate.document`, `AI/Actions/TranslateAction.cs`
  - What it does: Translates selected text, a section, or a full document with source/target language controls.
  - Current behavior: Includes translation proposal UI, alignment mode, and apply modes for replace / duplicate section / duplicate document.
  - Status: Implemented
  - Relevant files: `AI/Actions/TranslateAction.cs`, `Controllers/AiActionsController.cs`, `WriterApp.Client/Components/AI/TranslationProposalPanel.razor`, `WriterApp.Client/Pages/DocumentEditor.razor`, `WriterApp.Client/Pages/DocumentEditor.razor.cs`

- User-visible name: Propose next paragraph
  - Internal action key / service name if discoverable: `propose.next-paragraph`, `AI/Actions/ProposeNextParagraphAction.cs`
  - What it does: Generates a continuation paragraph using current section and scene context.
  - Current behavior: Offered as an AI action preset in the editor.
  - Status: Implemented
  - Relevant files: `AI/Actions/ProposeNextParagraphAction.cs`, `WriterApp.Client/Pages/DocumentEditor.razor.cs`

- User-visible name: Expand selection / Expand section
  - Internal action key / service name if discoverable: `expand.selection`, `expand.section`, `AI/Actions/ReviseTextActions.cs`
  - What it does: Expands text while preserving intent.
  - Current behavior: Registered only when revise tools are enabled.
  - Status: Stub / placeholder / hidden behind flag
  - Relevant files: `Program.cs`, `AI/Actions/ReviseTextActions.cs`, `WriterApp.Client/Pages/DocumentEditor.razor.cs`

- User-visible name: Tighten selection / Tighten section
  - Internal action key / service name if discoverable: revise tools in `AI/Actions/ReviseTextActions.cs`
  - What it does: Condenses text.
  - Current behavior: Backend registration is feature-flagged; not strongly surfaced in the current client presets.
  - Status: Partially implemented
  - Relevant files: `Program.cs`, `AI/Actions/ReviseTextActions.cs`

- User-visible name: Change tone / Show, don’t tell
  - Internal action key / service name if discoverable: revise tools in `AI/Actions/ReviseTextActions.cs`
  - What it does: Performs style transforms on selection or section text.
  - Current behavior: Some presets are visible in the editor; broader action family is feature-flagged.
  - Status: Partially implemented
  - Relevant files: `Program.cs`, `AI/Actions/ReviseTextActions.cs`, `WriterApp.Client/Pages/DocumentEditor.razor.cs`

- User-visible name: Synopsis evaluate / guiding questions / suggest alternative
  - Internal action key / service name if discoverable: `SynopsisEvaluateAction`, `SynopsisQuestionsAction`, suggest path in `DocumentSynopsisController`
  - What it does: Evaluates synopsis quality, asks guiding questions, and suggests alternatives for selected fields.
  - Current behavior: Fully wired in synopsis page UI.
  - Status: Implemented
  - Relevant files: `AI/Actions/SynopsisEvaluateAction.cs`, `AI/Actions/SynopsisQuestionsAction.cs`, `Controllers/DocumentSynopsisController.cs`, `WriterApp.Client/Pages/Synopsis.razor`

- User-visible name: Story coach
  - Internal action key / service name if discoverable: `story.coach`, `AI/Actions/StoryCoachAction.cs`, `Application/AI/StoryCoach/StoryCoachContextBuilder.cs`
  - What it does: Produces story coaching feedback based on built story context.
  - Current behavior: Action exists and is hidden from generic action list in `AiActionsController`; supporting coach UI language exists in editor.
  - Status: Partially implemented
  - Relevant files: `AI/Actions/StoryCoachAction.cs`, `Application/AI/StoryCoach/StoryCoachContextBuilder.cs`, `Controllers/AiActionsController.cs`, `WriterApp.Client/Pages/DocumentEditor.razor.cs`

- User-visible name: Scene card AI suggestion / refine / open questions
  - Internal action key / service name if discoverable: `scene.suggest`, `scene.refine`, `scene.find_open_questions`
  - What it does: Suggests scene card fields, refines scene thinking, and extracts open questions.
  - Current behavior: Visible in scene card panel and apply flow.
  - Status: Implemented
  - Relevant files: `AI/Actions/SceneSuggestAction.cs`, `AI/Actions/SceneRefineAction.cs`, `AI/Actions/SceneFindOpenQuestionsAction.cs`, `WriterApp.Client/Pages/DocumentEditor.razor`, `WriterApp.Client/Pages/DocumentEditor.razor.cs`

- User-visible name: Generate outline / Generate outline from synopsis
  - Internal action key / service name if discoverable: `generate.outline`, `generate.outline.from_synopsis`
  - What it does: Creates outline content from manuscript or synopsis context.
  - Current behavior: Outline generation exists in action layer; synopsis-origin generation is feature-flagged.
  - Status: Partially implemented
  - Relevant files: `AI/Actions/GenerateOutlineAction.cs`, `AI/Actions/GenerateOutlineFromSynopsisAction.cs`, `Program.cs`, `Controllers/AiActionsController.cs`

- User-visible name: Generate cover image
  - Internal action key / service name if discoverable: `generate.image.cover`, `AI/Actions/GenerateCoverImageAction.cs`
  - What it does: Produces a cover-image prompt from document title and excerpt.
  - Current behavior: Action is registered; older UI labels call it “mock” and current entitlements restrict it to Professional.
  - Status: Partially implemented
  - Relevant files: `AI/Actions/GenerateCoverImageAction.cs`, `Program.cs`, `Data/AppDbContext.cs`

- User-visible name: Prompt Library
  - Internal action key / service name if discoverable: `CustomTransformAction`, `api/ai/presets`
  - What it does: Lets users store and run AI prompt presets, including builtin or custom transform definitions.
  - Current behavior: Full editor UI exists for create/update/delete/run; backend registration of custom transform is feature-flagged.
  - Status: Partially implemented
  - Relevant files: `Controllers/AiPresetsController.cs`, `AI/Actions/CustomTransformAction.cs`, `Program.cs`, `WriterApp.Client/Pages/DocumentEditor.razor`, `WriterApp.Client/Pages/DocumentEditor.razor.cs`

### Continuity / canon / consistency AI

- User-visible name: Story canon extraction and refresh
  - Internal action key / service name if discoverable: `continuity.extract_character_bible`, `continuity.extract_place_bible`, `continuity.extract_timeline_bible`, refresh variants, `BibleRefreshService`
  - What it does: Builds and refreshes character, place, and timeline canon snapshots.
  - Current behavior: Available from consistency panel when feature flags and entitlements allow it.
  - Status: Implemented
  - Relevant files: `AI/Actions/ContinuityActions.cs`, `Application/Continuity/BibleRefreshService.cs`, `Controllers/DocumentBiblesController.cs`, `WriterApp.Client/Pages/DocumentEditor.razor.cs`

- User-visible name: Continuity check
  - Internal action key / service name if discoverable: `continuity.check_section`
  - What it does: Runs a consistency analysis against section text and canon.
  - Current behavior: Produces a continuity report with severity filtering and issue previews.
  - Status: Implemented
  - Relevant files: `AI/Actions/ContinuityActions.cs`, `Controllers/AiActionsController.cs`, `WriterApp.Client/Pages/DocumentEditor.razor.cs`

- User-visible name: Apply continuity fix
  - Internal action key / service name if discoverable: `continuity.apply_fix`
  - What it does: Applies AI-generated fixes for continuity issues.
  - Current behavior: Exists and is UI-wired, but registration is separately flag-gated.
  - Status: Partially implemented
  - Relevant files: `Program.cs`, `AI/Actions/ContinuityActions.cs`, `WriterApp.Client/Pages/DocumentEditor.razor.cs`

### AI execution infrastructure

- User-visible name: AI action history
  - Internal action key / service name if discoverable: `IAiActionHistoryStore`, `EfCoreAiActionHistoryStore`
  - What it does: Stores proposal history, applied events, and supports filtering / review.
  - Current behavior: User can inspect past AI proposals in the editor.
  - Status: Implemented
  - Relevant files: `Application/AI/EfCoreAiActionHistoryStore.cs`, `Controllers/AiActionsController.cs`, `WriterApp.Client/Pages/DocumentEditor.razor`

- User-visible name: AI undo / redo
  - Internal action key / service name if discoverable: `/api/ai/actions/history/undo`, `/api/ai/actions/history/redo`
  - What it does: Replays AI-applied history states for undo/redo at document/section/page scope.
  - Current behavior: UI controls exist in editor history panel.
  - Status: Implemented
  - Relevant files: `Controllers/AiActionsController.cs`, `WriterApp.Client/Pages/DocumentEditor.razor`, `WriterApp.Client/Pages/DocumentEditor.razor.cs`

- User-visible name: AI quota and entitlement enforcement
  - Internal action key / service name if discoverable: `AiQuotaService`, `AiUsageStatusService`, `AiUsagePolicy`
  - What it does: Enforces token quotas and plan entitlements and returns upgrade metadata.
  - Current behavior: Client surfaces quota exceeded banners and upgrade CTAs.
  - Status: Implemented
  - Relevant files: `Application/Usage/AiQuotaService.cs`, `Application/Usage/AiUsageStatusService.cs`, `Application/Subscriptions/EntitlementDeniedApiError.cs`, `WriterApp.Client/Pages/DocumentEditor.razor.cs`, `WriterApp.Client/Pages/Synopsis.razor`

- User-visible name: AI provider routing
  - Internal action key / service name if discoverable: `DefaultAiRouter`, `OpenAiProvider`, `MockTextProvider`, `MockImageProvider`
  - What it does: Routes AI requests to mock or OpenAI providers depending on configuration and modality.
  - Current behavior: OpenAI is enabled only when configured; mock providers remain available.
  - Status: Implemented
  - Relevant files: `WriterApp/AI/Core/DefaultAiRouter.cs`, `AI/Providers/OpenAI/OpenAiProvider.cs`, `AI/Providers/Mock/MockTextProvider.cs`, `Program.cs`

## 5. Export and publishing capabilities

- Supported output formats
  - HTML
    - Status: Implemented
    - Files: `Application/Exporting/TemplatedHtmlExportRenderer.cs`, `Application/Exporting/HtmlExportRenderer.cs`
  - Markdown
    - Status: Implemented
    - Files: `Application/Exporting/MarkdownExportRenderer.cs`
    - Notes: `Application/Exporting/ExportHelpers.cs` contains a TODO indicating markdown mapping is not fully polished.
  - DOCX
    - Status: Implemented
    - Files: `Application/Exporting/DocxExportRenderer.cs`
    - Notes: UI exposure depends on `Exports:DocxEnabled`.
  - EPUB
    - Status: Partially implemented
    - Files: `Application/Exporting/EpubExportRenderer.cs`
    - Notes: UI exposure depends on `Exports:EpubEnabled`; code contains TODO for embedded images.

- Supported export kinds
  - Document export
    - Status: Implemented
    - Files: `Application/Exporting/ExportKind.cs`, `Controllers/DocumentExportController.cs`
  - Synopsis export
    - Status: Implemented
    - Files: `Application/Exporting/SynopsisHtmlExportRenderer.cs`, `Application/Exporting/SynopsisMarkdownExportRenderer.cs`, `Application/Exporting/SynopsisDocxExportRenderer.cs`

- Export flows
  - GET export endpoint
    - What it does: Basic export by kind/format/template
    - Files: `Controllers/DocumentExportController.cs`
    - Status: Implemented
  - POST export endpoint
    - What it does: Advanced export with content scope, title page, TOC, template, preset-backed options
    - Files: `Controllers/DocumentExportController.cs`, `WriterApp.Client/Pages/DocumentEditor.razor.cs`
    - Status: Implemented
  - Print export endpoint
    - What it does: Returns print HTML payload
    - Files: `Controllers/DocumentExportController.cs`
    - Status: Implemented
  - Preview endpoint
    - What it does: Returns preview HTML for export dialog
    - Files: `Controllers/ExportPreviewController.cs`
    - Status: Implemented

- Export customization
  - Export templates
    - What it does: Custom page size, margins, typography, header/footer, page numbers, TOC depth
    - Files: `Controllers/ExportTemplatesController.cs`, `Data/Exporting/ExportTemplate.cs`, `WriterApp.Client/Pages/DocumentEditor.razor`
    - Status: Implemented
  - Export presets
    - What it does: Reusable export settings and project default presets
    - Files: `Controllers/ExportPresetsController.cs`, `Controllers/DocumentExportSettingsController.cs`, `Data/Exporting/ExportPreset.cs`, `Data/Exporting/ProjectExportSettings.cs`
    - Status: Implemented

- Outline / numbering / order influence on export
  - What it does: Uses outline order resolution and heading counters to shape rendered order and numbering.
  - Files: `Application/Exporting/OutlineOrderResolver.cs`, `Application/Documents/HeadingPrefixCountersService.cs`, `Controllers/DocumentExportController.cs`
  - Status: Implemented

## 6. Subscription / monetization capabilities

- Plans
  - Free
    - Current code evidence: seeded plan with no AI, no PDF export, no cover image generation, limited history
    - Files: `Data/AppDbContext.cs`, `Application/Subscriptions/UserEntitlementDefaults.cs`
    - Status: Implemented
  - Standard
    - Current code evidence: AI enabled, PDF export enabled, higher token budget, history retention
    - Files: `Data/AppDbContext.cs`, `Application/Subscriptions/UserEntitlementDefaults.cs`
    - Status: Implemented
  - Professional
    - Current code evidence: highest AI budget, cover image generation entitlement, PDF export, history retention
    - Files: `Data/AppDbContext.cs`, `Application/Subscriptions/UserEntitlementDefaults.cs`
    - Status: Implemented

- Entitlements and quotas
  - What it does: Resolves per-user plan, token budget, usage, feature gates, and history policy.
  - Files: `Application/Subscriptions/EntitlementService.cs`, `Application/Subscriptions/UserEntitlementStore.cs`, `Application/Usage/AiQuotaService.cs`, `Application/Documents/VersionHistoryPolicyService.cs`
  - Status: Implemented

- Stripe checkout
  - What it does: Creates checkout sessions for Standard / Pro plans.
  - Files: `Controllers/BillingController.cs`, `Application/Billing/StripeApiClient.cs`, `Application/Billing/StripePriceResolver.cs`
  - Status: Implemented

- Stripe billing portal
  - What it does: Opens customer portal for existing subscription management.
  - Files: `Controllers/BillingController.cs`
  - Status: Implemented

- Upgrade URL helper flow
  - What it does: Returns either portal or checkout URL depending on current subscription state.
  - Files: `Controllers/BillingController.cs`, `WriterApp.Client/Pages/Upgrade.razor`
  - Status: Implemented

- Checkout finalization and entitlement sync
  - What it does: Finalizes billing success, refreshes auth/account state, and syncs entitlements after checkout.
  - Files: `WriterApp.Client/Pages/BillingCheckout.razor`, `Controllers/BillingController.cs`
  - Status: Implemented

- Stripe webhooks
  - What it does: Verifies signatures, logs processed events, handles checkout/subscription/invoice events, and syncs entitlements.
  - Files: `Controllers/StripeWebhookController.cs`, `Application/Billing/StripeEntitlementSyncService.cs`
  - Status: Implemented

- Admin subscription override and support tooling
  - What it does: Allows manual plan override and Stripe resync by admin/support staff.
  - Files: `Application/Subscriptions/AdminPlanOverrideService.cs`, `Program.cs`, `Application/Users/AdminUsersService.cs`
  - Status: Implemented

## 7. Architecture-backed product capabilities

- Capability: Search indexing and rebuild queue
  - What it does: Maintains searchable project index and background backfill queue/hosted service.
  - Files: `Application/Search/SearchIndexService.cs`, `Program.cs`
  - Status: Implemented

- Capability: Version history policy and snapshots
  - What it does: Creates timed or reason-based page checkpoints, restores versions, and computes diffs.
  - Files: `Application/Documents/VersionHistoryService.cs`, `Application/Documents/PageVersionDiffService.cs`, `Controllers/PageVersionsController.cs`
  - Status: Implemented

- Capability: Autosave and checkpointing
  - What it does: Coordinates editor saves and creates checkpoints when due.
  - Files: `WriterApp.Client/Services/EditorSaveCoordinator.cs`, `Controllers/PagesController.cs`, `Application/Documents/VersionHistoryService.cs`
  - Status: Implemented

- Capability: Background jobs
  - What it does: Runs search index backfill hosted service and optional startup scene-content backfill.
  - Files: `Program.cs`, `Application/Search/SearchIndexBackfillHostedService.cs`, `Application/Documents/SceneContentBackfillService.cs`
  - Status: Implemented

- Capability: Auth integration
  - What it does: Supports EasyAuth, fake auth, local dev auth, and admin bootstrap logic.
  - Files: `Program.cs`, `Application/Security/*`
  - Status: Implemented

- Capability: Database provider flexibility
  - What it does: Supports SQLite and SQL Server with provider-specific startup and retry behavior.
  - Files: `Program.cs`, `Data/AppDbContext.cs`, `Migrations/`, `MigrationsSqlServer/`
  - Status: Implemented

- Capability: Migration support
  - What it does: Supports startup migration, admin migration endpoint, and separate SQL Server migrations context.
  - Files: `Program.cs`, `Migrations/`, `MigrationsSqlServer/`
  - Status: Implemented

- Capability: Caching
  - What it does: Uses memory cache for entitlements and client-side state stores for current document/project/auth context.
  - Files: `Application/Subscriptions/EntitlementService.cs`, `Program.cs`, `WriterApp.Client/State/*`
  - Status: Implemented

- Capability: Logging and diagnostics
  - What it does: Logs admin decisions, circuit events, AI request outcomes, search behavior, drag/drop diagnostics, and webhook events.
  - Files: `Program.cs`, `Application/Diagnostics/*`, `WriterApp.Client/Diagnostics/SectionReorderDiagnostics.cs`, controller logging
  - Status: Implemented

- Capability: Retry and resilience helpers
  - What it does: Uses SQL Server retry-on-failure and targeted cleanup/retry logic in some AI/export flows.
  - Files: `Program.cs`, `Application/Continuity/BiblePatchApplier.cs`
  - Status: Implemented

- Capability: Document/project deletion cleanup
  - What it does: Removes dependent entities including search index entries and AI history during project deletion.
  - Files: `Application/Documents/ProjectDeletionService.cs`, `Application/Documents/DocumentLifecycleService.cs`
  - Status: Implemented

## 8. Hidden, partial, or in-progress features

- Feature: Revise tool family behind AI feature flags
  - Evidence: `Program.cs` gates `Tighten*`, `Expand*`, `ChangeTone*`, `ShowDontTell*` on `AI:ReviseToolsEnabled`
  - Status: Stub / placeholder / hidden behind flag
  - Notes: Some client presets exist, but availability depends on registration.

- Feature: Generate outline from synopsis behind feature flag
  - Evidence: `Program.cs` gates `GenerateOutlineFromSynopsisAction`
  - Status: Stub / placeholder / hidden behind flag

- Feature: Continuity coach fixes behind separate feature flag
  - Evidence: `Program.cs` gates `ApplyContinuityFixAction`
  - Status: Partially implemented
  - Notes: UI and controller logic exist.

- Feature: Prompt Library custom transform behind feature flag
  - Evidence: `Program.cs` gates `CustomTransformAction`
  - Status: Partially implemented
  - Notes: Prompt preset CRUD UI exists regardless; custom execution depends on registration.

- Feature: Projects workflow feature gating
  - Evidence: `Controllers/ProjectsController.cs` and client pages check `Workflow:ProjectsEnabled`
  - Status: Stub / placeholder / hidden behind flag
  - Notes: Large parts of the app depend on this workflow being enabled.

- Feature: Goals / progress workflow feature gating
  - Evidence: `Controllers/ProjectsController.cs`, `Application/Documents/ProjectGoalsService.cs`, `WriterApp.Client/Pages/Projects.razor`
  - Status: Stub / placeholder / hidden behind flag

- Feature: Outline templates feature gating
  - Evidence: `Controllers/OutlineTemplatesController.cs`, `WriterApp.Client/Pages/Projects.razor`
  - Status: Stub / placeholder / hidden behind flag

- Feature: Outline undo / board-related behavior
  - Evidence: `Controllers/DocumentOutlineController.cs`, `Controllers/SectionSceneCardsController.cs`, feature flags for `Workflow:OutlineUndoEnabled` and `Workflow:OutlineBoardEnabled`
  - Status: Partially implemented
  - Notes: Backend support exists; current UI surface is narrower than the backend capability set.

- Feature: Scene annotations endpoint
  - Evidence: `Controllers/SceneAnnotationsController.cs`
  - Status: Partially implemented
  - Notes: Current client evidence is page annotations, not scene annotation UI.

- Feature: Scene quality checks endpoint
  - Evidence: `Controllers/SceneQualityChecksController.cs`
  - Status: Partially implemented
  - Notes: Current client clearly uses page quality checks; scene-level route exists but is not clearly surfaced.

- Feature: Scene versions endpoint
  - Evidence: `Controllers/SceneVersionsController.cs`
  - Status: Partially implemented
  - Notes: Current client uses page version history, not scene version UI.

- Feature: Glossary endpoint
  - Evidence: `Controllers/DocumentGlossaryController.cs`
  - Status: Partially implemented
  - Notes: Backend exists; no clear current client UI was found in `WriterApp.Client`.

- Feature: EPUB export image embedding
  - Evidence: TODO in `Application/Exporting/EpubExportRenderer.cs`
  - Status: Partially implemented

- Feature: Markdown export fidelity
  - Evidence: TODO in `Application/Exporting/ExportHelpers.cs` about headings/lists/inline marks
  - Status: Partially implemented

- Feature: Cover image generation
  - Evidence: Action exists and entitlement exists, but current product exposure is limited / inconsistent
  - Status: Partially implemented

- Feature: Legacy Blazor server pages
  - Evidence: `Components/Pages/*` contains older pages for editor, synopsis, landing, and admin
  - Status: Dormant / legacy
  - Notes: These appear to be older implementations coexisting with the current `WriterApp.Client` app.

## 9. Feature map by file area

- Client pages/components
  - `WriterApp.Client/Pages`
    - End-user routes: documents, projects, document editor, synopsis, onboarding, account, billing, auth
  - `WriterApp.Client/Components`
    - Editor chrome, AI translation proposal panel, search, coach cards, onboarding overlay
  - `WriterApp.Client/State`
    - Current document/project/scene/auth/onboarding layout state
  - `WriterApp.Client/Services`
    - Onboarding API client, save coordination, recovery drafts, auth helpers

- Server API surface
  - `Controllers/DocumentsController.cs`
    - Document CRUD, lifecycle, translations, heading outline
  - `Controllers/SectionsController.cs`
    - Section CRUD, duplicate, reorder, import, translations
  - `Controllers/PagesController.cs`
    - Page CRUD, move, notes
  - `Controllers/ProjectsController.cs`
    - Project CRUD, tree, nodes, manuscript mapping, progress, goals, sessions
  - `Controllers/AiActionsController.cs`
    - AI actions, history, undo/redo, execution
  - `Controllers/BillingController.cs`, `Controllers/StripeWebhookController.cs`
    - Billing, checkout, portal, webhooks, entitlement sync
  - `Controllers/SearchController.cs`
    - Search and rebuild

- Application services
  - `Application/Documents`
    - Version history, diffing, quality checks, goals, scene linking, deletion
  - `Application/Continuity`
    - Bible refresh and patching
  - `Application/Exporting`
    - Export renderers, preview, templates, presets
  - `Application/Subscriptions`
    - Entitlements, plan assignment, override, plan repository usage
  - `Application/Usage`
    - AI quota and usage status
  - `Application/Users`
    - Admin users, audit, user events
  - `Application/Search`
    - Search index service and backfill

- AI folder
  - `AI/Actions`
    - User-facing AI capabilities and action definitions
  - `AI/Providers`
    - Mock and OpenAI providers
  - `Application/AI`
    - AI history persistence and story coach helpers

- Data / persistence
  - `Data/*`
    - EF Core context, plan data, exporting data, subscriptions, document records
  - `Migrations/*`, `MigrationsSqlServer/*`
    - Schema evolution and dual-provider support

- Legacy / duplicate app surfaces
  - `Components/Pages/*`
    - Older Blazor server UI for editor, landing, synopsis, admin

## 10. Gaps / inconsistencies discovered

- Duplicate app surfaces exist
  - `WriterApp.Client` is the current app, but older `Components/Pages` implementations remain in the repo.

- Duplicate or conflicting route ownership around `/documents`
  - `WriterApp.Client/Pages/Documents.razor` and `WriterApp.Client/Pages/DocumentsList.razor` both declare `@page "/documents"`.
  - This strongly suggests stale duplication or unresolved route ownership.

- Product naming is stale across the repo
  - Current branding in layout/logo points to Prosa, but many files, namespaces, and UI strings still say WriterApp or Writer.

- Standard plan token budget is inconsistent in code
  - Seeded plan entitlements use `200000` monthly tokens in `Data/AppDbContext.cs`.
  - Runtime defaults use `250000` in `Application/Subscriptions/UserEntitlementDefaults.cs`.

- Some backend features do not have an obvious current client surface
  - Scene annotations
  - Scene quality checks
  - Scene versions
  - Glossary
  - Some outline board / undo related behavior

- Some client surfaces depend heavily on feature registration or flags
  - Revise tools
  - Prompt library custom transform
  - Continuity fixes
  - Outline templates
  - Goals
  - Projects workflow

- Export support is broader than current polish level
  - EPUB renderer exists, but image embedding is still TODO.
  - Markdown export has TODO notes about richer mapping fidelity.

- Cover image generation appears product-incomplete
  - Entitlement and action exist, but current exposure is inconsistent and legacy labels still call it mock in older UI.

## 11. Recommended next cleanup step

Short prioritized cleanup plan:

1. Decide and document the single source of truth for UI surfaces.
   - Remove or clearly quarantine legacy `Components/Pages` implementations.

2. Resolve route duplication and stale naming.
   - Fix `/documents` duplication and standardize Prosa vs WriterApp naming in visible UI.

3. Add a small, maintained feature registry.
   - Create one machine-readable manifest per area with route, feature flag, maturity, and owner file paths.

4. Align monetization data.
   - Fix the Standard token-budget mismatch between seeded plan data and runtime defaults.

5. Mark backend-only features explicitly.
   - Add a “no current UI” note near scene annotations, scene versions, glossary, and related endpoints so inventory drift is reduced.
