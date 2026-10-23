# Collaborative manuscript conversion and projections

Prompt 7 implementation, 2026-10-17. Migration and projection execution are opt-in;
product editor capabilities remain disabled. All acceptance uses synthetic manuscripts
in owned disposable databases. No real manuscript or hosted database was changed.

## Migration contract

`Collaboration:Conversion:Enabled` defaults to false. Enabling it registers permission
to execute the owner/paid migration endpoints and starts projection draining; it does
not enable collaborative editor capabilities or override the session/commit flags.
The backend needs Node 24, the checked-in service lockfile installed, and the generated
schema module. `NodeExecutable` and `CodecPath` under the same configuration section
may select deployment-owned absolute paths. Default codec path is
`WriterApp.Collaboration/src/codec.mjs`, relative to the backend content root. Packaging
and hosted process permissions must be verified before deployment.

1. Synchronize the initiating client and resolve known conflicts. Preserve other
   devices' unseen offline drafts. Read the current snapshot-sync document version.
2. Send `POST /api/documents/{id}/collaboration/migration` with a fresh `operationId`,
   `expectedVersion`, and `synchronizedAndConflictsResolved: true`. The assertion is
   an initiating-client precondition, not proof that every other device is online.
3. Source validation runs before fencing, without a long database lock. Unsupported
   formatting remains editable in Legacy mode. Independent scene text must exactly
   match the canonical ordered pages for its linked section; missing/ambiguous links
   or divergent text require reconciliation before conversion. Neither store wins
   silently. Scene sources, nodes, synopsis, outline, page source/order and document
   metadata are retained in the recovery snapshot.
4. Under the serializable document barrier, ownership/paid access, source version
   and the complete preflight source hash are rechecked. One unique job retains
   the immutable recovery source/hash/version. Mode becomes Migrating atomically.
5. Send `POST .../migration/{jobId}/resume`. Each page's generation, binary baseline
   and initial projection commit together. Restart/retry skips committed pages and
   preserves their exact seed bytes. A failed page leaves the job and source fenced
   for retry; it never resets a page or reopens legacy writes.
6. The final transaction checks every page, schema, baseline checksum and matching
   initial projection before atomically publishing Collaborative/Completed and the
   structure manifest. Repeating Begin with its original identity/version or Resume
   is idempotent. Reusing an operation identity for different input is refused.

`GET .../migration` returns owner-scoped progress and the job ID after a lost response.
The snapshot's raw prose is not returned by this status route. Cancellation between
pages leaves a resumable job. There is no downgrade/abort-to-Legacy endpoint or
in-place reset; retained recovery material must not be deleted as a rollback shortcut.

The codec uses the actual shared ProseMirror schema and validation, with a pinned
server-only `happy-dom` 20.14.6 window and JavaScript/resource evaluation disabled.
The window closes after each request. The owned process accepts bounded JSON over
UTF-8 stdin and returns bounded UTF-8 output; arguments contain paths only. Timeouts
kill only that process tree. Errors never print source or credentials. HTML projection
must round-trip through the same schema. Unsupported attributes, conflicting indent
values and unrepresentable independent table/column widths are refused. Cell alignment
is now represented explicitly in the shared schema; this changes its fingerprint,
so older page generations/clients must pass compatibility checks before use.

## Committed projections and consistent consumers

`CollaborationProjections` is separate from legacy `Pages.Content`. Frozen database
fences are unchanged; no projection header, ambient bypass or special SQL connection
can authorize a legacy save. Binary state remains authoritative.

The worker loads a checksummed committed snapshot plus its exact contiguous tail,
decodes it outside the transaction, and publishes a monotonic generation-scoped
projection. HTML/JSON/plain text, the page projection watermark and covered outbox
completion commit atomically. A newer commit can remain pending while that prefix
publishes. A stale worker cannot replace a newer projection. Failed work retains its
outbox; retry ordering considers attempt counts and processes distinct pages so a
poison page does not stop unrelated pages. Search indexes use projected content and
remain eventually consistent; capability watermarks expose projection lag.

`POST .../read` returns a transactionally consistent manuscript snapshot, including
structure revision, ordered page/generation/sequence metadata and ordered section
HTML mirrors. Mirrors aggregate page projections and never write back into scene
source. Optional input is `{structureRevision, required:[{pageId,generation,sequence}]}`.
If supplied, the vector must contain exactly the current live page set. Generation,
structure and future-sequence mismatches fail. Reads currently require every returned
projection to equal its current committed sequence, stronger than a lower-bound read.
Lag returns `409 collaboration_projection_pending`; retry after the worker catches up.
No timeout or failure substitutes legacy HTML.

The existing export and preview controllers now consume this snapshot for migrated
documents. Their POST request DTOs accept an optional `collaboration` field containing
the structure revision and complete required page vector. It is mandatory for
collaborative exports. Export GET and selection-text export lack that receipt-bound
contract and refuse collaboratively rather than assuming a client draft is current.
Clients must flush and obtain durable receipts before forming the requested vector;
unacknowledged local writing is not attested by an export. Scene fallback is disabled
for these manuscripts, and tombstoned pages cannot reappear through it.

`POST .../ai-context` returns consistent plain text together with its exact structure
revision and page vector. Legacy AI source capture/execution/history replacement is
blocked for collaborative manuscripts. Approval/application/history adapters that
cannot preserve these identities remain disabled; this step does not pretend an
ordinary HTML-history event is a CRDT commit. Durable update receipts remain retained
through projections and compaction for future attribution. Provider-backed AI apply,
collaborative approval/history UI and restore remain later activation gates.

Legacy page/scene read APIs and snapshot downloads refuse protected manuscripts so
old clients cannot display a stale snapshot as current. Existing legacy manuscripts
keep their normal reads, saves, exports and sync. Search indexes are refreshed from
projections, and tombstoned page results are filtered even during backfill lag.

## Editor and lifecycle integration

Both actual editor bundles expose `createJournaledEditor`: it checks complete scoped
capabilities/schema, opens the P6 journal, binds Tiptap directly to the journal Y.Doc,
uses collaborative local undo, blocks `setContent`, freezes editing on account/storage
failure, and refuses destruction if local persistence fails. Concurrent mounts of the
same host are rejected. No remote projection is applied through HTML replacement.

`DeviceCollaborationEditor` is the common native component adapter. It obtains the
verified journal binding, subscribes `Invalidated` before mounting, checks the captured
identity again after asynchronous creation, supplies accurate local/cloud status and
a local-save retry action, and invalidates JS immediately on account changes. Parents
must key by immutable account/page/generation and await `CloseAsync` before navigation.
The component and both bundles compile; packaged WebView, native navigation and actual
suspend/resume remain unverified. Current product routes/capabilities do not select
the adapter yet. The independent old `createLiveEditor` gate remains closed.

`POST .../delete-page` is the minimum owner lifecycle command. It requires an operation
UUID, expected structure revision, page ID and generation. A transaction saves its
idempotent command receipt, advances the manifest, tombstones/fences the page, revokes
its sessions, removes its search entries and terminates covered projection work.
All binary snapshots, receipts, pending local generations and source recovery remain.
Stale/offline text cannot resurrect the page. Rename/create/move/reorder, document
trash/restore/reset and cross-project operations remain explicitly fenced pending
their own authoritative commands. Disabling the feature never downgrades its mode.

## Verification and limits

See [implementation status](realtime-implementation-status.md) for final scoped counts
and artifact logs. Tests use both SQLite and disposable SQL Server Express databases,
actual Node codec processes, real HTTP controllers/export rendering, forced projection
transaction failure, migration interruption/races and real journal/editor bundles.
Conversion interruption is controlled fault injection between committed pages, not
a newly claimed OS process-kill test. Existing P5/P6 transport/journal process-crash
probes remain separate evidence.

Budgets enforced here include 32 MiB source recovery, 1,000 migrated pages, 16 MiB
binary reconstruction/codec state, 4,096 tail updates, 128 KiB public command bodies,
and 32 MiB combined projection characters per full read. These are safeguards, not
performance acceptance. Large-migration throughput, native/physical clients, hosted
deployment, backup restoration, membership, operational retention and combined pilot
activation remain outstanding. P8 adds manuscript-scoped sharing; P9 adds presence
and comments; P10 must close the combined platform and operational gates.
