# Desktop storyboard

Implemented 2026-09-30. Open **Projects → project → Storyboard**, or use the Storyboard link in the manuscript editor. The previous `/projects/{id}/planning` storyboard entry redirects to the dedicated board; Synopsis and Notes & Tasks keep their editor destinations.

## Implementation

- The web client and desktop use the same `StoryboardBoard`, `SceneCard`, and `StoryboardInsights` presentation. Web plan-gated buttons retain their existing behavior through the client button adapter.
- Desktop chapter/scene creation, renaming, drag/drop, filters, colors, multi-selection, bulk status/role/tag updates, duplication, and recoverable deletion use the local manuscript aggregate. Parts retain their chapter columns. Cross-chapter drag/drop saves the move and destination order together.
- Scene Detail uses the shared planning fields and desktop draft protection. Keyboard-accessible move buttons and a chapter selector provide alternatives to drag/drop. Open scene targets the scene's first page; Reveal in navigator selects its node and expands its ancestors.
- Summary, role/intent, and structure coaching use the existing `scene.suggest` and `scene.refine` backend actions with review before Apply. Board insights expose next-scene suggestions, missing-scene detection, subplot continuity, and POV balance through existing backend actions.
- AI uses the signed-in device service, synchronized cloud identities, and document revision checks. Local edits work offline. Expired, changed-account, and stale-board suggestions are rejected before scene creation.

## Automated evidence

Desktop and client builds passed. Desktop validation used `WriterApp.Desktop/bin/storyboard-validation` because the running desktop locked its standard output. The test build used `Tests/WriterApp.Tests/bin/storyboard-validation` because the running backend also locked its standard output.

78 tests passed across `LocalStoryboardTests`, `LocalProjectTests`, `LocalPlanningTests`, `PlanningSyncTests`, `ProjectSyncTests`, `AdvancedDeviceAiTests`, `DeviceWorkspaceViewTests`, and `EditorPanelPresentationTests`.

Coverage includes offline save/reopen, unknown metadata preservation, independent duplicated writing identities, recoverable deletion, invalid hierarchy rejection, stale-write rejection, atomic moves, manuscript order, mapped AI identities/revision checks, draft guards, typed structure proposals, shared board rendering, and desktop page dependency resolution with a selected scene.

Builds reported NU1900 because NuGet vulnerability data could not be reached. Compilation and the selected tests passed.

## Manual acceptance still to run

1. Open a project with root chapters and chapters under parts. Verify horizontal scrolling, empty chapter/part states, filtering, colors, and insights collapse at desktop and narrow window widths.
2. Select scenes normally, with Ctrl/Cmd, and with Shift. Verify bulk fields, card quick edits, and inspector edits. Close/reopen and verify saved values.
3. Drag a scene within and between chapters. Verify the manuscript page order; repeat with Move up/down and the chapter selector.
4. Duplicate a scene with multiple formatted pages. Edit the copy and confirm the source remains independent. Delete/restore the original in the project workspace.
5. Try switching scenes and leaving while a planning write fails. Verify the draft remains available and navigation is blocked until it is saved.
6. With an authorized live account/backend, exercise each scene-coach and board-insight action. Review proposals, Apply/Create explicitly, and verify offline, quota, account-change, and stale revision behavior.

Native visual/interaction acceptance and live provider execution have not been performed for this change.
