# Prosa Storyboard Marketing Extract

## What the storyboard is

Prosa includes a dedicated storyboard workspace for visual story planning across chapters and scenes. It is not only a static outline. The storyboard combines a board view, a scene detail inspector, and an insights panel so writers can plan structure, edit scene metadata, and act on AI-backed story guidance from one surface.

Primary implementation references:

- `WriterApp.Client/Pages/Storyboard.razor`
- `WriterApp.Client/Components/Projects/ProjectStoryboard.razor`
- `WriterApp.Client/Components/Projects/StoryboardSceneInspector.razor`
- `WriterApp.Client/Components/Projects/StoryboardInsightsPanel.razor`

## Marketing-ready positioning

### Short product description

Prosa Storyboard turns your manuscript plan into a visual scene board. Arrange scenes by chapter, update story metadata inline, and get structure-level guidance on pacing, POV balance, and missing beats without leaving the board.

### Value pillars

- Visual story architecture
  - See chapters and scenes as a board instead of a plain tree or document list.
- Inline planning workflow
  - Edit scene summaries, status, POV, narrative role, intent, notes, and subplot tags directly from the storyboard.
- Structural intelligence
  - Surface incomplete scenes, draft-heavy chapters, subplot continuity risks, POV imbalance, and next-scene opportunities.
- Actionable AI
  - Generate scene-level suggestions and create proposed scenes from board-level recommendations.
- Fast restructuring
  - Move, reorder, duplicate, and manage scenes across chapters with drag-and-drop and bulk actions.

## Core storyboard capabilities

### 1. Visual chapter-and-scene board

- Displays the project as chapter columns with scene cards
- Shows chapter-level signals such as scene count, draft count, and dominant POV
- Supports horizontal board navigation across the manuscript
- Provides empty-state onboarding for projects without scenes

Code references:

- `WriterApp.Client/Pages/Storyboard.razor`
- `WriterApp.Client/Components/Projects/ProjectStoryboard.razor`

### 2. Scene cards with planning metadata

Each scene card can expose:

- title
- summary
- word count
- status
- POV
- subplot tags
- narrative purpose / role
- narrative intent
- notes indicator
- updated timestamp

This makes the board useful for planning, not just navigation.

Code references:

- `WriterApp.Client/Components/Projects/ProjectStoryboard.razor`

### 3. Drag-and-drop story restructuring

- Reorder scenes within a chapter
- Move scenes between chapters
- Apply optimistic updates so the board responds immediately
- Refresh after structural changes to keep the board in sync

Code references:

- `WriterApp.Client/Components/Projects/ProjectStoryboard.razor`

### 4. Bulk scene management

When multiple scenes are selected, Prosa exposes bulk actions for:

- status
- narrative role
- subplot tag add
- subplot tag remove

This is useful for reclassifying large story sections quickly during planning and revision.

Code references:

- `WriterApp.Client/Components/Projects/ProjectStoryboard.razor`

### 5. Filters and visual analysis modes

The board supports filtering by:

- POV
- status
- subplot

It also supports color-coding scenes by:

- none
- POV
- subplot
- status

This helps writers isolate structure patterns and spot distribution issues faster.

Code references:

- `WriterApp.Client/Components/Projects/ProjectStoryboard.razor`

### 6. Inline scene inspector

Writers can select a scene and edit it without leaving the storyboard. The inspector includes:

- title
- summary
- notes
- narrative role
- narrative intent
- POV
- subplot tags
- emotional beat
- key events
- open questions
- status

It also provides direct actions to:

- open scene
- reveal in navigator
- duplicate scene
- delete scene

Code references:

- `WriterApp.Client/Components/Projects/StoryboardSceneInspector.razor`

### 7. Autosaving storyboard metadata

Scene-card fields and notes are persisted from the inspector, which makes the storyboard a working planning surface rather than a temporary overlay.

Code references:

- `WriterApp.Client/Components/Projects/StoryboardSceneInspector.razor`

### 8. Board-level insights

The storyboard insights panel summarizes board health and structure using:

- total chapters
- total scenes
- draft scenes
- incomplete scenes
- observations
- higher-level insights
- board-wide suggestions

It also surfaces grouped findings for:

- subplot continuity
- POV distribution
- POV balance issues

Code references:

- `WriterApp.Client/Components/Projects/StoryboardInsightsPanel.razor`

### 9. AI suggestions for story structure

At the storyboard level, Prosa can:

- suggest the next scene
- detect likely missing scenes
- analyze subplot continuity
- analyze POV balance

Suggested scenes can be created directly into the board.

Code references:

- `WriterApp.Client/Components/Projects/StoryboardInsightsPanel.razor`

### 10. AI help inside a selected scene

Within the scene inspector, Prosa can help generate or refine scene-card content, including:

- generate summary
- suggest scene role and intent
- improve structure

Suggestions are previewed before apply.

Code references:

- `WriterApp.Client/Components/Projects/StoryboardSceneInspector.razor`

## Message angles for marketing

### Angle: from outline to living story board

Prosa gives writers a live storyboard where chapters, scenes, and story signals stay connected. Instead of managing a static outline in one place and scene notes in another, writers can restructure the story, update metadata, and inspect scene logic from a single board.

### Angle: planning with structural visibility

The storyboard is built to reveal pacing and structure at a glance. Writers can filter by POV, subplot, or status, then scan chapter balance, draft density, and incomplete scenes before they become manuscript problems.

### Angle: AI that works at board level, not only paragraph level

Prosa’s storyboard AI is aimed at story architecture. It can suggest the next scene, flag likely missing scenes, check subplot continuity, and identify POV imbalance, then turn those recommendations into actionable scene additions.

### Angle: edit the plan without leaving the board

The scene inspector makes the storyboard editable. Writers can update summaries, role, intent, notes, emotional beats, and open questions right beside the board, keeping planning fast and focused.

## Suggested website copy

### Headline options

- Storyboard your manuscript like a working story system
- See your story structure before it breaks
- Plan scenes visually, then refine them in place

### Subheadline options

- Organize chapters and scenes on a visual board, edit scene metadata inline, and get AI help for pacing, POV balance, and missing story beats.
- Move from outline to execution with a storyboard that lets you reorganize scenes, track structural intent, and spot weak links across the manuscript.

### Feature bullets

- Visual chapter-and-scene board with drag-and-drop reordering
- Inline scene editor for summary, POV, role, intent, notes, and story beats
- Filters and color modes for status, subplot, and point of view
- Bulk updates across multiple scenes
- AI suggestions for next scenes, missing beats, subplot continuity, and POV balance

## Positioning note

The storyboard feature is currently gated as a higher-tier capability in the app via `FeatureKey.ProjectStoryboard`, with additional AI actions gated separately through story-coach and scene-suggestion entitlements. For marketing, it is best positioned as a premium planning workflow rather than a basic outline view.
