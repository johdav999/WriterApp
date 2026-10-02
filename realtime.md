# Real-time collaboration in Prosa

Implement collaboration through a shared Tiptap/Yjs editing layer across web, desktop, and mobile, with Prosa's .NET server managing access and a dedicated service synchronizing edits. The existing architecture supports this well, but the current document-sync mechanism needs a different path for collaborative content.

## Current foundation

Repository inspection on 2026-10-02 identified:

- Tiptap editors for web and devices.
- Windows desktop and iOS apps sharing `WriterApp.Device.Shared`.
- Durable offline synchronization with operation IDs and version checks.
- Owner-based document access.

Today, `WriterApp.Device.Shared/Services/DeviceSyncEngine.cs` uploads document snapshots, and `Application/Documents/DocumentSyncService.cs` rejects competing versions and preserves conflicts. That protects writing, but cannot merge two people typing into the same paragraph.

## 1. Give all platforms the same collaboration engine

Add a shared TypeScript module used by both editor entry points. Bind Tiptap to **Yjs**, which represents edits as mergeable operations. Each keystroke appears locally immediately; small updates travel to other participants.

Yjs supports merging updates received out of order or after an offline period. Tiptap provides the editor integration and collaborative cursor extensions. See the [Tiptap collaboration guide](https://tiptap.dev/docs/hocuspocus/guides/collaborative-editing).

Start with one collaborative document per existing Prosa `PageId`, loading only the pages being edited. This limits the amount of manuscript state mobile devices must keep active.

| Component | Responsibility |
|---|---|
| Web editor | Shared collaboration module and browser persistence |
| Desktop editor | Same module, with durable device storage |
| Mobile editor | Same module, with persistence and suspend/resume handling |
| .NET backend | Accounts, invitations, permissions, project structure, exports |
| Collaboration service | Live text updates, presence, reconnect synchronization |
| Database | Durable collaborative state, checkpoints, membership |

## 2. Add a small collaboration service beside .NET

The recommended starting point is **Hocuspocus**, a Node.js WebSocket backend designed for Yjs/Tiptap. It avoids building the collaboration protocol from scratch. See the [Hocuspocus documentation](https://tiptap.dev/docs/hocuspocus/getting-started/overview).

The connection flow:

1. The user opens a shared manuscript.
2. Prosa's backend verifies membership and issues a short-lived collaboration ticket.
3. The editor connects to the authorized page.
4. It exchanges missing updates and begins live editing.
5. The service persists updates and periodically compacts them into snapshots.

“Saved to cloud” should mean the server durably stored the changes, rather than merely received them.

SignalR could handle project notifications, but adding SignalR alone would not solve simultaneous text editing. An entirely .NET transport is possible, with additional work to integrate and maintain the Yjs protocol.

## 3. Make collaborative state authoritative

The most important migration rule: **existing snapshot uploads must not overwrite collaboratively edited text.**

For collaboration-enabled documents:

- Persist Yjs state and updates as the authoritative text.
- Generate HTML for existing rendering, search, AI context, and export.
- Route accepted AI edits through editor transactions.
- Prevent older clients from uploading replacement HTML.
- Convert existing content once, using a controlled migration.
- Keep project titles, ordering, moves, and deletion as versioned server operations initially.

This requires changes to both device synchronization and ordinary web save endpoints. Merely adding collaboration extensions would leave competing save paths.

## 4. Introduce shared-project permissions

The current owner filters need a centralized access policy supporting roles such as **owner, editor, commenter, and viewer**.

That policy must cover document reads, collaboration connections, writes, attachments, search, exports, and AI operations. Removing a collaborator should close their active connection and reject future updates.

If somebody edits offline after their access has been revoked, preserve their work as a private recovery copy when they reconnect. Do not merge it into the shared manuscript.

## 5. Preserve offline writing and handle mobile interruptions

Persist collaborative updates locally as they happen. Browser IndexedDB is an available Yjs option; for desktop and mobile, integrate persistence with Prosa's native storage boundary and test recovery after forced termination. See [Yjs offline support](https://docs.yjs.dev/getting-started/allowing-offline-editing).

Mobile should reconnect and exchange missing updates on resume, without depending on a continuously running background connection.

All clients also need compatible editor schemas. An older app must not silently remove formatting introduced by a newer client.

For the writing experience, add participant avatars, colored cursors, connection status, and undo scoped to the user's own edits. Comments should use stable text anchors so they follow edits; Yjs recommends relative positions for this purpose. See [Yjs ProseMirror guidance](https://docs.yjs.dev/ecosystem/editor-bindings/prosemirror).

## Delivery stages

1. **Cross-platform proof:** two browsers, desktop, and iOS editing one page; verify formatting, undo, offline reconnection, and crash recovery.
2. **Production foundation:** durable storage, migration, permissions, invitations, revocation, and protection against legacy saves.
3. **Writer workflow:** comments, presence across chapters, version checkpoints, and safe AI edits.
4. **Broader collaboration:** storyboard and outline updates, suggestion/review mode, and scaling.

The first acceptance scenario should be concrete: one writer edits on desktop while another edits the same paragraph on mobile; both disconnect, continue writing, reconnect, and retain both sets of edits. The largest work is making that reliable across storage, permissions, and every existing save path.

This document is an architecture proposal, not a record of implemented collaboration functionality.
