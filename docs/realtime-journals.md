# Collaboration journals and account recovery (Prompt 6)

The advanced journal API is implemented in both shipped editor bundles. Product live-editor activation remains disabled until migration and consumer work passes. This API stores Yjs bytes and immutable envelopes; it does not turn legacy HTML saves into collaborative commits.

## Atomic local record

`CollaborationJournalContracts.cs` and `src/collaboration/journal.ts` define the common version-1 record. Its identity binds normalized backend URL, verified server owner ID, environment, document, page and generation. The rich-content contract, protocol and schema fingerprint must match the current editor. Backend/owner/environment/page/generation namespaces remain separate, including recovery copies for obsolete generations.

One record contains the full binary Yjs checkpoint/hash, a durable installation UUID, a revision, ordered pending envelopes, exact acknowledged receipts and an optional private-recovery reason. A local transaction captures its own checkpoint immediately, before later edits can enter memory, and persists that checkpoint **with** its immutable envelope. A later remote checkpoint is merged from the persisted record rather than an in-memory document that might contain unjournaled future edits. Receipt storage and pending removal are another atomic mutation. ID/hash reuse, receipt mismatches, unsupported versions, corruption and schema mismatches fail closed with the original data retained. Existing records are never reseeded.

Web uses one aggregate IndexedDB `pages` record per identity and a durable `settings` installation ID. Hashing/validation happens before a strict read/write transaction; a second compare-and-swap read inside that transaction prevents a stale tab from overwriting another revision. Session retries merge the latest stored Yjs checkpoint with the captured transaction while retaining its original update ID/bytes. BroadcastChannel notifications carry only the hashed key, then compatible tabs reload their own record. Checksums cover the serialized aggregate; byte hashes cover the checkpoint and every pending update. A separate y-indexeddb store is deliberately unnecessary: it would introduce a second commit boundary between text and outbox.

Native uses `DeviceCollaborationJournal` in `<localDocumentPath>/collaboration`, registered through the shared desktop/iOS/Android service setup. A cross-process exclusive file lease serializes reads/mutations. The aggregate is checksummed and written with `AtomicDocumentWriter`: same-directory staging, write-through, flush-to-disk, then atomic replacement. A killed process's staging files are ignored. No truncation fallback or HTML reconstruction is used. Metadata and binary updates use the same JSON/base64 DTO shape as the web journal. Tokens and session tickets are never part of this record format.

Limits are 16 MiB checkpoint bytes, 512 KiB per update, 4096 pending envelopes, 50,000 retained receipts and a 64 MiB serialized aggregate. Capacity errors retain the existing record and unsaved in-memory recovery state. Receipts are retained conservatively; later maintenance must preserve their deduplication/recovery contract. Browser storage remains subject to its profile/eviction policy, and device files to normal host app-data protection. Process-crash tests establish local restart behavior; hardware power loss, disk-full behavior on physical devices and managed mobile storage remain separate acceptance gates.

## Session and transport

Both `window.tiptapEditor.collaboration` and the device module's `collaboration` export provide:

- `IndexedDbJournal`, `NativeJournalAdapter` and `CollaborationAccountFence`.
- `JournaledSession.open`, `connect`, `disconnect`, `flush`, `retryLocalPersistence`, `exportRecovery` and `dispose`.
- `webTicketSource` for same-origin owner-verified ticket issue/refresh. Native hosts use the binding's `GetTicket` callback.

Example for a future migrated web host (the product editor remains gated):

```typescript
const scope = {
  backend: apiBaseUrl, ownerId: authenticatedProfile.userId, environment,
  documentId, pageId, generation, support: await collaboration.getSupport(),
};
const fence = new collaboration.CollaborationAccountFence();
fence.activate(scope, authenticationGeneration);
const store = new collaboration.IndexedDbJournal(scope);
const session = await collaboration.JournaledSession.open({
  store, account: fence.capture(scope),
  getTicket: collaboration.webTicketSource(scope),
  onStatus: updateThisSessionStatus,
});
// Bind the compatible editor's collaboration extension to session.doc.
// Keep full-content replacement, legacy save callbacks and provider upload disabled.
await session.connect();
```

If a compatible local record exists, open restores it without network or a seed. For a fresh installation, open can obtain an ephemeral read ticket, load a separate empty Y.Doc through authenticated binary sync, validate its schema and atomically initialize the journal. That read ticket never owns a text envelope. The actual installation UUID is durable before any commit is sent. Without a local checkpoint, explicit committed binary seed or authenticated transport, offline open refuses. An explicit `committedSeed` is also accepted from a trusted binary boundary for tests/native integrations; existing checkpoints take precedence. HTML is not accepted as a seed.

The editor's local Y.Doc includes pending edits. Each network connection has a **different initially empty** read document; only the provider populates it with committed server state. No pending checkpoint is ever loaded into that transport document. Server updates merge into the local checkpoint through the journal, then into the editor with remote origin. Ordinary provider sync/update frames therefore cannot upload local pending edits. The only outbound text path sends a previously persisted `commitUpdate` envelope through P5's stateless channel, in pending order, one in flight at a time.

Transport sync does not remove pending work. A receipt must match page, generation, installation, update ID and hash, with valid sequence/timestamp, before it is persisted and that envelope removed. A disconnect/restart retries the original bytes and ID. Reconnect and scheduled expiry refresh obtain a fresh scoped ticket; the default provider's own reconnect loop is disabled. Explicit access rejection blocks delivery and retains a private recovery reason. `connect(true)` requests explicit recovery retry, which clears that reason only after fresh authenticated sync. Schema/generation incompatibility keeps the original copy; no legacy fallback is enabled.

`status` distinguishes `localDurable`, `remoteDurable`, queued work, pending envelopes, connection state, storage failure, private recovery and account change. A pending envelope prevents `remoteDurable`, including after `onSynced`. Storage errors keep `localDurable` false, stop delivery and retain the immutable work for `retryLocalPersistence`. `exportRecovery` exposes a private binary in-memory copy, including current state even when disk writes fail; it is not an automatic import/reseed protocol. `dispose` refuses to discard that copy when flush fails. Hosts must retain the session or export its recovery data and surface the failure.

## Account and native host contract

The web host activates its account fence from authenticated profile/generation changes and signs it out with `activate(null, generation)`. Different owner/backend/environment/generation events fence old leases. Cross-tab account notifications invalidate local leases but never authenticate another tab as the announced owner. Closing a page's fence stops only that fence, without broadcasting an application sign-out. Delayed ticket, remote-state and receipt callbacks check the captured lease before applying or acknowledging anything.

Native hosts resolve `DeviceCollaborationJournalHost` from DI:

- `OpenAsync(scope)` verifies the actual server profile/paid owner through the existing authenticated HttpClient and records the binding between its device account ID and server owner. `OpenAsync(scope, offline: true)` requires a matching previously verified binding and currently cached signed-in identity.
- `OpenLocalRecoveryAsync(scope)` supports a cold offline start for an explicitly selected known private generation while signed out. It grants local recovery only and cannot mint tickets. A sign-in invalidates that recovery binding; continuing online requires a fresh verified owner binding.
- The binding exposes `Load`, `Initialize`, `Save`, `Acknowledge`, `Recovery`, `IsCurrent` and `GetTicket` through JS interop. Pass a `DotNetObjectReference` to `NativeJournalAdapter`, and route ticket requests through `GetTicket(installationId)`. Its DTO `ToString` redacts ticket contents.
- The binding captures `DeviceAccountService.Generation` and account ID. Subscribe to `Invalidated` and call the old JavaScript session's `invalidate()` immediately before adopting new UI state. Every native bridge read, receipt, ticket operation and completion also checks the captured generation; the JS adapter checks `IsCurrent` on delivery callbacks. This binding event must be wired when P7 activates the actual WebView/editor component.
- Already-captured local work can finish **only in its immutable old namespace** after sign-out; its result cannot enter a new account. Receipts and transport remain fenced. Flush before disposing the binding. The old account's pending data remains available for later recovery, and no credentials are copied into it.

Background/resume hooks may call `flush`/`connect`, but continuous transaction journaling supplies durability. A background callback is not the only save trigger. The current legacy editor and snapshot-sync engine are unchanged; P7 must route migrated documents to this journal path, subscribe account/lifecycle events and prevent their legacy save callbacks. Packaged/native WebView and physical iOS/Android acceptance are still outstanding.

## Verification

```powershell
dotnet test Tests/WriterApp.Tests/WriterApp.Tests.csproj --filter 'FullyQualifiedName~CollaborationJournal'
npm run build --prefix WriterApp.Client
npm run check:collaboration --prefix WriterApp.Client
npm run build:schema --prefix WriterApp.Collaboration
npm test --prefix WriterApp.Collaboration

# Actual bundles, authenticated Kestrel/Hocuspocus, persistent headless Edge profile.
# Set PLAYWRIGHT_MODULE to the installed Playwright module path if it is not resolvable normally.
$env:WRITERAPP_COLLAB_BROWSER_TESTS = '1'
dotnet test Tests/WriterApp.Tests/WriterApp.Tests.csproj --filter 'FullyQualifiedName~ActualWebAndDeviceBundlesRecover'
# Optional: repeat against an owned random SQL Server Express database.
$env:WRITERAPP_SYNC_SQLSERVER_TEST_SERVER = '.\SQLEXPRESS'
dotnet test Tests/WriterApp.Tests/WriterApp.Tests.csproj --filter 'FullyQualifiedName~ActualWebAndDeviceBundlesRecover'
Remove-Item Env:WRITERAPP_SYNC_SQLSERVER_TEST_SERVER, Env:WRITERAPP_COLLAB_BROWSER_TESTS
```

The browser fixture is explicitly skipped without its opt-in/runtime and reports that skip. Its actual runs cover persistent IDB, two-tab merge/CAS, account invalidation, incompatible/corrupt data, denied generations, exact receipts, fresh binary bootstrap and reconnect. It aborts a real IDB write transaction and separately injects `QuotaExceededError`; this is fault injection, not a filled physical disk. It forcibly kills only its own browser PID obtained from that browser's CDP session, reopens the same owned temporary profile, and checks checkpoint/outbox and original-ID replay. The native crash helper kills itself before replacement, after save and after receipt storage. No shutdown/lifecycle flush runs at those crash points. These are synthetic data in owned temporary paths/databases. See [implementation status](realtime-implementation-status.md) for exact counts and logs.
