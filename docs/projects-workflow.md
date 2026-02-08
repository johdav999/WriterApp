# Projects Workflow

`Workflow:ProjectsEnabled` (default `false`) enables an additive manuscript layer above documents/sections.

## API

- `GET /api/projects`
- `POST /api/projects`
- `POST /api/projects/from-document/{documentId}`
- `GET /api/projects/{projectId}/tree`
- `POST /api/projects/{projectId}/nodes`
- `PATCH /api/projects/{projectId}/nodes/{nodeId}`
- `POST /api/projects/{projectId}/nodes/{nodeId}/reorder`
- `GET /api/projects/{projectId}/stats`

## Notes

- Scene nodes may link to existing sections (`LinkedSectionId`).
- Node word counts are cached and refreshed when page content changes.
- Existing document/section editing remains the default workflow.
