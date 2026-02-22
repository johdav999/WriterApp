# Outline Refactor Analysis

## Executive summary

Current structure/outline behavior is split across multiple models and UI surfaces:

- Project structure model: `ProjectNodeRecord` (`Part/Chapter/Scene`) via Projects Navigator UI.
- Document outline model: `DocumentOutlineNodeRecord` (document-local tree) via editor right panel `Story -> Outline`.
- Document section model: `SectionRecord` + `PageRecord` via editor left panel.

This creates two independent hierarchies for similar concepts (project tree vs document outline tree), with extra sync bridges:

- Project scene linking to sections (`ProjectSceneLinkingService`) on create/patch/open scene.
- Document-outline-to-sections sync (`apply-to-sections`).
- Template apply in Projects UI currently writes to document outline first, then mirrors to project nodes with heuristic node-type inference.

Key consequence: there is no single source of truth for structure today. Different screens mutate different tables and sync opportunistically.

## Scope and stack clarification

- Active WASM app routes: `Program.cs:406`, `Program.cs:410`, `Program.cs:484`, `Program.cs:644`, `Program.cs:677`, `Program.cs:691`.
- WASM pages analyzed here:
  - `WriterApp.Client/Pages/Projects.razor`
  - `WriterApp.Client/Pages/DocumentEditor.razor`
  - `WriterApp.Client/Pages/DocumentEditor.razor.cs`
- Parallel server-rendered pages also exist and contain similar outline logic:
  - `Components/Pages/DocumentEditor.razor:1`
  - `Components/Pages/DocumentEditor.razor.cs:2821`
  - `Components/Pages/Home.razor:311`, `Components/Pages/Home.razor:1580`

Ambiguity to resolve before implementation: whether refactor targets WASM only or both UI stacks.

## Step A: UI inventory (entry points)

| UI surface | Component(s) | Data source and state | Mutations in UI logic | IDs used | API calls |
|---|---|---|---|---|---|
| Projects Navigator tree | `WriterApp.Client/Pages/Projects.razor:1` | Loads projects + active project tree into `_projects`, `_nodes` (`Projects.razor:609`, `Projects.razor:656`) | Add node (`Projects.razor:2020`), rename (`Projects.razor:1143`), delete (`Projects.razor:1371`), reorder (`Projects.razor:2140`, `Projects.razor:2212`), open scene (`Projects.razor:2264`) | `projectId`, `nodeId`, `parentId`, `linkedSectionId` | `GET api/projects` (`Projects.razor:615`), `GET api/projects/{id}/tree` (`Projects.razor:659`), `POST api/projects/{id}/nodes` (`Projects.razor:2028`), `PATCH api/projects/{id}/nodes/{nodeId}` (`Projects.razor:1170`), `POST api/projects/{id}/nodes/{parentOrEmpty}/reorder` (`Projects.razor:2222`), `DELETE api/projects/{id}/nodes/{nodeId}` (`Projects.razor:1401`), scene redirect uses `POST api/projects/{id}/nodes/{nodeId}/open-scene` (`ProjectSceneRedirect.razor:41`) |
| Editor left panel (sections tree) | `WriterApp.Client/Pages/DocumentEditor.razor:27` | Loads `_sections` + `_pagesBySection` (`DocumentEditor.razor.cs:745`, `DocumentEditor.razor.cs:754`) | Create section (`DocumentEditor.razor.cs:1644`), rename (`DocumentEditor.razor.cs:1001`), reorder (`DocumentEditor.razor.cs:1535`), delete (`DocumentEditor.razor.cs:1223`), duplicate (`DocumentEditor.razor.cs:1049`) | `documentId`, `sectionId` | `GET api/documents/{documentId}/sections` (`DocumentEditor.razor.cs:746`), `GET api/sections/{sectionId}/pages` (`DocumentEditor.razor.cs:755`), `POST api/documents/{documentId}/sections` (`DocumentEditor.razor.cs:1665`), `PUT api/documents/{documentId}/sections/{sectionId}` (`DocumentEditor.razor.cs:1001`), `POST api/documents/{documentId}/sections/reorder` (`DocumentEditor.razor.cs:1547`), `DELETE api/documents/{documentId}/sections/{sectionId}` (`DocumentEditor.razor.cs:1237`), `POST api/documents/{documentId}/sections/{sectionId}/duplicate` (`DocumentEditor.razor.cs:1056`) |
| Editor right panel Story -> Outline | `WriterApp.Client/Pages/DocumentEditor.razor:991` | Loads `_outlineNodes` (`DocumentEditor.razor.cs:3755`) | Create/rename/reorder/delete are client-side tree edits then full save (`DocumentEditor.razor.cs:4446`, `DocumentEditor.razor.cs:4546`, `DocumentEditor.razor.cs:4384`, `DocumentEditor.razor.cs:4475`, `DocumentEditor.razor.cs:4652`), link section (`DocumentEditor.razor.cs:4592`), apply to sections (`DocumentEditor.razor.cs:4826`) | `documentId`, `nodeId`, `parentId`, `linkedSectionId` | `GET api/documents/{documentId}/outline/nodes` (`DocumentEditor.razor.cs:3768`), `PUT api/documents/{documentId}/outline/nodes` (`DocumentEditor.razor.cs:4658`), `POST api/documents/{documentId}/outline/nodes/{nodeId}/link-section` (`DocumentEditor.razor.cs:4606`), `PUT api/documents/{documentId}/outline/nodes/{nodeId}/metadata` (`DocumentEditor.razor.cs:3850`), `POST api/documents/{documentId}/outline/apply-to-sections` (`DocumentEditor.razor.cs:4846`), undo/redo (`DocumentEditor.razor.cs:3985`, `DocumentEditor.razor.cs:4031`) |

## Step B: logic and API wiring

### Service/client wrappers

- Outline templates client wrapper:
  - `WriterApp.Shared/OutlineTemplatesClient.cs:11`
  - `GetTemplatesAsync` (`OutlineTemplatesClient.cs:20`)
  - `CreateTemplateAsync` (`OutlineTemplatesClient.cs:33`)
  - `DeleteTemplateAsync` (`OutlineTemplatesClient.cs:40`)
  - `ApplyTemplateAsync` (`OutlineTemplatesClient.cs:45`)
- Projects and document outline operations are mostly direct `HttpClient` calls inside Razor code-behind (no dedicated typed client).

### Feature flags/gates in current flow

- Projects workflow gate in controller: `ProjectsController.IsEnabled` (`Controllers/ProjectsController.cs:1375`), checked on endpoints such as `ProjectsController.cs:54`, `ProjectsController.cs:832`, `ProjectsController.cs:863`.
- Outline templates gate in controller: `OutlineTemplatesController.RejectIfDisabled` (`Controllers/OutlineTemplatesController.cs:358`) + `IsEnabled` (`Controllers/OutlineTemplatesController.cs:546`) using `WriterApp:Workflow:OutlineTemplatesEnabled` (`OutlineTemplatesController.cs:548`).
- Document outline endpoints do not have a global outline-enabled gate; only undo/redo gating exists (`DocumentOutlineController.cs:398`, `DocumentOutlineController.cs:447`, `DocumentOutlineController.cs:843`).

### Sequence diagrams (text)

#### Projects Navigator: list structure

```text
Browser (/app/projects)
  -> Projects.razor OnInitializedAsync (WriterApp.Client/Pages/Projects.razor:573)
  -> GET api/projects (Projects.razor:615)
  -> ProjectsController.ListProjects (Controllers/ProjectsController.cs:51)
  -> GET api/projects/{projectId}/tree (Projects.razor:659)
  -> ProjectsController.GetTree (ProjectsController.cs:829)
  <- ProjectTreeDto (Project + ProjectNodeDto[])
```

#### Projects Navigator: create node

```text
UI Add (Projects.razor:2020)
  -> POST api/projects/{projectId}/nodes (Projects.razor:2028)
  -> ProjectsController.CreateNode (ProjectsController.cs:857)
  -> if scene, ensure section link via ProjectSceneLinkingService (ProjectsController.cs:934, Application/Documents/ProjectSceneLinkingService.cs:77)
  <- ProjectNodeDto
  -> Refresh tree (Projects.razor:2041)
```

#### Projects Navigator: rename node

```text
Inline rename save (Projects.razor:1143)
  -> PATCH api/projects/{projectId}/nodes/{nodeId} (Projects.razor:1170)
  -> ProjectsController.PatchNode (ProjectsController.cs:945)
  -> if scene, ensure link (ProjectsController.cs:1024)
  <- ProjectNodeDto
  -> Refresh tree (Projects.razor:1179)
```

#### Projects Navigator: reorder node

```text
Drag/drop (Projects.razor:2140)
  -> POST api/projects/{projectId}/nodes/{parentOrEmpty}/reorder (Projects.razor:2222)
  -> ProjectsController.ReorderChildren (ProjectsController.cs:1036)
  <- reordered ProjectNodeDto[]
  -> Refresh tree (Projects.razor:2209)
```

#### Projects Navigator: delete node

```text
Delete action (Projects.razor:1371)
  -> DELETE api/projects/{projectId}/nodes/{nodeId} (Projects.razor:1401)
  -> ProjectsController.DeleteNode (ProjectsController.cs:1111)
  <- 204 NoContent
  -> Refresh tree (Projects.razor:1409)
```

#### Projects Navigator: link/open scene

```text
Open scene from project node
  -> navigate to /projects/{projectId}/scenes/{sceneNodeId}/edit (Projects.razor:2270)
  -> ProjectSceneRedirect POST api/projects/{projectId}/nodes/{sceneNodeId}/open-scene (ProjectSceneRedirect.razor:41)
  -> ProjectsController.OpenScene (ProjectsController.cs:1161)
  -> ProjectSceneLinkingService.EnsureSceneLinkedSectionAsync (ProjectsController.cs:1192)
  <- ProjectSceneOpenTargetDto
  -> navigate /documents/{documentId}/sections/{sectionId} (ProjectSceneRedirect.razor:70)
```

#### Editor left panel (sections): list/create/rename/reorder/delete

```text
Load document editor
  -> GET api/documents/{documentId}/sections (DocumentEditor.razor.cs:746)
  -> SectionsController.ListSections (Controllers/SectionsController.cs:66)
  <- SectionDto[]

Create section
  -> POST api/documents/{documentId}/sections (DocumentEditor.razor.cs:1665)
  -> SectionsController.CreateSection (SectionsController.cs:92)
  <- SectionDto

Rename section
  -> PUT api/documents/{documentId}/sections/{sectionId} (DocumentEditor.razor.cs:1001)
  -> SectionsController.UpdateSection (SectionsController.cs:454)
  <- SectionDto

Reorder sections
  -> POST api/documents/{documentId}/sections/reorder (DocumentEditor.razor.cs:1547)
  -> SectionsController.ReorderSections (SectionsController.cs:310)
  <- SectionDto[]

Delete section
  -> DELETE api/documents/{documentId}/sections/{sectionId} (DocumentEditor.razor.cs:1237)
  -> SectionsController.DeleteSection (SectionsController.cs:628)
  <- 204 NoContent
```

#### Editor right Story -> Outline: list/create/rename/reorder/delete/link/apply

```text
List outline nodes
  -> GET api/documents/{documentId}/outline/nodes (DocumentEditor.razor.cs:3768)
  -> DocumentOutlineController.GetOutlineNodes (Controllers/DocumentOutlineController.cs:117)
  <- DocumentOutlineNodeDto[]

Create/rename/reorder/delete nodes
  -> mutate _outlineNodes in client (DocumentEditor.razor.cs:4446, 4546, 4384, 4475)
  -> PUT api/documents/{documentId}/outline/nodes (DocumentEditor.razor.cs:4658)
  -> DocumentOutlineController.UpdateOutlineNodes (DocumentOutlineController.cs:150)
  -> server replaces full node set for document (DocumentOutlineController.cs:201-224)
  <- updated DocumentOutlineNodeDto[]

Link outline node to section
  -> POST api/documents/{documentId}/outline/nodes/{nodeId}/link-section (DocumentEditor.razor.cs:4606)
  -> DocumentOutlineController.LinkSectionToNode (DocumentOutlineController.cs:267)
  <- updated DocumentOutlineNodeDto

Apply outline to sections
  -> POST api/documents/{documentId}/outline/apply-to-sections (DocumentEditor.razor.cs:4846)
  -> DocumentOutlineController.ApplyOutlineToSections (DocumentOutlineController.cs:482)
  <- OutlineApplyResultDto (SectionDto[] + DocumentOutlineNodeDto[])
```

#### Template flow currently touching both models

```text
Projects Templates modal Apply
  -> Ensure manuscript document for project (Projects.razor:1720, 1951)
  -> GET document outline nodes baseline (Projects.razor:1727)
  -> POST api/documents/{docId}/outline/apply-template/{templateId} (Projects.razor:1737)
  -> OutlineTemplatesController.ApplyTemplate (Controllers/OutlineTemplatesController.cs:133)
  -> returns updated DocumentOutlineNodeDto[]
  -> client computes inserted nodes (Projects.razor:1757)
  -> client mirrors inserted outline nodes into ProjectNodes with inferred node type (Projects.razor:1783, 1871)
  -> POST api/projects/{projectId}/nodes for each mirrored node (Projects.razor:1858)
```

## Step C: DB schema map

### Primary tables/entities involved

| Table / Entity | Key columns | Relationships | Used by UI surface(s) |
|---|---|---|---|
| `Projects` / `ProjectRecord` | `Id`, `OwnerUserId`, `UpdatedUtc` | One-to-many to `ProjectNodes` and `Documents` (`Data/AppDbContext.cs:181`, `Data/AppDbContext.cs:185`) | Projects page |
| `ProjectNodes` / `ProjectNodeRecord` | `Id`, `ProjectId`, `ParentId`, `NodeType`, `OrderIndex`, `LinkedSectionId` (`Data/Documents/ProjectNodeRecord.cs:10-27`) | Self-parent tree, optional FK to `Sections` (`Data/AppDbContext.cs:202`, `Data/AppDbContext.cs:206`) | Projects navigator |
| `DocumentOutlineNodes` / `DocumentOutlineNodeRecord` | `Id`, `DocumentId`, `ParentId`, `Order`, `LinkedSectionId`, `MetadataJson` (`Data/Documents/DocumentOutlineNodeRecord.cs:10-29`) | Self-parent tree, optional FK to `Sections` (`Data/AppDbContext.cs:298`, `Data/AppDbContext.cs:302`) | Editor right Story->Outline |
| `DocumentOutlines` / `DocumentOutlineRecord` | `DocumentId`, `Outline`, `UpdatedAt` (`Data/Documents/DocumentOutlineRecord.cs:7-13`) | One-to-one to `Documents` (`Data/AppDbContext.cs:471`) | Legacy/plain-text outline endpoints |
| `Sections` / `SectionRecord` | `Id`, `DocumentId`, `OrderIndex` | One-to-many to `Pages`; linked from both `ProjectNodes` and `DocumentOutlineNodes` | Editor left panel; linkage target for project + outline |
| `Pages` / `PageRecord` | `Id`, `SectionId`, `OrderIndex` | Belongs to section/document | Editor content |
| `OutlineTemplates` / `OutlineTemplateRecord` | `Id`, `OwnerUserId`, `TemplateJson` (`Data/Documents/OutlineTemplateRecord.cs:7-13`) | Per-user template storage | Templates in Projects and editor |
| `SectionSceneCards` / `SectionSceneCardRecord` | `SectionId`, metadata columns (`Data/Documents/SectionSceneCardRecord.cs:7-31`) | One-to-one with `Sections` (`Data/AppDbContext.cs:448`) | Scene metadata enriched from outline apply |

### Migrations introducing relevant schema

- Document plain outline: `Migrations/20260126103000_AddPageNotesOutline.cs:12` (`DocumentOutlines`).
- Document outline nodes: `Migrations/20260127181738_AddDocumentOutlineNodes.cs:14` (`DocumentOutlineNodes`).
- Project model: `Migrations/20260208110000_AddProjectsAndProjectNodes.cs:12` (`Projects`), `:32` (`ProjectNodes`).
- Template + outline metadata: `Migrations/20260208123000_AddOutlineMetadataAndTemplates.cs:12` (node `MetadataJson`), `:55` (`OutlineTemplates`).

### Runtime schema patching in controllers

- Projects controller executes sqlite `CREATE TABLE IF NOT EXISTS` for `Projects` and `ProjectNodes`: `Controllers/ProjectsController.cs:235`, `Controllers/ProjectsController.cs:247`, `Controllers/ProjectsController.cs:261`.
- Document outline controller ensures missing sqlite column `MetadataJson`: `Controllers/DocumentOutlineController.cs:903`.

## Step D: duplication and conflict analysis

## Where concepts are duplicated

- Hierarchical structure duplicated:
  - Project hierarchy in `ProjectNodes`.
  - Document hierarchy in `DocumentOutlineNodes`.
- Linkage duplicated:
  - `ProjectNodes.LinkedSectionId` (`Data/Documents/ProjectNodeRecord.cs:26`).
  - `DocumentOutlineNodes.LinkedSectionId` (`Data/Documents/DocumentOutlineNodeRecord.cs:28`).
- Outline semantics duplicated:
  - Structured nodes (`DocumentOutlineNodes`).
  - Flat outline text (`DocumentOutlines`), derived from nodes in some flows (`DocumentOutlineController.cs:66`, `DocumentOutlineController.cs:226`).

## Source-of-truth statement (current)

- For Projects UI navigation/order: source of truth is `ProjectNodes`.
- For editor right Story->Outline: source of truth is `DocumentOutlineNodes`.
- For editor writing navigation/content: source of truth is `Sections` + `Pages`.
- There is no global canonical structure model shared by all surfaces.

## Conflicts and drift risks

- Template apply path in Projects writes to document outline first, then mirrors into project nodes using heuristics (`Projects.razor:1783`, `Projects.razor:1871`).
- `DocumentOutlineNodeDto` has no `NodeType` (`WriterApp.Shared/NotesOutlineDtos.cs:9`), while project nodes require node type (`WriterApp.Shared/ProjectDtos.cs:33`), forcing inference.
- Editor right outline uses full-replace `PUT` semantics (`DocumentOutlineController.cs:201-224`), while Projects uses granular node operations; concurrent edits can drift.
- Scene linking runs in project flows (`ProjectSceneLinkingService`, `ProjectsController.cs:934`, `ProjectsController.cs:1024`, `ProjectsController.cs:1192`) independently from document-outline link operations (`DocumentOutlineController.cs:267`).
- Parallel UI stacks (`WriterApp.Client/*` and `Components/*`) increase maintenance and behavior divergence risk.

## Step E: phased refactor proposal (plan only)

## Target model

- Single authoritative structure model: `ProjectNodes` (`Part/Chapter/Scene`) as canonical manuscript structure.
- Editor navigation uses section links from project scene nodes.
- Document outline tables remain temporarily for compatibility but stop being primary workflow source.

## Phase 0: instrumentation and safety gates

- Add a new rollout flag, e.g. `WriterApp:Workflow:UnifiedStructureModelEnabled` (optional but recommended).
- Add telemetry around calls to:
  - `api/documents/{id}/outline/*`
  - `api/projects/{id}/nodes/*`
  - `api/documents/{id}/sections/*`
- Confirm active UI stack scope (WASM only vs WASM + server components).
- Add explicit deprecation warnings in server logs for document-outline write endpoints when feature is on.

Checklist:

- [ ] Decide active UI stack scope.
- [ ] Add feature flag and metrics counters.
- [ ] Add endpoint-level logging for deprecated flows.

## Phase 1: UI removal with backend compatibility retained

- Remove editor document-outline surfaces:
  - Remove right panel `Story -> Outline` tab and controls from `WriterApp.Client/Pages/DocumentEditor.razor`.
  - Remove any left-side document-outline UI if present in active stack; keep section list.
- Keep Projects Navigator as the only structure-edit UI.
- Keep old outline endpoints alive but unused.

Checklist:

- [ ] Remove Outline tab/action entry points in editor.
- [ ] Keep editor section panel intact.
- [ ] Remove dead client calls to `api/documents/{id}/outline/*` in active stack.
- [ ] Keep fallback UI/message if deep-link/state refers to removed tab.

## Phase 2: logic consolidation and API reroute

- Introduce a project-structure query endpoint tailored for editor consumption (if needed), e.g. project nodes + resolved section links in one payload.
- Route editor scene navigation from project nodes/open-scene flow rather than document outline nodes.
- Replace template apply mirror hack:
  - Apply template directly to `ProjectNodes` (new project-template apply endpoint), not through document outline.
- Keep section creation/linking behavior in one place: `ProjectSceneLinkingService`.

Checklist:

- [ ] Add read model endpoint for editor navigation from project tree.
- [ ] Add direct project-template-apply endpoint.
- [ ] Remove project UI dependency on `api/documents/{docId}/outline/nodes` baseline diffing.
- [ ] Remove `InferProjectNodeType` heuristic path in Projects UI.

## Phase 3: DB/API cleanup and migration

- Options:
  - Preferred: deprecate `DocumentOutlineNodes` and `DocumentOutlines` for manuscript workflow; keep read-only compatibility window.
  - Optional migration: one-time importer from existing `DocumentOutlineNodes` into `ProjectNodes` for projects lacking tree data.
- Mark or remove deprecated endpoints:
  - `api/documents/{documentId}/outline/nodes` (PUT/POST link/apply).
  - `api/documents/{documentId}/outline` plain text.

Checklist:

- [ ] Decide data migration policy for existing document-outline-only users.
- [ ] Add migration utility and idempotent reconciliation checks.
- [ ] Deprecate/remove outline endpoints after compatibility window.
- [ ] Remove obsolete tables in final migration (optional final step).

## Regression risks and UAT plan

High-risk areas:

- Scene-to-section linking consistency during create/move/rename.
- Project tree reorder correctness and persistence.
- Editor open-scene routing and resume behavior.
- Template apply semantics and insertion parent logic.
- Legacy stack still invoking old outline endpoints.

Recommended UAT:

- [ ] Create part/chapter/scene in Projects; open scene; verify section/page created and editable.
- [ ] Reorder nodes deeply; refresh; verify persisted order.
- [ ] Rename/move scene between chapters; verify linked section remains correct.
- [ ] Apply template at root and under selected node; verify immediate navigator update.
- [ ] Verify editor has no document-outline UI and remains fully usable.
- [ ] Verify old outline endpoints return controlled deprecation response when unified model flag is on (if adopted).

## Notable ambiguities / missing wiring

- Dual implementation exists (`WriterApp.Client/*` and `Components/*`) with similar outline logic; target of removal must be explicit.
- “Editor left outline” currently behaves as sections navigator in WASM (`DocumentEditor.razor:27`), not document-outline tree.
- Templates are currently available in both Projects and editor outline UIs; removing editor outline requires clear ownership of template UX in Projects.
