# Editor UX Tech Plan

This document defines the authoritative UX architecture and implementation plan for the WriterApp editor experience. It is the source of truth for future editor-related work. It is not used at runtime.

## 1) UX Principles (Writer Perspective)
- Writing is the primary activity; editing text is the default focus.
- Structure/navigation must not compete with the text surface.
- Metadata, AI usage, plans, and export tools are secondary and collapsible.
- Minimize vertical chrome above the editor to maximize writing space.

## 2) Current Layout Inventory (Brief)
- Top bar / app nav: layout shell and app-level routing.
- Sections panel: left column list for section reorder and selection.
- Editor column: main writing surface, toolbar, TipTap editor instance.
- Context panel: right column for outline, AI usage, AI history, settings.
- Status bar: bottom area for word count, save state, AI status.
- Recovery banner: transient banner for unsaved changes recovery.

## 3) Target Layout Model
- Focus / Write mode
  - Definition: a mode that hides non-essential UI to prioritize the editor surface.
  - Behavior: collapse left sections panel and right context panel; keep only editor, compact toolbar, and minimal status.
  - Exit: toggle button and Esc shortcut (if not conflicting with editor).

- Collapsible panels
  - Left nav: app/global navigation (if present) collapses independently.
  - Sections panel: collapsible on demand; default open in normal mode.
  - Right context panel: collapsible on demand; default open in normal mode.
  - Panel collapse must not change document state; only layout state.

- Single source of truth for document title
  - Document title stored once in Document metadata.
  - Any title field edits update the same underlying value.
  - Section titles are independent and never overwrite document title.

- Compact toolbar + selection bubble menu
  - Toolbar: keep a compact, single-row primary formatting strip.
  - Secondary actions move to a selection bubble menu (contextual).
  - Bubble menu appears near selection; it should not shift layout.

- Manuscript width vs full width
  - Manuscript width: fixed readable column (default).
  - Full width: expand editor to available width when desired.
  - Toggle stored in layout state, not in document content.

- Minimal status bar
  - Show save state + word count in a compact footer.
  - Additional telemetry (AI, plans, export) is secondary and can be hidden in focus mode.

## 4) Shared Layout State
- LayoutState model (fields, defaults)
  - IsFocusMode: false
  - IsLeftNavCollapsed: false
  - IsSectionsPanelCollapsed: false
  - IsContextPanelCollapsed: false
  - IsPreviewOpen: false (if preview exists)
  - EditorWidthMode: Manuscript (default) | Full
  - ZoomPercent: 100 (visual zoom only)
  - ToolbarDensity: Compact (default) | Expanded

- Persistence rules
  - Stored in localStorage under key: writerapp.layout.v1
  - Versioned payload with a schemaVersion field.
  - On mismatch or parse failure: fallback to defaults.

- Zoom vs semantic formatting distinction
  - Zoom is a UI-only scale of the editor surface.
  - Font size/line spacing are semantic formatting stored in content or document settings.
  - Zoom changes must never mutate content or document settings.

## 5) Section Model Assumptions
- Sections are edited independently (one editor instance per active section).
- Reordering sections never mutates section content.
- Section titles are per-section; document title is global and separate.

## 6) Non-Goals and Constraints
- No editor rewrite.
- TipTap remains the editor.
- No runtime dependency on this file.

## 7) Implementation Sequence
1) Add LayoutState model + localStorage persistence (writerapp.layout.v1).
2) Introduce focus/write mode toggle; collapse panels and reduce status bar.
3) Make sections and context panels independently collapsible with remembered state.
4) Consolidate document title edits to a single source of truth.
5) Implement compact toolbar and move secondary actions to a selection bubble menu.
6) Add editor width mode (manuscript vs full width) and zoom controls.
7) Reduce status bar to essential items; move secondary info to panels.
