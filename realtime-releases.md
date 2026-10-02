# Prosa real-time collaboration: three-release implementation plan

Status: proposed implementation plan, 2026-10-02. No collaboration functionality is claimed as implemented or verified by this document.

This plan develops [realtime.md](realtime.md) into three independently gated releases spanning the web client/server, Windows desktop, and iOS app. Other mobile platforms can reuse the shared device implementation when their application hosts exist; Android delivery is not assumed here.

## Release overview

| Release | User outcome | Distribution |
|---|---|---|
| 1: Reliable editing across your devices | One author can edit the same manuscript on web, desktop, and iOS, including offline changes | Opt-in pilot for existing owners |
| 2: Shared manuscripts | Authors invite editors and readers, write together, and discuss anchored comments | Controlled multi-user rollout |
| 3: Complete collaborative project workflow | Teams coordinate manuscript structure, storyboard work, review suggestions, and recovery history | General availability after scale and recovery gates |

Release 1 deliberately establishes the production storage and compatibility boundaries before inviting other users. Release 2 adds the permission model and sharing workflow. Release 3 extends collaboration beyond text and completes the operational work needed for wider adoption.

## Architecture and rules shared by all releases

- Use a shared TypeScript collaboration module in both Tiptap editor entry points, backed by Yjs and a Hocuspocus WebSocket service alongside the .NET backend.
- Identify each collaborative page by backend/environment, document ID, page ID, and a content generation. Keep identity stable through title changes and moves within the supported project boundary.
- Use one Yjs document per Prosa `PageId`. Subscribe to active pages; do not load an entire novel into every mobile editor.
- Keep .NET authoritative for identity, entitlement, access, project structure, deletion, and issuing scoped collaboration tickets. Room names and client-supplied roles never confer permission.
- For migrated pages, Yjs state is authoritative. HTML is a derived representation for existing readers, search, AI context, and export. Never reconstruct collaborative state from HTML on each reconnect.
- Retain the existing snapshot-sync path for non-migrated documents. Server-side checks prevent legacy web saves and device uploads from replacing migrated text.
- Persist edits locally and remotely. Distinguish “Saved on this device,” “Waiting to sync,” and “Saved to cloud.” A provider connection or synchronization event alone is not evidence of durable cloud storage.
- Use explicit protocol and editor-schema versions. Incompatible clients must be prevented from writing before loading content into an editor that could strip unknown nodes or marks.
- Keep presence ephemeral. Keep authorization, audit records, and durable content independent of presence messages.
- Treat CRDT convergence as preservation of concurrent operations, not a guarantee that overlapping prose changes express the authors' intended meaning. Provide review and recovery tools.

## Release 1: Reliable editing across your devices

### Scope and experience

An existing owner opts a manuscript into the pilot and can edit it from supported web, Windows, and iOS builds. Their devices receive live text updates and merge persisted offline edits after reconnection. The editor shows other active device sessions and accurate save status.

No invitations or access by another account ship in this release. Existing private manuscripts continue using current synchronization until explicitly migrated.

### Implementation work

1. **Extract a common editor collaboration layer.** Add proposed modules such as `WriterApp.Client/src/collaboration/session.ts`, `persistence.ts`, and `schema.ts`. Integrate them into `tiptap-editor.ts` and `device-editor.ts`. Align supported nodes and marks with `WriterApp.Shared/Editor/content-contract.json`. Use collaboration-aware undo scoped to local changes; stop full-content replacement on remote updates.
2. **Introduce the service and session contract.** Add a proposed `WriterApp.Collaboration` Node service, with pinned dependencies selected and checked during implementation. Add a .NET session endpoint that resolves the requested page, validates ownership and entitlement, and issues a short-lived, audience-bound ticket. Validate that ticket and current page state at the service boundary. Support token renewal without losing unsent work.
3. **Build durable storage.** Store page metadata, binary updates, and compacted binary snapshots. Suggested logical records are `CollaborationPage`, `CollaborationUpdate`, and `CollaborationSnapshot`; names are proposals, not existing types. Record schema version, generation, and projection watermark. Deduplicate retried submissions and acknowledge cloud saves only after durable commit. Keep the client's outbox until that acknowledgement. Replay and compact without deleting data still needed for recovery.
4. **Implement native and browser persistence.** Use browser persistence for web and a bridge to account-scoped device storage for desktop/iOS. Persist updates continuously; do not rely on an app-closing event. Reuse account-generation and connectivity boundaries in `WriterApp.Device.Shared`. Load the local state before synchronizing, then exchange missing updates. Account switching must not send one account's outbox through another account's session.
5. **Create the HTML projection path.** Generate compatible HTML from committed collaborative state for existing consumers. Track the state used for each projection. Export and AI requests must explicitly use a sufficiently current projection or wait for it. Prevent derived writes from re-entering the old upload path as fresh user edits.
6. **Protect every mutation path.** Update `DocumentSyncService`, `DeviceSyncEngine`, page repositories, web save handlers, import/replace operations, and document deletion checks. Reject legacy content uploads for migrated documents with an actionable compatibility response. Keep unsupported structural changes disabled for pilot documents until a safe command path exists.
7. **Handle destructive and external edits.** Reject updates for deleted pages. Preserve offline work as a recovery copy instead of resurrecting deleted content. Apply supported AI replacements as guarded editor transactions; disable any AI/import/restore path that still overwrites whole-page HTML for migrated content. If the selected text changed during an AI request, require review before applying the result.

### Migration and deployment

1. Deploy additive database changes, server-side write guards, and capability reporting before enabling migration.
2. Release clients that recognize collaboration mode and recover unsupported-version errors without discarding local writing.
3. Before conversion, synchronize the initiating client and require existing known conflicts to be resolved. Other devices can still have unseen offline drafts; those must remain recoverable rather than being silently imported later.
4. Take a recoverable copy of the source document, fence writes using the current version, seed each page exactly once, and validate the resulting content.
5. Switch the manuscript's mode only after every page is ready. Use an idempotent migration state machine so a crash can resume without duplicating text. New clients observe either the old mode or the fully activated mode.
6. Start with a single collaboration-service instance and a bounded pilot. Keep an explicit maintenance/fail-closed behavior for service outages; local writing remains available.

Rollback stops new migrations and can disable cloud writes while preserving local work. Do not silently restore the old snapshot writer for migrated documents. Returning to legacy mode requires a controlled export of the latest state, write fencing, generation change, and reconciliation of pending updates.

### Acceptance and release gate

- Two browsers, Windows, and an actual iOS device edit the same page and converge after concurrent insertion, deletion, formatting, and undo.
- Supported tables, images, links, Unicode, and mobile text composition survive synchronization. Large-page typing remains responsive against performance budgets recorded before pilot launch.
- Offline edits survive process termination and merge after reconnect. Duplicate delivery, missing acknowledgements, and reordered updates do not duplicate text or falsely report a cloud save.
- Service restart, database failure, and snapshot compaction preserve all acknowledged updates. A backup is restored into an isolated environment and checked against expected content.
- Old clients cannot overwrite migrated pages; non-migrated documents retain existing sync behavior.
- Deletion, sign-out, account switching, schema mismatch, and interrupted migration preserve recoverable writing.

Deliver automated protocol/storage evidence plus browser, packaged Windows, and native iOS evidence. A successful build or browser simulation does not satisfy device acceptance. If iOS delivery is blocked by signing or device access, the release remains incomplete for the promised platform scope.

## Release 2: Shared manuscripts

### Scope and experience

Owners invite collaborators to a project. Editors write together, commenters discuss text, and viewers read. Participants can see who is present and which page they are working on. Comments remain attached as surrounding text moves.

### Implementation work

1. **Centralize project access.** Add project membership and invitation records, with owner, editor, commenter, and viewer roles. Define a permission matrix for viewing, editing text, commenting, export, AI actions, inviting, structural changes, and deletion. Preserve ownership for billing and lifecycle decisions. Resolve guest entitlement policy explicitly rather than inheriting the current owner's paid-sync check for every invited account.
2. **Replace owner-only reads deliberately.** Introduce a shared authorization service and audit document/page/section repositories, search, attachments, exports, AI operations, sync feeds, and collaboration endpoints. Do not perform a blanket replacement of `OwnerUserId` predicates. Shared discovery needs membership-aware feeds and removal events, not just the owner's existing sync cursor.
3. **Build invitation flows.** Implement invite, accept, decline, revoke, and role change, with expiring single-use acceptance tokens bound to the intended identity. Add shared-project discovery and local caching on all three platforms. Make initial downloading and offline availability visible.
4. **Enforce permissions during sessions.** Issue page-scoped rights and enforce them server-side, including read-only roles. Role changes and revocation invalidate live sessions and pending authorizations. Recheck access on resumed sessions and before accepting mutations; ticket expiration alone is not sufficient for immediate revocation.
5. **Add collaborative comments.** Store thread metadata, authorship, resolution state, and access checks in the backend. Anchor ranges with Yjs relative positions and page generation. If the referenced text is deleted, show an orphaned comment with its original quote instead of attaching it to unrelated text. Commenter access must not permit arbitrary document updates.
6. **Complete presence and save UI.** Show authenticated participant identity, colors, cursors, and active pages. Distinguish the same person's multiple devices. Remove stale presence after disconnection; never trust presence fields as proof of identity or permission.
7. **Define access-loss recovery.** On revocation, stop shared reads and writes and remove the project from shared navigation. Preserve pending local authored work through an explicit private recovery flow. Document that revocation cannot retract content already downloaded to another person's device.

### Rollout and compatibility

Expand the Release 1 feature flag to selected multi-user projects. Deploy access-policy changes and isolation tests before exposing invitations. Apply permissions consistently to old API routes as well as new endpoints. Maintain the compatibility gate for clients without shared-project support.

Keep structural editing restricted to the supported owner command path in this release. Collaborators must see resulting metadata refreshes even before collaborative outline commands arrive in Release 3.

### Acceptance and release gate

- Different accounts collaborate across browser, desktop, and iOS; each role can perform only its assigned actions.
- Attempts to guess room IDs, reuse tickets for another page, access another project's files, or mutate through old APIs fail without leaking content.
- Revocation and role downgrade take effect during active editing and after offline resume. Rejected offline edits remain privately recoverable.
- Invitations handle expiry, duplicate acceptance, wrong-account acceptance, and cancellation consistently.
- Anchored comments survive concurrent editing, page switching, and deletion of their referenced text.
- Shared discovery, removal, search, exports, and AI access obey the same membership policy. Existing private-document behavior remains intact.

Release evidence must include real multi-account sessions and each native platform, not only simulated role claims in unit tests.

## Release 3: Complete collaborative project workflow

### Scope and experience

Teams collaborate on the manuscript and its surrounding project: outline, scene order, notes, and storyboard. Writers can review suggestions, create named checkpoints, and recover earlier content without erasing concurrent work.

### Implementation work

1. **Add authoritative structural commands.** Implement create, rename, move, reorder, and delete with operation IDs, expected revisions, and server-enforced invariants. Use stable IDs and relative placement targets. Broadcast committed events with resumable cursors. On conflicts, refresh and explain the result; CRDT text merging does not resolve incompatible structural intentions.
2. **Extend device queues and project views.** Integrate command queues with shared project/outline/storyboard services. Preserve local drafts, replay idempotently, and display rejected operations. Handle delete-versus-edit by preserving a recovery copy. Initially restrict cross-project moves; permissions and page identity must be revalidated before supporting them.
3. **Choose merge rules by data type.** Use the established text engine for rich scene notes where useful. Use field-level revision checks for card properties and authoritative commands for ordering. Avoid uploading an entire storyboard over another user's changed field.
4. **Implement review suggestions.** Store proposer identity, base context, affected anchors, and decision state. Accept or reject through authorized, idempotent operations. Rebase or mark a suggestion as needing review when its target changes. Ensure offline decisions and competing reviewers cannot apply the same suggestion twice. AI suggestions follow the same guarded review path.
5. **Add named checkpoints and safe restore.** Record a manifest containing project structure revision and exact page checkpoints so manuscript history does not mix unrelated moments. Default restoration to a new copy. An in-place restore must fence active writers, create a new generation, preserve pending work, and reconnect clients against the new state.
6. **Prepare multi-instance operation.** Adopt and validate the selected Hocuspocus coordination approach before adding replicas. Define room ownership, cross-instance fan-out, persistence ordering, failover, and revocation propagation. A pub/sub backplane is not durable storage. Load-test representative manuscript sizes and simultaneous sessions against explicit latency and memory budgets.
7. **Complete operations and lifecycle support.** Add content-free diagnostics, alerting for persistence/projection lag, update-size and connection limits, storage retention policies, deletion handling, backup restoration drills, and incident procedures. Monitor durable-save latency separately from typing and network latency.

### Rollout and compatibility

Enable structural collaboration, suggestions, and history independently. Use protocol capability checks so clients that cannot preserve a new structure or review feature cannot mutate it. Expand capacity only after representative load and failover tests pass.

Before general availability, demonstrate rollback procedures that preserve acknowledged writing, and rehearse recovery of both collaborative text and relational project state to a consistent point.

### Acceptance and release gate

- Concurrent moves, reorder operations, card updates, deletion, and text edits yield valid project structure and visible conflict outcomes on every platform.
- Offline structural commands replay safely, and updates missed during disconnection are recovered from the event cursor or a fresh snapshot.
- Suggestions remain attributable, cannot be applied twice, and require review when their context becomes stale.
- Checkpoints reproduce the recorded manuscript and structure; restoring cannot silently overwrite other participants' pending writing.
- Multiple service instances converge under disconnects and restarts; permission revocation reaches all active sessions.
- Load, backup restoration, rolling upgrade, and incident exercises meet documented operating targets. Packaged Windows and native iOS suspend/resume behavior is included.

## Implementation discipline and handoff

Each release should finish with migrations, compatibility behavior, feature flags, operating instructions, test evidence, known limitations, and a concrete handoff for the next release. Record native/device and deployment checks separately from automated results.

Start implementation with Release 1's shared editor binding and a single-page prototype, then complete its persistence, migration, and write-guard work before enabling the pilot. Do not defer data-loss prevention or compatibility protection to later releases.

## Technical references

These references support the architecture selected in `realtime.md`. Verify dependency versions, integration APIs, deployment requirements, and licensing when implementation begins.

- [Tiptap collaborative editing](https://tiptap.dev/docs/hocuspocus/guides/collaborative-editing)
- [Hocuspocus overview](https://tiptap.dev/docs/hocuspocus/getting-started/overview)
- [Yjs document updates](https://docs.yjs.dev/api/document-updates)
- [Yjs offline support](https://docs.yjs.dev/getting-started/allowing-offline-editing)
- [Yjs ProseMirror integration and relative positions](https://docs.yjs.dev/ecosystem/editor-bindings/prosemirror)
