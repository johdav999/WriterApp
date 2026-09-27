# Desktop synchronization (Release 1, Prompt 8)

The Windows host now runs the shared device sync engine against the [Prompt 7 API](document-synchronization.md). Local editing, autosave, recovery, and document files remain available without a paid account or connection.

## Using synchronization

1. Configure and verify [native sign-in](native-authentication.md), deploy the backend API, and apply its provider migration in staging first. Sign in with an active paid account.
2. Existing cloud documents download automatically after sign-in. Choose **Enable cloud sync for this document** in the library or editor to upload a local-only document. Other local documents and conflict copies are not uploaded automatically.
3. Linked edits synchronize after a two-second debounce following a completed local save. Windows connectivity events trigger sync after reconnection. **Sync now** checks for cloud edits and retries transient failures; **Pause sync** cancels the current run while retaining requests.
4. The library and editor display local save status separately from cloud status, last synchronization time, and actionable errors. Cloud-only edits made after the last run are fetched on the next trigger or explicit Sync now; there is no continuous background polling in Release 1.

Sync state is bound to the backend URL and backend-provided user ID. A local document is reserved to that binding before its first upload. Changing accounts or backends cannot automatically upload its writing elsewhere. To intentionally share a separate copy with another account, duplicate the document and explicitly enable that copy. Signing out keeps all local documents and journals.

## Offline queue and recovery

The `documents/sync` subdirectory contains a versioned, atomic journal per account/backend plus hashed owner bindings. A journal records server identities and versions, acknowledged writing fingerprints, cursor, last-sync time, failures, immutable pending operations, deletion intent, and unresolved conflict snapshots. It contains document content when an operation or conflict needs it; protect and back up this directory with the document files. Credentials remain in the existing secure identity cache and are never stored in the sync journal.

A saved local revision is durable unsent work. The engine compares it to the acknowledged writing fingerprint on each run, so a crash between a local save and queue preparation does not lose that edit. Before transmission, the full operation and stable operation ID are atomically persisted. Retries and restarts send that immutable request until acknowledged. Later local edits become a separate operation; a successful older receipt never marks newer writing synchronized.

The journal uses a cross-process file lease during sync, separate from the document-store lock. Network requests do not hold the document-store lock. Incoming writes use local revision compare-and-swap. Corrupt or unsupported journals stop synchronization without replacing the files; do not delete a journal to repair a retry, because it contains operation identities and account bindings.

Transient network failures, 408, 429, and server errors get at most three attempts per request, with one- and two-second backoff. After exhaustion, a reconnect, local-save trigger, or explicit Sync now can try again. Authentication and entitlement failures stop the run. Validation, payload limits, missing documents, and operation-ID errors require attention instead of automatic retries. **Retry after fixing** replaces a definitely rejected 400/413/422 request with fresh work; requests with an uncertain outcome retain their original operation ID. Canceling never discards pending work.

A run processes at most four successive operations per document and 100 discovery pages. The cursor is committed only after every item in a page was handled or durably retained as pending/conflicted. If further pages remain, the UI asks for another Sync now. Repeated feed items and repeated receipt acknowledgments are safe.

## Conflicts and deletion

When a server version differs from the queued base, the original local document stays intact. The engine persists the server snapshot and creates an independent **(cloud conflict copy)**. The **Review conflicts** screen links to both versions. Incoming changes to an open editor also create a conflict, protecting writing that may still be in its WebView or recovery journal.

- **Keep local as linked version** retains a **(local conflict copy)** and uploads local writing against the reviewed cloud version. If the cloud changed again, another conflict is raised.
- **Keep cloud as linked version** first retains a **(local conflict copy)**, then installs the reviewed cloud snapshot in the linked document. The independent cloud conflict copy remains too.
- Copies are local-only and editable. Editing an inspection copy does not change the stored resolution snapshot; upload that copy explicitly if you want to use its new writing.

There is no automatic text merge. Metadata-only acknowledgments can be rebased while editing; changed writing is never silently rebased. Interrupted resolution can leave an additional recovery copy, which is preferable to deleting writing.

Moving a linked document to Trash queues a versioned trash operation; restoring queues restore, followed by any unsent writing. In Trash, **Delete from cloud…** requires a separate explicit confirmation. This queues permanent cloud deletion, while retaining local content for recovery. A known account can queue this intent offline; first-time account verification/enrollment requires connectivity after restart. Permanent tombstones stop all further uploads to the deleted server ID. Offline conflicting writing is retained as an independent local copy, and uploading that copy under a new identity requires explicit enrollment. Linked local files are not automatically purged, even after cloud deletion.

## Limits and release verification

The API's basic HTML, payload, ownership, and structural limits still apply. Title-only changes use rename and do not resend unchanged rich/legacy content. Content edits use a complete aggregate upload; unsupported formatting, omitted existing sections/pages, and page moves are surfaced as errors without deleting local content. The existing editor preserves unsupported formats instead of silently converting them.

Automated coverage includes 28 new device-sync cases: deterministic sign-in/debounce/reconnection triggers, offline edits and restart, lost acknowledgments and identical operation IDs, cancellation, edits during upload, two-device downloads/deletion, both conflict choices and retained copies, active-editor protection, account/backend isolation, metadata rebasing, corruption, paid/auth failures, permanent validation failures, cursor encoding, and HTTP response classification. Retry timers are advanced explicitly; tests do not rely on sleeps. These are simulated API/device tests, complemented by Prompt 7's real database tests.

The full suite passes **500 tests**. The Release solution, Windows Debug host, and iOS managed Debug host build with zero warnings.

Before release, run live Windows UAT against staging: sign in, enroll a disposable draft, disconnect/edit/restart/reconnect, edit the same document from the web and desktop, inspect and resolve both conflict choices, verify plan loss preserves local files, and confirm permanent deletion does not resurrect offline writing. Confirm native close/recovery behavior from Prompt 5 too. Real tenant login and live Azure synchronization were not exercised here, and no Azure deployment or database migration was performed.

The shared engine compiles for iOS. Windows has the connectivity adapter; native iOS connectivity/lifecycle and authentication integration remain future iOS work. The next desktop implementation step is Prompt 9's AI actions.
