# Azure multi-document migration — 2026-10-02

The user explicitly approved applying `20261002061110_MultiDocumentProjectsSqlServer` to the existing Azure `Prosa` database on `prosa-sql-server`, retaining the existing cloud data.

The local project APIs returned HTTP 500 before the update. Read-only database inspection confirmed that `Projects.PrimaryDocumentId`, `Projects.MetadataRevision`, and `ProjectNodes.DocumentId` were absent and this was the only pending migration. The device sync backend maps database exceptions to HTTP 503 with `sync_storage_unavailable` and the message about an unacknowledged transaction; the missing schema was consistent with that failure, but the actual device transaction was not replayed during diagnosis.

The approved migration was applied successfully with:

```powershell
dotnet ef database update 20261002061110_MultiDocumentProjectsSqlServer --project BlazorApp.csproj --context SqlServerMigrationsDbContext --no-build
```

Post-migration verification:

- The three required columns exist and the migration is recorded in `__EFMigrationsHistory`.
- Document, storyboard-node, and page counts remain 9, 30, and 10 respectively.
- Every page's SHA-256 content hash, page ID, document ID, and section ID matches the fresh pre-migration snapshot. Total writing size remains 31,380 bytes. Node IDs and project ownership are unchanged.
- All 30 nodes have manuscript ownership; there are no nodes linked to a wrong project or a supporting document. Every project containing a manuscript has a primary manuscript.
- The running local server returns HTTP 200 for `/healthz`, `/api/projects`, `/api/projects/list-items`, and `/app/projects`.
- The prior focused automated sync checks passed 121 tests; the multi-document ownership/sync checks passed 2 tests. These are automated protocol/storage checks, separate from native device acceptance.

The generated SQL and read-only preflight tool are in `artifacts/azure-schema-fix/`. `approved-before.json` and `approved-after.json` record the counts, migration history, IDs and content hashes without manuscript text.

The device's queued requests were left intact. Use **Retry cloud sync** in the existing signed-in desktop app to replay the durable operation. End-to-end native device synchronization has not been verified after the migration. The migration has no supported downgrade; reverting requires a pre-migration backup restore.
