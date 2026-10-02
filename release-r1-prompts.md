# Real-time collaboration Release 1: implementation prompts

Status: implementation instructions, not evidence of completed work.

These prompts implement **Release 1: Reliable editing across your devices** from [realtime-releases.md](realtime-releases.md), using the architecture in [realtime.md](realtime.md). This is the collaboration release, not the earlier Windows desktop release described in `docs/release-1-desktop-prompts.md`.

## Outcome and execution

An existing owner opts a manuscript into collaboration and edits it across the web client/server, Windows desktop, and iOS. Live text updates and offline recovery preserve writing. Other accounts cannot join. Invitations, shared-project roles, comments, review suggestions, and collaborative storyboard/outline operations belong to later releases.

Run prompts in order in the same checkout, or after explicitly transferring all preceding changes. Copy the common instructions below together with the selected prompt into each new chat. Each prompt authorizes implementation of its scope, not production deployment or conversion of real user manuscripts. Keep pilot activation disabled until the final gates pass.

| Prompt | Deliverable |
|---|---|
| 1 | Repository audit, contracts, acceptance plan |
| 2 | Additive persistence schema, capability reporting, legacy write guards |
| 3 | Shared editor binding and isolated convergence prototype |
| 4 | Authenticated service and owner-only sessions |
| 5 | Durable server updates, acknowledgements, compaction |
| 6 | Browser and native persistence, offline replay |
| 7 | HTML projections and existing-consumer integration |
| 8 | Idempotent manuscript migration |
| 9 | Web, desktop, and iOS product integration |
| 10 | Deletion, external edits, and recovery workflows |
| 11 | Failure, compatibility, and performance verification |
| 12 | Packaged Windows and native iOS acceptance |
| 13 | Pilot operations, recovery rehearsal, release handoff |

## Common instructions for every prompt

```text
Implement the selected prompt for Prosa real-time collaboration Release 1.

Read realtime.md, the Release 1 section of realtime-releases.md, this prompt, applicable repository instructions, and docs/realtime-r1-status.md if it exists. Inspect current code and working-tree changes before editing. Preserve unrelated and preceding work; do not assume a new chat includes prior chat context.

Use one Yjs document per existing PageId and a shared Tiptap collaboration module across web and devices. Keep platform-neutral native services in WriterApp.Device.Shared, with platform-specific lifecycle adapters in the respective hosts. Keep .NET authoritative for identity, ownership, entitlement, document lifecycle, and session authorization.

For migrated text, collaborative binary state is authoritative; HTML is a projection. Never send remote edits through a legacy full-document save. Keep non-migrated document behavior intact. Gate compatibility before exposing unsupported content to a writable editor. Keep account, environment, page, and generation boundaries explicit.

Keep collaboration opt-in and disabled for real users during implementation. Use disposable test accounts and manuscripts. Do not introduce Release 2 or 3 features. Never store service secrets in clients, logs, or committed configuration. Do not change Prosa.Landing for this work.

Implement the production code required by this prompt, not just a proposal or mock, except where the prompt explicitly requests design or native evidence. Add meaningful tests for changed persistence, protocol, access, migration, and recovery behavior. Run scoped checks and report pre-existing failures separately. Do not claim a native or deployed outcome from a build or browser test.

Update docs/realtime-r1-status.md with completed work, exact changed paths, commands and results, unresolved risks, disabled features, and the next prompt's prerequisites. Keep test evidence under artifacts/realtime-r1/ using the repository's artifact conventions; do not commit generated outputs or private data. Update docs/device-development.md when device setup or architecture changes.

Finish with a concise report of delivered behavior, verification, remaining gates, and a concrete next-prompt handoff. If a required environment is unavailable, complete independent work and record the exact blocker; do not mark the gate complete.
```

## Prompt 1 — Audit the current paths and define the contracts

```text
Prepare the implementation map for collaboration Release 1. Inspect the actual web and device editor initialization/save paths, rich-content contract, local storage, DeviceSyncEngine, DocumentSyncService, database providers, account generation, connectivity, AI replacement, import/export, and document lifecycle handlers.

Create docs/realtime-r1-design.md and docs/realtime-r1-status.md. Map each existing content mutation path to its proposed treatment: collaborative transaction, protected projection, safe metadata command, or explicit rejection. Include ordinary web saves and background/device sync; do not limit the audit to the synchronization API.

Define proposed contracts for document mode, migration state, page generation, schema/protocol capability, session tickets, immutable client update envelopes, durable acknowledgements, local outbox identity, recovery copies, and projection watermarks. Separate client update identifiers, Yjs state vectors, and server commit sequences; none is interchangeable with another.

Inspect current official documentation and select compatible pinned Yjs, Tiptap collaboration, and Hocuspocus dependencies. Define how the selected service will persist before reporting a durable acknowledgement, including any required custom protocol. Do not assume default provider events or debounced storage hooks provide this guarantee.

Choose the database ownership boundary between Node and .NET. Prefer one migration owner and an authenticated internal persistence interface if it avoids two services independently managing relational schema. Document tradeoffs and a single-instance deployment topology.

Create an acceptance matrix mapping Release 1 requirements to later prompts. Set concrete initial performance budgets, representative page/manuscript sizes, and test hardware assumptions. Record missing Mac/Xcode/signing/device prerequisites without treating them as verified.

Acceptance: every known save path and release requirement has an implementation owner and verification step; protocol and persistence decisions are concrete enough for Prompt 2; no existing behavior changes in this design step.
```

## Prompt 2 — Add document modes, persistence metadata, and write guards

```text
Implement the additive database and shared-contract foundation defined in Prompt 1. Support the database providers actively supported by this repository and provide their required migrations.

Add document collaboration mode/migration state and page identity, schema, generation, and projection metadata. Establish the update/snapshot/receipt schema needed by the chosen persistence design. Introduce capability reporting so clients can determine whether they may read and write a document before opening its editor.

Guard all audited legacy text mutation routes for migrating and collaborative documents, including web saves, device uploads, imports, restores, and indirect repository writes. Use centralized mode checks inside the relevant transaction boundary. Allow only explicitly authorized internal projection writes, not a client-supplied bypass flag. Fence unsupported structural writes. Preserve safe operations only where their independence from text is demonstrated.

Return structured, actionable errors that preserve local drafts and distinguish upgrade-required, migration-in-progress, and wrong-generation cases. Keep all normal legacy behavior for non-migrated documents.

Acceptance: meaningful integration tests prove old upload and ordinary web paths cannot replace collaborative text, mutation races respect the mode fence, unsupported clients fail safely, and legacy documents still save and sync. Migration activation remains inaccessible to end users.
```

## Prompt 3 — Build the shared editor binding and convergence prototype

```text
Implement the common collaboration module and integrate it behind a disabled feature gate in WriterApp.Client/src/tiptap-editor.ts and device-editor.ts. Reuse the actual editor schema and align it with WriterApp.Shared/Editor/content-contract.json.

Bind Tiptap to Yjs with one document per PageId/generation. Seed fixture content once in a controlled harness; never initialize each connecting client by inserting its HTML. Apply remote updates through the collaboration binding, not setContent or HTML save callbacks. Configure undo to affect local edits only and identify remote transaction origins to prevent save echoes.

Create an isolated in-memory or loopback harness for two editor instances. It must not expose an unauthenticated service outside the test environment. Exercise different delivery orders, duplicate updates, formatting, tables, images, links, Unicode, and selection behavior.

Acceptance: the two instances converge, undo preserves the other instance's edits, supported content survives round trips, incompatible schemas are rejected before editing, and non-collaborative editor behavior remains intact. Clearly label this as an editor/protocol prototype, not proof of cloud durability or native acceptance.
```

## Prompt 4 — Implement owner-only sessions and the collaboration service

```text
Create the collaboration-service project and local startup configuration using the pinned dependencies. Add the .NET session endpoint and the authenticated service integration defined in Prompt 1.

Resolve page ownership through authoritative document relationships. Check current account status, entitlement, document mode, deletion state, generation, and schema/protocol compatibility. Issue short-lived audience-bound tickets scoped to the exact environment, owner, document, page, and allowed operation. Validate these constraints server-side on connection and renewal; room names and presence payloads never authorize access.

Support reconnect and ticket renewal without dropping locally queued edits. Define ongoing authorization checks and invalidate sessions for deletion, account removal, or disabled access. Ensure internal persistence endpoints are not callable using ordinary client credentials. Apply bounded connection/update limits and redact credentials from logs.

Use disabled/test-only activation until durable persistence is complete. Do not display a cloud-saved state in this intermediate implementation.

Acceptance: real endpoint/service tests cover valid owner sessions, another account, wrong page/environment/audience, expired or tampered tickets, incompatible schema, and renewal. No invitations or cross-account sharing are introduced.
```

## Prompt 5 — Persist updates and acknowledge durable saves

```text
Implement the production update storage path, replay, snapshots, and compaction. Connect it to the service from Prompt 4.

Use immutable update envelopes and stable retry identifiers scoped to the correct identity/generation. Reject reuse of an identifier for different payload bytes. Atomically commit the update and receipt before sending its durable acknowledgement. A retry after a lost acknowledgement must return the same committed outcome. Only accepted updates may enter authoritative shared state; a rejected or failed write must not contaminate it.

Define reconnect synchronization so updates transmitted by the provider's initial state exchange cannot bypass durable acceptance or the client's acknowledgement accounting. Track server commit sequences independently from Yjs state vectors. Enforce current page lifecycle/generation at the durable mutation boundary.

Reconstruct state from snapshots plus later updates. Compact at an explicit watermark without racing new commits or destroying required retry receipts and recovery history. Document retention. Survive interruption before and after every critical transaction boundary.

Acceptance: fault-injection tests cover database rejection, server crash before/after commit, lost acknowledgement, duplicate/reordered submissions, concurrent compaction, and restart. All acknowledged content survives reconstruction; no failed update is reported as saved. Provide an isolated backup/restore check of the persisted data.
```

## Prompt 6 — Add browser and native offline persistence

```text
Implement continuous local persistence of Yjs state and pending update envelopes for web and shared device services. Use the chosen browser store and a durable native bridge for desktop/iOS, rather than relying solely on WebView storage.

Scope local records by environment, account, document, page, and generation. Store enough state to reload the editor and retry unchanged submissions after a crash. Remove pending work only after the matching durable server acknowledgement; preserve the recoverable local document state afterward.

Load local state before connecting. Coordinate local storage completion, network synchronization, and acknowledgement handling without feedback loops or outbox races. Reuse existing account-generation and connectivity services; cancel obsolete sessions and prevent work from being sent after account switching. Do not rely on shutdown callbacks or continuous iOS background execution.

Handle disk-full, corrupt/incompatible state, interrupted local writes, and temporary network failure explicitly. Avoid labeling edits as locally saved before persistence completes. Keep legacy FileLocalDocumentStore and DeviceSyncEngine behavior for non-migrated documents.

Acceptance: tests verify force-restart recovery, missing acknowledgement replay, offline edits from multiple instances, duplicate delivery, account/environment isolation, disk failure, and schema mismatch. Distinguish host-simulated tests from actual mobile suspend/termination evidence required later.
```

## Prompt 7 — Project committed content for existing consumers

```text
Implement deterministic HTML projection from committed collaborative state using the supported rich-content schema. Record its exact generation and committed watermark. Make projection retries idempotent and prevent older work from replacing a newer projection.

Integrate existing rendering, word counts, search indexing, AI context, and exports with the projection. Define freshness requirements per consumer: export and AI must wait for the requested committed state or return a clear retryable status; browsing/search may use an explicitly documented eventual-consistency policy. Unsent local content must not be falsely represented as included in a server export.

Separate projection persistence from legacy content mutation and sync triggers. Ensure a generated HTML update does not re-enter DeviceSyncEngine as a competing user upload or start an infinite projection/save loop. Preserve existing sanitization and attachment access controls.

Acceptance: tests cover projection lag/failure, restart/retry, concurrent updates, schema fidelity, stale-worker completion, and absence of sync echoes. Verify exported/AI-selected content matches the requested watermark and ordinary legacy consumers still work.
```

## Prompt 8 — Implement controlled manuscript migration

```text
Implement the idempotent legacy-to-collaborative migration state machine behind an internal/test activation gate. Do not migrate existing user manuscripts as part of development.

Check the initiating client's synchronized version and unresolved conflicts. Fence new legacy writes transactionally, capture a recoverable source backup, and seed each page exactly once using deterministic migration identities. Validate content fidelity before activation. Stage all pages before making the manuscript collaborative; clients must never see a partially activated manuscript.

Support restart after failure at each step, and define safe cancellation before activation. Serialize migration attempts and handle concurrent legacy saves, document deletion, and source-version changes. Prevent repeated conversion from duplicating content.

Recognize that another offline device may still have a pre-migration draft. Detect it on reconnect and preserve it for recovery/review; do not treat its HTML as a fresh authoritative replacement or silently discard it.

Acceptance: integration tests interrupt every stage, retry requests, race a save/delete, include multiple pages and rich content, and reconnect an old client with an offline draft. Source backups remain usable, activation is atomic from the user's perspective, and legacy documents remain unaffected.
```

## Prompt 9 — Integrate the end-user workflow on all platforms

```text
Wire the completed collaboration engine into the actual web, Windows, and iOS editing flows. Add owner-only opt-in UI gated by pilot eligibility and the global feature flag. Explain the compatibility transition, save pending local work, check migration prerequisites, and invoke the migration flow without duplicating it in UI code.

Add accurate local/cloud save states, pending-change counts where useful, reconnect/retry behavior, upgrade-required messages, and active device-session presence/cursors. Distinguish one owner's multiple devices. Presence must be ephemeral and its displayed identity derived from the authenticated session.

Manage page switching, editor disposal, navigation, multiple windows, session renewal, and device suspend/resume without losing pending updates or applying updates to the wrong page. Avoid loading every manuscript page into the active editor. Ensure the current client cannot accidentally route collaborative content through legacy save handlers.

Disable unsupported structural/import/restore operations with a clear explanation for migrated documents. Do not add team invitations, roles, comments, or storyboard collaboration.

Acceptance: browser and available device-host checks cover opt-in, switching between legacy and collaborative documents, two simultaneous sessions, presence expiry, offline status, navigation, and reconnect. Leave packaged/native acceptance explicitly pending for Prompt 12 where it cannot yet be demonstrated.
```

## Prompt 10 — Complete deletion, external-edit, and recovery behavior

```text
Audit and complete every destructive or whole-content operation identified in Prompt 1. Implement authoritative deletion/tombstones and generation fences so active sessions and offline updates cannot resurrect a deleted page or manuscript. Preserve rejected local work through a discoverable private recovery-copy flow.

Apply supported AI replacements as collaborative editor transactions with stable target anchors and validated context. If the target changed while a request was running, present the proposal for review instead of overwriting concurrent edits. Ensure applying twice cannot duplicate the result. Disable any remaining AI, import, duplicate, or restore action whose current implementation cannot preserve collaborative state safely; support safe copy/export operations where feasible and document the exact restrictions.

Handle account deletion, sign-out, expired entitlement, disabled pilot access, and generation mismatch consistently. Local recovery must not require unauthorized cloud reads. Revalidate authorization before submitting recovered work.

Acceptance: race tests cover edit-versus-delete, offline reconnect after deletion, AI result after concurrent edits, stale generations, account switching, and loss of access. Recovery copies can be opened/exported and do not overwrite the shared source. No unresolved bypass can mutate authoritative text through HTML.
```

## Prompt 11 — Verify failures, compatibility, and performance

```text
Run the end-to-end automated acceptance matrix and close defects found in Releases 1's implementation. Reuse earlier meaningful tests and add coverage for gaps rather than duplicating the implementation in assertions.

Exercise two independent browser profiles, service/database interruption, packet duplication/reordering, lost acknowledgements, offline edits, crash/restart, compaction, projection backlog, interrupted migration, old clients, and schema mismatch. Verify both converged content and save-status truthfulness. Include non-migrated document regression checks.

Measure the page sizes, manuscript sizes, typing latency, reconnect time, memory, and durable-save latency against Prompt 1's budgets. Report hardware, workload, measurements, and failures; do not silently relax budgets to obtain a pass. Confirm that network or projection delays do not block local typing.

Audit logs and errors for manuscript content, tokens, and account leakage. Validate the single-instance deployment's bounded capacity and failure behavior. Multi-instance scaling remains Release 3 work.

Acceptance: every automated gate has reproducible evidence or an explicit unresolved defect. Fix in-scope failures and rerun affected checks. Produce a compact verification report and the exact manual scripts needed for Prompt 12; do not claim native acceptance from these tests.
```

## Prompt 12 — Perform packaged Windows and native iOS acceptance

```text
Build/package the supported Windows application and the actual iOS application using the required Mac/Xcode/signing environment. Use isolated accounts and disposable manuscripts, and confirm the running apps contain the current changes.

Execute the same-owner acceptance scenario with two browsers, packaged Windows, and an actual iOS device: edit the same paragraph, make distinct and overlapping changes, disconnect devices, continue writing, terminate/restart an app, reconnect, and verify retained edits and convergence. Check local undo, formatting, tables/images/links, Unicode and keyboard composition, navigation, save status, and session presence.

On iOS test background suspension, foreground resume, network transitions, and forced termination with pending local edits. On Windows test multiple windows and abrupt process exit. Check account switching, unsupported-version handling, and deletion recovery on the native surfaces.

Record build identity, device/OS versions, steps, observed results, and evidence locations. Fix discovered defects and rerun affected scenarios. Never use a user's working manuscript for acceptance.

If Mac access, signing, installation, or physical-device access is unavailable, finish the preparation and available platform work, document exact prerequisites and the runnable script, and mark the native gate blocked. Do not substitute simulator/browser results for the actual-device requirement or mark Release 1 complete.
```

## Prompt 13 — Prepare pilot operations and the release handoff

```text
Complete the operational package for the single-instance owner-only pilot. Reconcile every Release 1 requirement against implementation and evidence, and fix remaining code/documentation gaps within available environments.

Document deployment order: additive database migration and server guards, service configuration and secret provisioning, compatible client releases, then opt-in enablement. Provide health checks, update/connection limits, alerts for persistence failures and projection lag, safe log collection, and a pilot capacity bound supported by measured evidence.

Provide independent controls to stop new migrations and to stop cloud writes. Confirm these controls never re-enable legacy HTML replacement for migrated documents and preserve local recovery. Document the controlled, exceptional procedure for returning a document to legacy mode: fence writes, capture current state, reconcile pending work, change generation, and validate before enabling legacy writes.

Rehearse database/service backup restoration in an isolated environment, including page generations, retry receipts, projection state, and document mode. Verify all acknowledged writing and consistent relational state. Prepare incident procedures for service outage, storage failure, incompatible client release, and failed migration.

Write docs/realtime-r1-release.md with delivered scope, migration/rollback runbooks, validation evidence, remaining limitations, and Release 2 prerequisites. Update docs/realtime-r1-status.md and device-development documentation. Leave production deployment and conversion of real manuscripts for a separately authorized rollout.

Acceptance: mark the release ready only when automated, browser, packaged Windows, native iOS, and recovery gates have passed. Otherwise report the exact outstanding gates and their owners/prerequisites; do not label the cross-platform release complete. The final handoff must be sufficient for a new chat to continue from this checkout without relying on conversation history.
```
