# Document synchronization API (Release 1, Prompt 7)

The backend exposes an authenticated, paid synchronization protocol at `/api/sync/v1/documents`. It uses the existing web/native authentication and owner identity. Every request checks the current subscription; `documents.sync` is derived from active paid access, not a client-supplied flag or cached UI entitlement. Local editing remains independent of these endpoints. The desktop queue and conflict UI are Prompt 8.

## Protocol

Shared records live in `WriterApp.Shared/DocumentSyncContracts.cs`; JSON uses camelCase.

| Request | Result |
| --- | --- |
| `GET /changes?limit=50` | Initial discovery: `changes`, opaque `cursor`, `hasMore`. Includes trash and permanent tombstones. |
| `GET /changes?cursor=...&limit=50` | Changes after a previous cursor; limit is 1–100. URL-encode the cursor. |
| `GET /{documentId}` | `state` and full `document`, with a quoted ETag. A permanent tombstone returns `document: null`. |
| `POST /{documentId}/operations` | Atomically apply an operation; return `operationId` and resulting `state`, with ETag. |

`state` contains `documentId`, opaque `version`, `isDeleted`, and `isTrashed`. A snapshot includes document/project identities, title, language, kind, archive state, timestamps, ordered sections, and pages. Page content is labeled `html`, `legacyText`, or `legacyJson`; clients must recognize unsupported formats and preserve their originals. This is an editing subset of the web aggregate; unrelated web metadata is retained on update.

Use a client-generated document GUID for a new upload. The server creates its standalone project. Each section and page also needs a stable, nonempty GUID. Existing documents retain their project, kind, creation time, and other advanced metadata.

Example new-document operation:

```json
{
  "operationId": "1b8a620a-e534-422f-9f80-37e7b08555cb",
  "expectedVersion": null,
  "action": "upload",
  "document": {
    "title": "My draft",
    "languageCode": "en",
    "sections": [{
      "id": "407e356f-1678-4263-89f1-e920c612610a",
      "title": "Opening",
      "orderIndex": 0,
      "narrativePurpose": null,
      "languageCode": "en",
      "pages": [{
        "id": "d6a1d33c-f162-42e3-9c6d-d2cf8dd203a2",
        "title": "Page 1",
        "orderIndex": 0,
        "content": "<p>Once upon a time.</p>",
        "contentFormat": "html"
      }]
    }]
  }
}
```

For subsequent changes send the latest unquoted `state.version` as `expectedVersion`. Actions are case-sensitive: `upload`, `rename` (also send `title`), `trash`, `restore`, and `delete`. Only upload accepts `document`. Upload contains the full section/page set. Trash prevents content edits until restored; permanent delete requires trash first. Restore also clears the archive flag.

## Device queue and conflict rules

1. Persist an immutable request, route document ID, and operation ID before sending. Retry that same request after lost acknowledgments or transient errors. Do not reuse an operation ID for edited content or another route.
2. Successful receipts are stored transactionally with content and its version. Replaying a successful operation returns its original result even after newer edits or deletion, without repeating the mutation. That response is an acknowledgment, not proof that the returned version is still current. Process subsequent local edits with separate operations.
3. A stale base returns HTTP 409 `version_conflict` with `current` server state. Preserve local writing, download the server snapshot, and present a conflict copy; never automatically overwrite. Download may have advanced again since the conflict response.
4. Permanently deleted IDs return 409 `document_deleted`, even for uploads with a null base version. Preserve offline writing as a separate local copy; uploading it under a new identity must be an explicit user decision.
5. Process discovered records and persist them locally before advancing the durable cursor. Fetch pages while `hasMore` is true, then retain the cursor for later polling. Changes are coalesced to the latest state per document, not an event log. Concurrent edits can repeat a document on later pages; consumers must tolerate repeats. Fetch each changed snapshot and use its returned version.

Cursors are protected and bound to the owner. Invalid cursors return 400; restart discovery without a cursor while keeping local documents and pending operations. Never interpret an absent item in a page as deletion. Feed sequence numbers are internal; versions cannot be ordered or compared numerically.

## Errors and limits

Domain errors have `{ "code": "...", "message": "...", "current": null }`; conflicts include current state when applicable. Middleware authentication challenges, malformed JSON/model binding, and hosting request-size rejection can use ordinary HTTP/problem responses instead. Clients must classify status codes as well as domain codes.

| Status/code | Client handling |
| --- | --- |
| 401 | Sign in or restore the session; preserve the queue. |
| 403 `entitlement_required`, `account_deleted` | Stop synchronization; preserve local writing. |
| 404 `document_not_found` | Missing or another owner's ID; do not recreate automatically. |
| 409 `version_conflict`, `document_deleted`, `document_trashed`, `trash_required` | Resolve the indicated state before retrying with a new operation. |
| 409 `operation_id_reused` | Queue/protocol error: same ID was used with a different request. |
| 400 `invalid_sync_request` | Fix validation or restart discovery for an invalid cursor; no blind retries. |
| 413 `payload_too_large` | Surface the size limit; preserve local content. |
| 422 `structure_removal_not_supported` | Retain all server sections/pages; use the web client for structural removal in v1. |
| 503 `sync_storage_unavailable` | Outcome was not acknowledged. Retry the same operation with bounded backoff; `Retry-After: 2`. |

Requests are limited to 2 MiB; uploads allow 1–100 sections and at most 1,000 pages, with at most 500,000 content characters per page. Titles are at most 200 characters (document title is required), language codes 35, and narrative purpose 4,000. IDs and sibling order indexes must be unique; order indexes are nonnegative.

Uploads accept the basic desktop HTML subset: paragraphs, headings 1–3, bold/italic/strike, lists, blockquotes, links, breaks, code, and preformatted text. Only ordered-list start and safe link attributes are accepted. Scripts, images, styles, active attributes, unsafe URLs, and unsupported markup are rejected rather than silently removed. HTML depth/node complexity is bounded. Legacy formats and richer web formatting require explicit conversion or read-only handling on the device. Moving existing pages between sections and removing existing sections/pages are excluded from v1 to protect linked web metadata. Document trash/permanent deletion is supported.

## Persistence and deployment

Migrations add `DocumentSyncRecords`, `DocumentSyncOperations`, and `DocumentSyncClocks`, backfill existing documents, and install triggers on Documents, Sections, and Pages. Triggers cover both web edits and sync writes, including bulk deletion. A transactionally locked global counter orders changes by commit, avoiding missed changes when independent writes finish out of order. This serializes participating writes and is a deliberate Release 1 throughput tradeoff; monitor contention before scaling.

Successful receipts and permanent deletion tombstones have **indefinite retention in v1**. They contain IDs, owner, versions/status and request hashes, not document text. Do not prune them: there is no offline-expiry/device-rebase protocol yet. Account-erasure policy must explicitly address this retained metadata. Deleted identities are denied API access. Do not disable triggers or reuse permanently deleted document IDs through maintenance scripts.

Back up and rehearse the upgrade in staging before deployment. Apply the matching provider migration through the existing database rollout process:

```powershell
# SQLite development database
dotnet ef database update --context AppDbContext

# SQL Server/Azure SQL, with the intended connection supplied securely
dotnet ef database update --context SqlServerMigrationsDbContext
```

`EnsureCreated` alone does not install the synchronization triggers. Production must use migrations. Persist and share ASP.NET Data Protection keys across backend restarts/replicas so cursors remain readable. Keep native registration and identity-continuity checks from [Native authentication setup](native-authentication.md). Do not enable device uploads until staging has verified those identities against existing paid accounts. Search refresh after a successful sync mutation is queued and eventually consistent.

## Verification

The 472-test suite passes, including 16 sync cases covering two-client conflicts/deletion, retries, ownership, revoked entitlement, controller errors, unsafe HTML, payload/structure validation, cursor pagination, rollback, simultaneous SQLite connections, and SQLite migration backfill. The provider-selectable tests also pass against local SQL Server Express, exercising its triggers and restrictive foreign keys; the concurrency and migration-backfill cases explicitly remain SQLite tests. A SQL Server idempotent upgrade script was generated successfully. Azure deployment and migration execution were not performed.

To run the SQL Server variant, set `WRITERAPP_SYNC_SQLSERVER_TEST_SERVER` to a local SQL Server instance and run `DocumentSyncTests`. The fixture uses integrated authentication and creates uniquely named `WriterApp_SyncTests_*` databases, deleting only its own database after each case. Leave the variable unset for normal SQLite CI.

Prompt 8 adds the [desktop sync engine, queue, conflict copies, and status UI](desktop-synchronization.md). Remaining rollout work: staging upgrade and end-to-end authenticated synchronization with the configured native client. Native iOS account/connectivity integration remains future work.
