# Presence, carets and comments (Prompt 9)

Implemented as a gated foundation on 2026-10-18. Product collaborative editor capabilities and `createLiveEditor` remain disabled. Set `.NET Collaboration:Interactions:Enabled=true` only for an authorized staging/test environment; it defaults false. Node presence additionally requires `COLLABORATION_INTERACTIONS_ENABLED=true`. Existing session, commit, migration and sharing gates remain independent. No real manuscript was converted or production setting changed.

## Authenticated ephemeral presence

Ordinary Yjs awareness frames remain blocked. A custom stateless `{type:"presence",cursor:null|{start,end}}` message is accepted only after authenticated session resolution and only when both interaction gates permit it. The sender cannot specify an actor, role, name, color, room or connection identity. Unknown fields, malformed/noncanonical positions and foreign document roots fail closed. Rate-limited cursor updates are discarded without interrupting durable text sessions. Identity comes from the backend-authorized session; the server broadcasts a canonical `presenceSnapshot` roster. Display text is inserted with `textContent`, never interpreted as HTML.

Presence is kept in bounded process memory, outside Y.Doc, update receipts, projections and local journals. There are at most 64 participants per page/generation room, a 4 KiB input limit, and a minimum 100 ms interval between accepted updates per connection. The editor sends selection updates at most every 200 ms and heartbeats every five seconds. Entries expire after 15 seconds without an update. Disconnect, account change, access loss and server shutdown clear participant state. Backend authorization is rechecked for the sender and roster recipients; normal session polling also disconnects revoked/expired sessions. A dropped connection can appear until its disconnect is observed or its entry expires.

Disabling only the interactions feature removes presence while leaving an otherwise authorized durable text session intact. Revoking manuscript access still closes that session and preserves its private journal.

The shared web/device editor binding renders an accessible participant bar, remote carets and selections. Local unsent text never travels through presence: while text is pending, the client publishes no cursor. Presence cannot mutate manuscript text and is never a cloud-save signal. Display names come from backend account profiles; a missing name becomes “Collaborator”. Multiple connections for one actor collapse to one participant and one cursor, preferring a connection with a cursor over a cursorless connection. Clients cannot supply names or colors as authority.

The normal editor mounts the collaborator bar above its toolbar, outside manuscript paper. Colored initials and an explicit count open a keyboard-accessible list of people on this page; the local account is marked “You”, including when names match. Colors are stable per actor and match remote selections/cursors. Escape and outside interaction dismiss the list. Live, connecting, disconnected and unavailable states come from the durable session's connection/access state, independently of the save indicator. Disconnect/access loss removes stale names and closes the list. Cursor name badges have bounded width and flip below/left at viewport edges. Presence UI is hidden for print. See [collaborator UI UAT](collaborator-ui-uat.md) for evidence and acceptance boundaries.

## Comment authority and persistence

Comments use separate thread, immutable reply and operation-receipt tables. SQLite migration `20261018060230_CollaborationComments` and SQL Server migration `20261018060233_CollaborationCommentsSqlServer` add them without changing manuscript bytes or legacy annotations. Downgrade refuses retained comments rather than dropping their bodies/history.

| Operation | Owner | Editor | Viewer |
| --- | --- | --- | --- |
| Read comments on an authorized manuscript | Yes | Yes | Yes |
| Create an anchored thread or append a reply | Yes | Yes | No |
| Resolve/reopen a thread | Any thread | Own thread | No |
| Import legacy annotations as detached threads | Yes | No | No |

`CollaborationAccessService` checks current manuscript access and owner-funded entitlement before content retrieval or mutation. Mutations bind the captured membership revision, page and immutable generation. Revoked members cannot read comments or replay old operations; a subsequent grant does not adopt old requests. All changes serialize through the same document barrier used by sharing/commits. Comments do not bypass existing structure, snapshot or AI write guards.

Each create/reply/resolve/reopen request carries a fresh operation UUID. The durable receipt binds authenticated actor, target, exact request hash and resulting revision. Lost responses can be retried with the same request; changed operation reuse conflicts. Threads use expected-revision compare-and-set. Replies preserve their original author and creation time; there is no destructive edit/delete endpoint. Resolving a thread prevents new replies until an authorized reopen. Comment bodies are plain text, limited to 4,000 UTF-16 characters, and rendered as text by the shared UI. Requests are bounded to 64 KiB; a generation has at most 200 threads, 100 replies per thread and 1,000 replies in total.

## Relative positions and detached passages

An anchor stores base64-encoded canonical Yjs relative positions for both ends, scoped by page and generation. The editor uses right affinity at the start and left affinity at the end so another author's boundary insertion stays outside the selected passage. A caret uses one right-associated position. Empty selections cannot create a new comment.

Creating a thread requires the exact current committed sequence. The bounded shared Node codec reconstructs the checksummed committed snapshot/tail, validates its actual editor schema/fingerprint, decodes the relative positions and verifies that they resolve within the `default` editor fragment in document order. Anchors referring only to private unsent edits are refused. A concurrent text commit causes a sequence conflict; refresh the comments and select the passage again while retaining the comment draft. Client-provided character offsets, HTML and claimed author IDs are never accepted as authority.

Comment reads resolve the original relative positions against a consistent committed state. Insertions move the anchor with its original passage. Deleting the passage collapses or invalidates its range and returns `Detached=true`; the body/replies remain readable and the editor removes the highlight. Reads for retained tombstoned generations are allowed under current manuscript access and return detached threads; new writes to those generations are refused. No operation guesses a replacement quote or silently reattaches a thread to different text.

Legacy integer offsets cannot identify Yjs items. The owner can explicitly import retained page annotations and unambiguously section-linked scene annotations as detached threads. Scene annotations go to the first active page in that section, with same-project/document checks. Imported source identities make repeated import idempotent. Original annotations, offsets, kind/text and resolved state remain in their existing tables; no legacy writer is enabled. Unmapped scene material remains in legacy recovery rather than being assigned to another document. Rich anchor mapping is deferred until it can be proved against the original migration snapshot.

## Endpoints and shared UI

Authenticated root: `/api/documents/{documentId}/pages/{pageId}/collaboration/comments`.

- `POST read` takes `{generation}` and returns the consistent sequence and threads, including resolved/detached status, anchors and immutable author-attributed messages.
- `POST` creates a thread with `{operationId,generation,membershipRevision,expectedSequence,anchor,body}`.
- `POST {threadId}` takes `{operationId,generation,membershipRevision,expectedRevision,action,body?}`; actions are `reply`, `resolve`, `reopen`.
- `POST import-legacy` takes `{generation}` and is owner-only.

Responses are `no-store`; access loss returns 403 and stale sequence/revision, malformed anchors or unsupported source return a structured conflict. No provider-backed comment API is used. Backend authority is always required.

`CollaborationCommentsPanel` supplies common read/create/reply/resolve/reopen/import controls. It polls every five seconds when idle. It keys state by account context, actor, document, page, generation and membership revision; account/page changes cancel work, clear text and ignore late completions. Uncertain requests retain their exact UUID/body/anchor for explicit retry. After an error, the user can retain the draft and refresh before starting a new request. Comments require a connection; unsent comment text is component state, not a durable offline comment outbox. The UI says so explicitly. Manuscript text retains its independent durable journal and private recovery.

The native journal-backed editor adapter connects selection capture, comment highlighting and passage navigation to the common panel. The web/native sharing read view can read comments; editors can reply or resolve permitted threads there. New passage anchoring requires the gated editor binding. Production authoring routes remain closed pending platform acceptance. Comment decorations and selection/presence transactions do not enter local text undo history. Local undo/redo continues to track only local authoring origins and preserves another author's committed edits.

## Evidence and remaining gates

See [implementation status](realtime-implementation-status.md) for final test counts and evidence paths. Verification includes actual .NET HTTP permissions/receipts, SQLite/SQL Server migrations, retained legacy annotations, thread/account races, real Hocuspocus identity/spoof/revocation probes, both rebuilt editor bundles, and a combined browser journey through authenticated services. Synthetic accounts and capability grants in these harnesses do not activate the product.

Packaged Windows/Android, actual iOS WebViews, physical-device navigation/suspend/IME and accessibility, production identity display policy, hosted operation/performance and backup/restore remain acceptance gates. Presence is single-process; multi-worker distribution requires an authority-preserving fan-out adapter. Comment polling is bounded per open page but operational budgets remain Prompt 10 work. No physical/native, external-tenant or production acceptance is claimed.
