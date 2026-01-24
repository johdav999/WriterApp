# WriterApp Context and Architecture

This file summarizes the current WriterApp editor architecture and UI rules for Codex prompts.

## UX and Layout Principles
- Writing is the primary activity; editor surface should dominate.
- Panels (left sections, right context drawer) are collapsible and must not change document state.
- Focus Mode hides non-essential UI and collapses panels.
- Document title is a single source of truth in metadata; section titles are independent.
- Manuscript width constrains ONLY the text content column; layout columns remain full width.
- Full-width mode removes max-width constraints between browser edge and editor content.
- Status bar is minimal (word count + save state).

## Layout Structure (Home.razor)
EditorShell (full width)
  EditorWorkspace (grid: sections panel, editor column, context drawer)
    Sections panel (left)
    Editor column (center)
      EditorSurface (visual canvas, no card chrome)
        EditorViewport (full width, centers content)
          EditorContent (ONLY max-width in manuscript mode)
            TipTap root (SectionEditor)
    Context drawer (right, tabs: Notes / Outline / AI)

## State and Persistence
- Layout state lives in `Application/State/LayoutState.cs`.
  - Fields: FocusMode, LeftNavCollapsed, SectionsCollapsed, ContextCollapsed,
    ManuscriptWidthMode, EditorZoomPercent.
  - Persisted in localStorage via `LayoutStateService` under `writerapp.layout.v1`.
- Document state managed by `DocumentState` + `CommandProcessor`.
- Autosave + manual save flow in `Components/Pages/Home.razor`.

## Editor Content and Zoom
- Zoom is UI-only (font-size scale) via CSS variable `--editor-font-scale`.
- Manuscript width uses `--editor-max-width` on `.editor-content`.
- Full width sets `--editor-max-width: none`.

## Right Context Drawer
- Tabs: Notes (per-section, stored on `Section.Notes`),
  Outline (document/section list), AI (actions + AI change history).
- Drawer visibility depends only on `LayoutState.ContextCollapsed` or Focus Mode.

## AI Change History (Safety Feature)
- Domain: `Domain/Documents/AIHistoryEntry.cs` stored in `Section.AI.AIHistory`.
- Append entries on AI apply via `AI/Core/DefaultProposalApplier.cs`.
- Rollback uses `CommandProcessor.RollbackAiEditGroup` and is section-aware.
- UI renders history in AI tab with details + revert; uses `AffectedSectionId`.

## Key Files
- `Components/Pages/Home.razor`: editor page, layout, toolbar, drawer, autosave, AI history UI.
- `Application/State/LayoutState.cs` + `LayoutStateService.cs`: layout persistence.
- `Domain/Documents/Section.cs`: section model, includes `Notes`.
- `AI/Core/DefaultProposalApplier.cs`: AI apply + history entry.
- `Application/Commands/CommandProcessor.cs`: undo/redo, AI rollback, history append.

## Constraints / Gotchas
- Do NOT introduce another document title field in the editor column.
- Manuscript width must not affect layout columns or right drawer visibility.
- Avoid adding telemetry (AI usage/plan) to persistent UI; keep it secondary/collapsible.
