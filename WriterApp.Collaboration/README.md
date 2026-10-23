# Collaboration transport and operational foundations (Prompts 4–10)

Node 24, Hocuspocus 4.7.1, Yjs 13.6.33. This service exposes authenticated scoped read sessions and an independently gated **durable custom commit channel**. Default-off manuscript sharing adds owner/editor/viewer roles through the .NET access resolver. Normal provider text writes remain blocked. Shared web/native journals and gated migration/projection adapters are implemented; `createLiveEditor` and collaborative capability writes remain disabled pending combined product/platform acceptance. A provider sync event is never a durable receipt. See [sharing contracts](../docs/realtime-sharing.md) for invitations, owner-funded access, explicit supporting links and revocation.

Prompt 9 adds independently default-off authenticated presence/carets and .NET comment records/HTTP authority. Enable `.NET Collaboration:Interactions:Enabled` and `COLLABORATION_INTERACTIONS_ENABLED` only in an authorized staging/test setup. Presence uses bounded stateless frames; ordinary Yjs awareness remains rejected. The server supplies actor/connection identity and rechecks recipients. Comments use committed generation-bound relative positions and retained operation receipts; they cannot mutate manuscript text. See [interaction contracts](../docs/realtime-interactions.md) for limits, routes, legacy detached import and acceptance gates. Schema generation now publishes atomically so running codec processes never observe a missing/partial generated module.

Prompt 10 adds [acceptance/operations procedures](../docs/realtime-operations.md), a repeatable matrix runner, native database restore rehearsals and offline integrity/sealing tools. A persisted recovery fence requires a fresh collaboration environment after an explicit restored-copy seal; old scopes stay private and cannot mint tickets into that namespace. Inspect capability `environment` when constructing immutable journal scopes. Ordinary restart retains the environment; restore is a separate reviewed operation. An authenticated 10-second application heartbeat keeps quiet providers alive without authorizing text writes. Performance and physical/hosted gates remain open; passing local tests does not enable the hard product editor gate.

## Local startup

From the repository root, install the service dependencies with `npm ci --prefix WriterApp.Collaboration --ignore-scripts --no-audit --no-fund`. Apply the checked-in migrations to your selected **local development database** using the repository's usual SQLite or SQL Server migration workflow. Prompt 7 provides opt-in owner migration endpoints and a projection worker; both default off. See [conversion contracts and acceptance boundaries](../docs/realtime-conversion.md) before enabling local synthetic tests. Product editor capabilities remain disabled.

The checked-in `src/generated/schema.mjs` bundles the shared editor schema, source validation and fingerprint implementation from `WriterApp.Client/src/collaboration/server-schema.ts`. Rebuild it after changing those sources: install the client lockfile with `npm ci --prefix WriterApp.Client`, then `npm run build:schema --prefix WriterApp.Collaboration`. Vite 8.3.1 comes from the client lockfile; Yjs remains external and resolves to the service's single pinned runtime. Commit the generated module with its sources. The commit validator remains DOM-free; the separate bounded `src/codec.mjs` process uses pinned happy-dom 20.14.6 for the shared HTML parser/serializer with evaluation/resource loading disabled. It exposes no network listener.

Configure both processes with the same service credential and environment. This example generates the credential in memory and starts an owned, hidden Node child. Run it in a development PowerShell, with your normal local .NET database/authentication configuration already selected:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:Collaboration__SessionsEnabled = 'true'
$env:Collaboration__CommitsEnabled = 'false'
$env:COLLABORATION_COMMITS_ENABLED = 'false'
$env:Collaboration__Environment = 'local'
$env:Collaboration__WebSocketUrl = 'ws://127.0.0.1:1234'
$env:Collaboration__ServiceKey = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
$env:COLLABORATION_SERVICE_KEY = $env:Collaboration__ServiceKey
$env:COLLABORATION_ENVIRONMENT = 'local'
$env:COLLABORATION_BACKEND_URL = 'http://127.0.0.1:5188/'
$env:COLLABORATION_ALLOWED_ORIGINS = 'http://127.0.0.1:5188'
$env:PORT = '1234'
$transport = Start-Process node -ArgumentList 'WriterApp.Collaboration/src/main.mjs' -WindowStyle Hidden -PassThru
try {
    dotnet run --project BlazorApp.csproj --no-launch-profile --urls http://127.0.0.1:5188
} finally {
    if (-not $transport.HasExited) { Stop-Process -Id $transport.Id }
    Remove-Item Env:Collaboration__ServiceKey, Env:COLLABORATION_SERVICE_KEY
}
```

Default configuration disables sessions. `ws:` is accepted only for loopback ticket URLs; use `wss:` behind a WebSocket reverse proxy elsewhere. Node defaults to loopback. Browser Origins must exactly match the explicit allowlist. Native clients may omit Origin, but still require the same ticket. URLs must not contain credentials or query parameters. Keep the internal API on a private route/network; require HTTPS when the .NET backend is not on loopback. The dedicated service key grants no account login and must never enter browser/native configuration or logs. Set deployment secrets through the existing environment/secret mechanism.

Data Protection keys belong to .NET. Persist and share the key ring and application discriminator across backend instances using the application's deployment configuration. Loss or rotation of unavailable keys fails closed and requires owner refresh. The random service key above is development-only and is not printed or written to a file. Test authentication (`X-Test-User`) exists only in the test host.

## Session boundary

- Authenticated actor POST `/api/documents/{documentId}/pages/{pageId}/collaboration/sessions` with `{ generation, installationId, contentContractVersion: 2, protocolVersion: 1, schemaFingerprint, requestCommit: false, membershipRevision: 0 }` returns `{ sessionId, version, ticket, roomName, webSocketUrl, expiresAtUnixMilliseconds, readOnly }`. Use the P3 editor's `getSupport()` values; page metadata must match exactly. Owners use revision zero; invited members capture the current authenticated capability revision in their immutable journal scope. The resource owner must have active paid access and a live collaborative manuscript with one active generation and valid binary history. A request for a commit grant requires owner/editor access and .NET `Collaboration:CommitsEnabled`; otherwise it is denied. Viewers receive read-only tickets. Existing read-only sessions cannot be promoted by enabling a flag. A successful grant returns `readOnly: false`, referring only to the custom channel: Hocuspocus itself always has read-only scope.
- Room names are canonical `environment/documentId/pageId/generation`. Tickets are opaque, authenticated-encrypted ASP.NET Data Protection payloads, bound to `Prosa.Collaboration.Session.v3` and the environment. They bind authenticated actor, resource owner, membership revision, installation, scope, schema, protocol, version, explicit commit grant and expiry. They expire after five minutes. The caller supplies no user ID or role. Earlier purpose-v1/v2 tickets fail closed; the sharing migration revokes retained sessions. Fresh guest ticket requests must match the immutable journal membership revision, so an old outbox cannot silently adopt a new grant. Current membership/owner funding is rechecked during bootstrap, polling and commits.
- POST `.../sessions/{sessionId}/refresh` with `{ version }` uses authenticated owner access, atomically advances the durable version, clears the prior connection binding and renews expiry. A fresh owner refresh may renew an expired ticket; a socket cannot. Disconnect the provider, refresh, create a fresh provider/Y.Doc from committed binary state, and reconnect. In-band token sync is disabled in this phase. Refresh/reconnect must preserve any local draft separately.
- POST `.../sessions/{sessionId}/revoke` requires the owner and permanently revokes this session. Revocation remains available after entitlement loss. Refresh cannot revive a revoked session. Issue another session only after current access checks pass.
- Node alone calls POST `/api/internal/collaboration/authorize` and `/bootstrap` with `Authorization: Bearer <service-key>` and `{ ticket, roomName, connectionId, acquire }`. A cookie, native account bearer or page ticket cannot replace this service credential. Initial acquisition atomically binds the ticket to the server-generated socket UUID; a different socket cannot reuse it, including after disconnect/restart. Reconnect requires refresh. The bootstrap endpoint never acquires a binding.
- Every inbound allowed message rechecks .NET ownership, account deletion, entitlement, lifecycle, feature configuration, active generation, schema, durable session version, revocation and expiry. Idle sockets are checked every second, with a three-second backend timeout; failed checks close the connection. API outages fail closed. This bounds idle invalidation to the polling interval plus request latency/timeout, rather than promising instantaneous revocation.
- Bootstrap returns the hashed committed snapshot and contiguous hashed tail, capped at 4096 updates/16 MiB binary, under a serializable database read. Missing, corrupted or incomplete state is rejected. Node validates Yjs decoding/dependency completeness on a temporary document before loading the room. Neither side falls back to HTML or an empty document.
- The wire gate permits sync-step-1 reads, exact empty sync-step-2 acknowledgements, awareness queries and close. It rejects nonempty sync-step-2, sync updates, awareness changes, broadcast payloads, token sync, malformed/trailing bytes and frames over 1 MiB before mutation. Stateless messages are rejected except for the custom commit envelope below, requiring both the explicit ticket grant and Node's commit feature flag. `readOnly = true` remains an additional Hocuspocus safeguard.

## Durable commit boundary

Both `Collaboration:CommitsEnabled` on .NET and `COLLABORATION_COMMITS_ENABLED=true` on Node are required. They default off. Enabling them does not migrate documents or open the product editor. Use P6's [journaled session](../docs/realtime-journals.md): it atomically persists local envelopes and keeps its committed-only provider document separate from the editor. Product wiring remains gated. Do not connect an ordinary mutable provider Y.Doc to this service as a substitute for that adapter.

The only accepted client stateless payload is a strict object with exactly these fields; UUID strings and SHA-256 are canonical lowercase:

```json
{
  "type": "commitUpdate",
  "protocolVersion": 1,
  "documentId": "00000000-0000-0000-0000-000000000001",
  "pageId": "00000000-0000-0000-0000-000000000002",
  "generation": "00000000-0000-0000-0000-000000000003",
  "installationId": "00000000-0000-0000-0000-000000000004",
  "updateId": "00000000-0000-0000-0000-000000000005",
  "update": "base64-encoded-immutable-Yjs-update",
  "sha256": "64-lowercase-hex-characters"
}
```

The ticket must bind that installation and room. Updates are at most **512 KiB binary**, inside a **1 MiB** WebSocket frame. Each room has one serial queue with at most 32 pending admitted operations. Node merges into a temporary copy of the committed document, checks the exact editor fingerprint and full rich schema before schema parsing can strip unsupported values, rejects unknown fragments and unresolved Yjs dependencies, and caps the resulting document at 16 MiB. Invalid candidates never change canonical state or reach peers.

Private POST endpoints `/api/internal/collaboration/lease`, `/commit`, `/checkpoint` require the dedicated service credential and revalidate the bound session. They accept a nested `session: { ticket, roomName, connectionId, acquire: false }`, a server-generated `workerId` and (after acquisition) `epoch`. Commit also carries `expectedSequence`, `updateId`, base64 `update` and `sha256`; checkpoint carries `expectedSequence`, base64 `state` and `sha256`. They are not client endpoints. Node is the trusted binary/schema validator; .NET verifies byte hashes/limits, current authority, lease and ordering.

.NET serializes on document/session/page barriers under a serializable transaction, using EF's execution strategy on both providers. It discards tracked preflight state before rechecking authority, so an entitlement change cannot be hidden by EF's identity map. The database-derived owner/installation determine attribution. It atomically appends the update, independent receipt and projection outbox event, and advances the page sequence. `(page, generation, installation, updateId)` plus hash is immutable: a matching retry returns the original receipt; different bytes under the same identity are refused. Expected predecessor conflicts cannot fork the ledger. .NET storage failure rolls back the entire operation.

After successful persistence, Node applies to the canonical room, broadcasts, and sends `{ type: "durableReceipt", receipt: { pageId, generation, installationId, updateId, sha256, commitSequence, committedAtUnixMilliseconds } }`. Client journal code must match every field and persist that acknowledgement before clearing its envelope. Errors use `{ type: "commitRejected", updateId, code, preserveLocalDraft: true }`; transport loss is an uncertain outcome, not evidence that a write failed. Retry the identical envelope after reconnecting. Never generate another update ID just because an acknowledgement was lost.

Each Node process has a fresh worker UUID. A database lease lasts 30 seconds and renews through the room queue when less than 10 seconds remain and an eligible session is active. Takeover increments its epoch; an expired epoch cannot renew. Every append and checkpoint verifies current worker/epoch/expiry. A competing or stale worker fails closed. A process crash may require waiting up to the lease lifetime before takeover. Authorization remains fresh for each transaction. Any uncertain persistence response, invalid receipt, lease/sequence loss, or apply failure quarantines the room and closes connections with `reload_committed_state`. Recovery loads the database prefix under a new ticket; it never resets from HTML or acknowledges an unverified in-memory state.

Node checkpoints after 64 commits (tests use 2), through the same queue and lease. .NET atomically inserts the hashed snapshot, advances `SnapshotSequence` and removes covered update rows. Receipts, outbox work and historical snapshots are retained. Failed checkpoints roll back the pointer and deletions. A retry after compaction still returns its original receipt. The independently gated P7 worker publishes derived HTML/JSON/text and projection watermarks; a durable update receipt alone does not imply projection freshness. Consistent export/AI context returns pending while behind.

`GET /health/live` reports process liveness. `GET /health/ready` requires a recent successful private .NET schema/storage probe, matching commit configuration, and no currently quarantined room. It returns 503 until checked or when unavailable; probes run alongside session checks, with a three-second request timeout and five-second freshness bound. Readiness is an operational signal, not a per-update receipt or a tested availability guarantee.

Shared journal recovery, opt-in migration, projections, consistent consumers and common journal/editor adapters are implemented and verified locally. Product route activation, packaged/native WebView acceptance, sharing roles, hosted deployment and load/backpressure/availability acceptance remain later gates. The room admission bound does not claim a full WebSocket flood/rate-limit policy; P10 must validate upstream buffering and limits before activation. P8 owns membership; P9 owns awareness. Retain revocation records and receipts for now; retention cleanup must not revive old identities.

## Verification

```powershell
npm test --prefix WriterApp.Collaboration
dotnet test Tests/WriterApp.Tests/WriterApp.Tests.csproj --filter 'FullyQualifiedName~Collaboration|FullyQualifiedName~DocumentSync'
# Optional real SQL Server: only randomized owned test databases are created/deleted.
$env:WRITERAPP_SYNC_SQLSERVER_TEST_SERVER = '.\SQLEXPRESS'
dotnet test Tests/WriterApp.Tests/WriterApp.Tests.csproj --filter 'FullyQualifiedName~Collaboration'
Remove-Item Env:WRITERAPP_SYNC_SQLSERVER_TEST_SERVER
```

The .NET suite uses real Kestrel and separate owned Node subprocesses. Its durable probe uses two installation IDs, concurrent commits, compaction and immutable receipt retries, rejects both write-frame bypasses, and kills Node after database commit but before apply/receipt. It waits for lease expiry, starts a fresh worker and verifies recovery. Database trigger failures verify append and checkpoint rollback; an entitlement race test verifies the fresh check inside the transaction. `npm test` independently covers schema/dependency rejection, commit-before-mutation, fault prefixes, queueing, framing, Origin checks and backend failure. Tests use synthetic manuscripts and isolated databases; owned processes/listeners are cleaned up. See [implementation status](../docs/realtime-implementation-status.md) for exact evidence.

Implementation was checked against the pinned package sources and the official [Hocuspocus hooks](https://tiptap.dev/docs/hocuspocus/server/hooks) and [provider configuration](https://tiptap.dev/docs/hocuspocus/provider/configuration) documentation.
