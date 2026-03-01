# Export Preview and Scene Suggest Root Cause Notes

## Summary

This fix addresses three production issue clusters:

1. `scene.suggest` intermittently returned `500`.
2. Import was missing from the editor Document menu.
3. Export preview/output did not always match current editor content.

## Root Causes

### 1) `scene.suggest` 500

- The AI execute path loads scene-card metadata (`SectionSceneCards`) before invoking the provider.
- In environments with schema drift (for example, incomplete migration rollout), metadata queries could throw SQLite schema errors (`no such column` / `no such table`) and bubble as `500`.
- History persistence failures could also bubble into action failures in earlier behavior.

### 2) Import menu regression

- Import conversion backend existed (`SectionImportService` + section import endpoint), but the Document menu entry was missing from the current editor UI path, so import was not discoverable.

### 3) Export preview/output mismatch

- Scene-route editing persisted content in `SceneContents`, while export/preview primarily assembled body text from `Pages`.
- If `Pages` content was stale/empty and only `SceneContents` had the latest text, preview/export showed headings/structure but incorrect body text.

## Fixes Applied

### AI / scene.suggest

- Added correlation-aware structured logging and timing in AI execute pipeline.
- Added safe fallback for scene metadata loading:
  - if SQLite reports missing metadata schema, continue with empty scene-card context instead of crashing.
- Kept provider failure mapping to `ProblemDetails` (`400/429/503/504`) instead of generic `500`.
- Hardened history behavior so history persistence failure does not fail successful AI proposal responses.

### Import

- Reintroduced `Import...` in Document menu for editor workflow.
- Existing import flow supports `.txt` and `.docx`, including replace/append behavior and progress/error states.

### Export preview/output

- On scene-content save (`/api/projects/{projectId}/scenes/{sceneNodeId}/content`), linked section pages are synchronized so canonical export data stays current.
- On section import, linked scene content is synchronized back to keep both storage paths aligned.
- Export controllers now include fallback to latest linked `SceneContents` per section when page-derived content is empty.

## Diagnostics Added

- Correlation ID propagation/echo (`X-Correlation-ID`) for API responses.
- Structured request completion logging for API calls with duration and status.
- ProblemDetails responses for unhandled API errors with trace/correlation metadata.

## Regression Coverage Added

- `AiActionsControllerTests`:
  - scene suggest success
  - missing scene -> `404`
  - invalid payload -> `400`
  - provider missing -> `503` ProblemDetails
- `SectionImportServiceTests`:
  - TXT conversion
  - DOCX conversion (heading + bold/italic subset)
- `ExportEndpointTests`:
  - export print uses linked `SceneContents` fallback when pages are empty

