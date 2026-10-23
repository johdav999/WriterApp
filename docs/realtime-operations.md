# Collaboration acceptance and operations (Prompt 10)

Updated 2026-10-18. The ten implementation prompts now have local code, checks and operational tooling. **Product activation remains blocked.** Automated correctness, local development packages and performance samples do not establish hosted or physical-platform readiness. `FeatureAvailable` and `CanWriteCollaboratively` remain false and `createLiveEditor` remains closed. No production database, deployment, signing identity or real manuscript was changed.

## Repeatable acceptance

Install the exact client and service lockfiles using the repository's development dependency workflow. Use Node 24 and .NET 10. The CI workflow now selects Node 24, installs the collaboration service, rebuilds the shared schema and runs its tests. It does not deploy or open capability gates.

From the repository root in PowerShell 7:

```powershell
./Scripts/verify-realtime.ps1 -SqlServerInstance '.\SQLEXPRESS' `
  -IncludeBrowser -PlaywrightModule '<absolute installed Playwright module path>'
```

The runner builds/checks both editor bundles and their shared schema, runs Node tests, the combined SQLite matrix, the SQL Server collaboration/sync matrix, both real browser/provider probes and editor measurements. SQL targets are randomized fixture-owned databases, never an existing application database. Browser crash tests stop only their owned browser process. `-UseExistingBuild` is for an already checked, current local build. `-IncludeLoad` additionally runs the owned 100-room/20-editor load case on each selected database. Load correctness passing does not imply latency budgets passed; inspect the performance JSON.

Each run creates a separate evidence directory under `artifacts/realtime-acceptance/` containing step logs, durations, bundle/schema/lockfile/tool SHA-256 hashes and `manifest.json`. The manifest always records `pilotReady=false` and lists the outstanding platform/hosted/reference-machine gates. The script restores its process environment and stops on a failed automated step. It neither installs native applications nor deploys a server.

## Fault and acceptance matrix

| Area | Local evidence | Remaining boundary |
| --- | --- | --- |
| Old snapshot/HTML/scene writers | HTTP, EF and database fences, both providers; sync/import/translation/AI/history regression | Unsupported lifecycle and AI replacement operations stay refused |
| Concurrent rich editing and local undo | Both actual bundles; remote edits, reorder/duplicate, Unicode, lists/tables/images, selection and undo/redo | Physical IME, accessibility and packaged navigation |
| Authority | Real .NET/Hocuspocus owner/editor/viewer; wrong scopes, ticket refresh/expiry/replay, role change/revocation, supporting/search/images/export/AI isolation | Hosted identity claim configuration and tenant/network deployment |
| Durable acknowledgement | Before/after commit failure, lost receipt/retry, hash reuse, lease takeover, compaction, real owned-process crash | Physical storage/media failure and managed database failover |
| Local writing | IndexedDB and native atomic journal tests; quota/transaction errors, account/generation/recovery isolation; owned browser restart | Actual native forced termination/suspend on supported devices |
| Migration/projection | Multi-page conversion, interruption/retry, version/hash refusal, projection rollback/lag, consistent consumers | Production-sized migration duration and operational scheduling |
| Presence/comments | Real two-bundle/service journey; spoof/revocation, relative boundary affinity, deletion/detached import, operation receipts | Multi-worker presence distribution, physical cross-client UX |
| Disable | Commit/session/sharing/interaction/conversion switches, read-only owner snapshot, retained receipts and legacy fences | Hosted restart and feature-management propagation |
| Database restore | Native SQLite backup API; SQL `COPY_ONLY` backup, checksum verification and restore to a new database; ledger/comment preservation, stale authority and recovery-environment rejection | Cross-host DB/assets/key-ring rehearsal and agreed RPO/RTO |
| Load and cleanup | 100 rooms, 119 connections, 20 active writers, 100 durable commits, 80 ms owned proxy, exact replay after proxy loss, 50 room/editor switches | Cold-start stampede, 300 ms visibility target, reference machine/mobile, production proxy/TLS |
| Packages | Development Windows build and fresh Android APK; bundle hash inspection | Installed/signed Windows package acceptance, physical Android, actual iOS hardware |

The standard suite explicitly skips the two opt-in browser cases and the load case; the runner can execute them separately. Sources and detailed results are indexed in [implementation status](realtime-implementation-status.md). No source-string assertion substitutes for the runtime rows above.

## Measured budgets and capacity limits

Evidence files are `artifacts/realtime-p10/editor-performance.json`, `load-sqlite.json` and `load-sqlserver.json`. The local machine reports Windows 10.0.26200, i9-13900K, 32 logical CPUs and approximately 128 GiB RAM. It is **not** the proposed 4-core/16-GB reference machine or a physical mobile device.

The real transport workload uses 100 independently authorized page rooms with 5,000 words each, 20 editing accounts on one hot page, 119 connections, five bursts of 20 commits and an owned TCP proxy adding 40 ms each direction. It measures receipt and remote observation after custom-envelope submission, opening after ticket authorization, room counts and Node memory. It preserves exact original installation/update identities when replaying an acknowledged update after proxy connection loss. It asserts zero ordinary provider text uploads.

Startup admission is controlled: hot-room clients and other rooms are ramped rather than treating all-room simultaneous startup as accepted. The early simultaneous-open run exceeded the 3-second backend deadline; that failure is retained in `realtime-p10-load-burst-failure.log`. An earlier gradual run also exposed the provider's 30-second message watchdog despite healthy WebSocket ping/pong. The implementation now sends a bounded authenticated application heartbeat every 10 seconds, clears its timer on disconnect/account changes, and still closes on lost authority. A focused test exercises a shortened watchdog. The underlying 60-second transport timeout remains unchanged.

The final latency-remediation matrix (`artifacts/realtime-acceptance/20261018T091448Z-a272ed57/manifest.json`) measured the following p95 values in milliseconds, using the original workload and unchanged budgets:

| Provider | Opening after authorization (target 2,000) | Durable receipt (target 1,000) | Remote observation (target 300) |
| --- | ---: | ---: | ---: |
| SQLite | 303.2 | 212.2 | **226.6: passed** |
| SQL Server | 241.2 | 204.4 | **209.0: passed** |

The initial remote p95 values were 318.9/330.3 ms. Profiling identified duplicate authorization work in the serialized commit path, bursts of idle policy queries and cold writer-lease acquisition on the first edit. The pre-lock lookup now resolves protected identities only; full current policy runs inside the database barriers. Shared policy checks are reused within that authorization, topology validation is one database query, and idle checks start on staggered ticks within the existing interval. A room first loaded with writable authority acquires its fenced lease during opening; read-only loading acquires none. Writable loaded rooms consequently reserve/renew their lease before any text edit. The existing 30-second lifetime/epoch fences and persistence-before-apply/broadcast remain in force. Reference-machine/mobile performance and simultaneous admission remain unaccepted.

The load JSON includes backend-call timing, submission-to-before-persist time (including inbound proxy delay, queueing and validation), persistence time and event-loop diagnostics. Original baseline reports use the `*-before-latency.json` suffix. Intermediate SQLite misses of 302.8 and 435.2 ms are retained in `load-sqlite-intermediate-latency.json` and `load-sqlite-polling-latency.json`; they precede the final lease/topology fix and do not count as passing evidence.

Both actual browser bundles also measure 20 real keyboard events per 5,000-word, 20,000-word and rich fixture. The rich fixture has 100 table rows, 20 image identities and a final typing paragraph. Keystroke-to-paint is measured through two animation frames; local journal durability and 20 distinct pending envelopes are checked separately. Seed preparation and warm local-journal opening are reported separately from transport opening. Fifty mount/edit/flush/destroy cycles in each bundle leave zero editors/carets; heap at cycles 20 and 50 is recorded after test-only GC. This is bounded desktop evidence, not proof of zero memory growth on every platform.

Typical and rich paint p95 values were about 39 ms. The final 20,000-word stress page measured **40.5 ms web / 43.2 ms device bundle**, passing the unchanged 50 ms target (initially 53.1/50.5 ms). Durability notifications now update editable state only on a permission transition and emit no synthetic content edit; checkpoint decoding uses a direct byte copy. All checksum, bounds, canonical encoding, atomic-journal and CAS checks remain. The browser regression verifies status-only updates, exactly one durable envelope per real edit and account invalidation. Fifty switches retained zero editor/caret DOM nodes and zero server rooms; this remains desktop Edge evidence, not physical-device memory acceptance.

The separate 100-page/100,000-word consumer workload passed exact word preservation and watermark-bound HTTP HTML export on both providers. Final migration/read/export durations: SQLite 69,630/55/223 ms; SQL Server 79,057/73/225 ms. The per-page conversion process cost is included in migration time. See `manuscript-sqlite.json` and `manuscript-sqlserver.json`.

The original 10,000-offline-update/10-second target is **not accepted**. The current journal intentionally bounds pending envelopes to 4,096; the individual durable update limit is 512 KiB, hydrated state 16 MiB, and journal aggregate 64 MiB. Raising these limits or streaming a larger recovery workload requires design, failure and performance evidence.

After running the matrix, load cases and package checks, `node WriterApp.Collaboration/scripts/readiness-report.mjs <acceptance-run-directory>` collects their exact SHA-256 hashes and separates automated correctness, local latency budgets and remaining platform gates in `artifacts/realtime-p10/readiness.json`. The final run passes correctness and local latency budgets; activation remains blocked. The report checks the historical APK's editor hash against the current bundle: the earlier development package predates these latency changes, so its build evidence does not establish a package containing the fix. Installed/physical platform and hosted acceptance remain outstanding.

## Offline integrity tool

Build once with `dotnet build Tools/Collaboration.Operations/Collaboration.Operations.csproj`. The tool starts no application host or network listener, does not run migrations, and accepts connection strings only through a named process environment variable. It never prints the connection string or prose.

```powershell
$env:WRITERAPP_RECOVERY_CONNECTION = 'Data Source=C:\recovery-copy\restored.db'
dotnet run --project Tools/Collaboration.Operations --no-build -- inspect `
  --provider sqlite --connection-env WRITERAPP_RECOVERY_CONNECTION `
  --root '<absolute repository or deployed codec root>' --document '<manuscript UUID>'
```

For SQL Server use `--provider sqlserver` and the restored database's explicit connection string. System databases are refused. SQLite inspect opens an existing file read-only; it never creates one. Pending migrations or a model mismatch refuse inspection/sealing. Apply the matching provider's additive migrations while all writers are stopped first.

Inspection uses a consistent serializable snapshot and checks migration recovery hashes, page topology, committed snapshot/tail coverage and checksums, immutable receipt/outbox coverage, exact current projection content/schema and retained comment anchors. A lagging projection is distinguished from corruption. It reports bounded metadata/finding codes without rewriting data. Exit codes: 0 coherent and read-ready, 2 invalid integrity, 3 projection pending, 4 invalid/unsafe arguments, 5 operational failure. Preserve the copy and investigate any nonzero outcome; do not reseed from legacy HTML.

## Backup and restore procedure

1. Record an explicit recovery point and stop all application writers, Node workers and conversion/projection jobs. Keep every collaboration feature off. A stopped process acknowledgment is an operator precondition; the CLI cannot discover or stop another host. Keep clients' private local journals intact.
2. Back up the **whole database**, not just HTML, snapshots or current update rows. SQLite must use its online backup API or a properly quiesced/checkpointed database procedure; copying only the main file of a live WAL database is insufficient. SQL Server uses a `COPY_ONLY` full backup with checksums and `RESTORE VERIFYONLY ... WITH CHECKSUM`. Preserve encrypted backups under the existing access/retention policy.
3. Back up document-image and cover storage with a checksummed manifest, plus the deployment's persistent Data Protection key ring, service configuration, codec/schema/bundle/lockfile versions and migration history. Do not place secrets/key rings in the repository or evidence JSON. Database-only rehearsal does not prove asset/key-ring recovery.
4. Restore to a **new isolated database/copy** and an isolated asset root. SQL restore must use explicit logical-to-physical file moves and refuse an existing target; never add `REPLACE` to the tested procedure. Do not overwrite the original source database. Check native database checksums and inspect every protected manuscript with the offline tool.
5. Seal the restored copy before allowing traffic. Inspection returns a SHA-256 target fingerprint; compare it with the intended explicit target. Generate a **fresh operation UUID for each new restore**, and reuse it only for retries of that same sealing operation:

```powershell
$restoreOperation = [Guid]::NewGuid().ToString()
dotnet run --project Tools/Collaboration.Operations --no-build -- seal-restored-copy `
  --provider sqlite --connection-env WRITERAPP_RECOVERY_CONNECTION `
  --root '<absolute repository or deployed codec root>' `
  --expect-target '<fingerprint from inspection>' --writers-stopped true `
  --operation $restoreOperation
```

Sealing is transactional and idempotent for the operation. It revokes restored sessions, clears leases, revokes all restored members/invitations/supporting links, and persists a new `restore-<operation>` recovery environment. A backup may predate revocation, so old grants are never silently revived. Committed bytes, generations, receipts, outbox, migration recovery, projections and comments remain intact. The recovery-state migrations refuse rollback over a persisted fence.

6. Configure both .NET `Collaboration:Environment` and Node `COLLABORATION_ENVIRONMENT` to the returned `RequiredEnvironment` before starting that restored deployment. The database fence rejects the old environment even if an operator forgets this step. New tickets require an explicit matching environment; capabilities advertise it. The old local journal namespace remains private and cannot be silently adopted by the restored service. Reinspect all manuscripts; rebuild lagging derived projections through their normal worker only after source integrity passes. Review sharing explicitly before issuing new invitations.

Retain the previous environment in the backup/deployment manifest. Existing recovery enumeration is scoped to an explicit environment; it does not automatically search another environment's local data. Use the recorded old scope for deliberate local recovery/reconciliation. A packaged recovery flow across this namespace change remains part of platform acceptance, rather than an inferred UI guarantee.
7. Rehearse a scoped owner read, receipt replay at the backup recovery point, comment read, private asset access, expired/revoked access, deployment restart and backup manifest comparison. A fresh ticket is required; an old worker/session remains fenced. Keep product activation closed until the full platform/hosted matrix passes.

Writes acknowledged **after** the backup's recovery point may be absent from the restored database. The new recovery environment preserves those clients' old journals for explicit reconciliation rather than displaying them as current cloud state or auto-replaying them. A private recovery reason suppresses the cloud-saved flag. Do not promise zero data loss from a backup that predates acknowledgments. Establish and rehearse the actual managed-database recovery policy and RPO/RTO before pilot use.

## Deployment, restart and emergency disable

- Deploy schema, .NET backend, pinned Node service and matching web/native schema/bundles as one reviewed version set. Keep the internal service API private, use HTTPS/WSS and an exact browser Origin allowlist, and persist the Data Protection key ring across ordinary restarts. Service credentials are server-to-server only. Never put tickets in URLs or logs.
- Before restart, stop admission, set .NET `Collaboration:CommitsEnabled=false`, stop conversion/projection jobs, and wait for bounded in-flight commit transactions. Then stop Node and backend processes gracefully. Never terminate by executable name. A normal restart retains environment and database identities; it is not a database restore and must not manufacture a new generation.
- `/health/live` proves only that Node is responding. `/health/ready` additionally depends on backend reachability, matching commit settings and nonfailed rooms. A mismatched recovery environment is unready. These probes are not an integrity audit, capacity certification or acceptance grant. Use the offline audit and matrix evidence separately.
- For emergency read-only operation, disable .NET commits and stop the Node commit channel. Disable new sessions if credentials/transport must stop; disable sharing/interactions/conversion as required. Existing scope validation rejects new writes even if an old Node process is still running. Preserve the database, all generation records, receipts and local journals. Consistent owner read/export can remain available through supported read paths; stale legacy pages cannot become writable.
- Do not set `CollaborationMode=Legacy`, drop collaboration tables, clear receipts/outboxes or upload an old snapshot to recover. Unsupported trash/move/reset/restore flows remain fenced. The safe rollback is to a compatible read-only application with collaboration data retained, not a pre-collaboration writer. After sealing a restored copy, rollback cannot remove its recovery fence.
- Starting any future pilot requires explicit evidence for the remaining gates and a reviewed code/configuration change; the current environment flags alone cannot open the hard product editor gate.
