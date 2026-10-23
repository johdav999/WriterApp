# Manuscript sharing (Prompt 8)

Local testing update: the explicitly enabled loopback Staging profile now supports real-account sign-in and journaled real-time editing in a separate test database. See [two-account local test](local-collaboration-test.md). The default-off and normal-product boundaries described below still apply outside that profile.

Implemented 2026-10-18 as a gated foundation. `Collaboration:Sharing:Enabled` defaults to `false`. Sessions, commits, conversion and projection retain their independent default-off configuration. Sharing does not activate the product's collaborative editor: capabilities still report `FeatureAvailable=false` and `CanWriteCollaboratively=false`, and `createLiveEditor` remains closed. Use synthetic local manuscripts for acceptance. No production manuscript was converted or invitation delivered by this work.

## Authority and roles

`CollaborationAccessService` resolves the authenticated actor, resource owner, effective role and membership revision before shared prose, asset, search, export or AI-context retrieval. It checks active documents, deleted identities and the resource owner's active paid access. An invited editor does not need a separate paid subscription. Owner identity remains the document's existing owner; invitation and membership APIs cannot transfer it.

| Operation | Owner | Editor | Viewer |
| --- | --- | --- | --- |
| Read collaborative manuscript and committed socket state | Yes | Yes | Yes |
| Commit supported text updates | Yes | Yes | No |
| Consistent export and AI-context preparation | Yes | Yes | No |
| Manage invitations, members and supporting links | Yes | No | No |
| Read explicitly linked supporting material and its images | Yes | Yes | Yes |

Unsupported structure, legacy snapshot replacement, image uploads, AI application and history replacement retain their previous fences. A membership does not grant access to the private project, sibling manuscripts, cover assets, owner templates or unlinked supporting documents. Ordinary private owner flows keep their existing policies.

## Invitations and membership changes

Apply the checked-in SQLite `20261017200441_CollaborationSharing` or SQL Server `20261017200449_CollaborationSharingSqlServer` migration through the corresponding local development workflow. These add retained membership, invitation and explicit supporting-link records. Existing sessions are assigned their original resource owner and revoked/versioned; old purpose-v2 tickets fail closed. New tickets use Data Protection purpose-v3 and bind actor, resource owner and membership revision. Rollback refuses retained sharing state instead of deleting its authority history.

Owners create an invitation with a fresh operation UUID, editor/viewer role and exactly one target: a canonical backend account ID or email. Email acceptance requires an authenticated identity containing an actual email claim and explicit `email_verified=true`. Display names, `preferred_username`, UPN and native MSAL account IDs do not substitute for the backend subject or verified email. Providers without that email claim contract must use account-ID invitations.

Invitations expire within one hour to seven days. A cryptographically random 32-byte token is returned once; storage contains only its SHA-256 hash and a canonical request hash. The same operation and request replay returns metadata without reproducing the secret. Altered operation reuse conflicts. The UI retains the exact operation/request after an uncertain response so a retry cannot create another invitation accidentally. Owners can list and cancel pending invitations, including invitations whose response was lost. Acceptance binds both secret and authenticated target, and is atomic and single-use. Recipients can decline without supplying the secret. No automatic email sending is implemented; owners distribute the invitation ID/code themselves through an appropriate existing channel. Codes are sent in request bodies, never URL parameters.

Changes use expected membership revisions; stale changes fail. Revocation retains the row and increments its revision. Downgrade/revocation invalidates that actor's sessions and is rechecked by session bootstrap, authorization polling and the durable commit transaction. Retained receipts preserve the original editing actor. Re-invitation increments the retained revision; it cannot revive an old ticket. Active memberships are bounded to 100 per manuscript and per invited actor, and active pending invitations to 100 per manuscript.

## Offline work and revocation

The journal's historical `OwnerId` field means the local authenticated actor. Resource-owner identity is resolved by the server. Browser IndexedDB and native atomic-file journal identities additionally include nonzero membership revision. Owner revision zero preserves existing owner journal keys. Hosts must capture the current authenticated capability/discovery revision when constructing a guest scope and keep that scope immutable.

Ticket issuance requires the scope's captured membership revision to equal the current grant. The editor binding also checks capability/scope revision equality. Therefore an offline outbox created before revocation or a role change cannot silently move into a subsequent grant, even if it never observed the intervening revocation. The new revision opens a separate journal; original checkpoints, pending IDs and bytes remain available for private recovery. Observed access loss stops automatic delivery. Explicit recovery does not bypass server authority. Downloaded material cannot be remotely erased from another person's device.

Account changes immediately invalidate the shared UI and journal bindings; late asynchronous responses cannot adopt tokens, content or membership state into the next account. The sharing UI refreshes discovery every 15 seconds when idle and clears a displayed manuscript when its grant disappears. Server checks remain authoritative during that interval.

## Supporting material and consistent consumers

Supporting links are owner-managed, revisioned and explicit. Only active legacy supporting documents with the same resource owner and project can be linked; manuscripts cannot be linked as supporting material. Unlinking retains a revoked link revision. The linked resource is read-only for guests. Search and existing document-image reads resolve the same access policy, rechecking authority before returning content; guessed private sibling IDs do not widen access.

Consistent export/preview still requires a current structure/page watermark vector. Viewer exports and AI preparation are denied. Editors can request AI context with up to ten explicit linked supporting IDs; unauthorized IDs fail the whole request. Supporting snapshots include bounded pages, synopsis and outline/node content with an exact source hash. Limits are 2 MiB serialized source and 1,000 pages/nodes per supporting snapshot. The reader returns plain-text context; raw legacy source is not inserted into the manuscript editor. This is context preparation, not provider-backed AI apply/history acceptance.

## Routes and UI

Authenticated API root: `/api/collaboration`.

- `GET availability`, `GET owned`, `GET shared`, `GET invitations` return feature availability, complete scoped discovery and the recipient's pending inbox.
- `POST invitations/{id}/accept` accepts an addressed invitation using the authenticated identity alone. Target, expiry, revocation, single-use, owner entitlement and feature checks remain mandatory. The legacy `POST invitations/accept` still takes ID/code; `POST invitations/{id}/decline` declines an addressed invitation.
- `POST manuscripts/{id}/invitations/by-email` resolves exactly one identity recorded from the configured trusted issuer and returns no token. Editable profile email is not used; unknown or ambiguous addresses are rejected.
- `GET/POST manuscripts/{id}/invitations` lists/creates owner invitations; `POST .../invitations/{invitationId}/cancel` takes the expected revision.
- `GET .../members` lists members for the owner; `POST .../members/{userId}/change` changes role or revokes with an expected revision.
- `GET/POST .../supporting` lists owner choices or changes an explicit link; `GET .../supporting/{supportingId}` reads an authorized supporting snapshot.

The common `CollaborationSharingPanel` provides email invitations, one-click inbox acceptance, membership/supporting controls and discovery. The normal web shell exposes **Shared** and owner **Share** actions. `/app/documents/{id}/sharing` prepares legacy manuscripts through version-fenced conversion and explicit user acknowledgement. Collaborative manuscripts open in the full normal `DocumentEditor` and its existing text component; `/live` is only a compatibility redirect. `/collaboration/workspace` supplies authorized projected text and scoped document/section metadata without exposing private project siblings. Viewer projection lag is retried without losing the last view. Revocation stops the affected editor without signing out. Native hosts retain the read-only fallback until their live-editor integration is accepted.

Full-editor formatting uses the contract-v2 content schema plus the normal presentation extensions. y-tiptap's exact `__tiptap_position_migrations:root:default` metadata is validated separately on clients and the Node codec, with bounded records, numeric IDs, known fields and local-fragment targets. Unknown roots still fail closed. Toolbar commands are fenced after access loss as well as ordinary typing. The content schema/fingerprint is unchanged; deploy the client and codec together. Checked page/section AI Apply and history, whole-page restore, section organization, and shared notes now use dedicated collaborative operations. See [full-editor tools](realtime-editor-tools.md) for the current verified scope and remaining tool gaps; the full editor surface does not establish blanket tool parity.

## Verified boundaries

- Scoped SQLite regression: **180 passed, one explicit browser opt-in skip**; the opt-in test passed separately. Includes invitations, identity/expiry/idempotency, owner-funded grants, HTTP export/AI/search/images, private siblings, UI account/uncertain-response races, revision-bound native journals, migrations and previous legacy/durability fences.
- SQL Server Express scope: **22 passed**, including the same sharing/conversion cases, real socket case, model snapshots and upgrade/rollback preservation. All test databases are randomized and fixture-owned.
- Real Kestrel/Hocuspocus synthetic multi-account scenario: **22 assertions** cover owner/editor/viewer, durable actor attribution/broadcast, viewer write denial, downgrade/revocation closure and preserved private offline bytes. Previous real authorization and durable process-crash probes also pass.
- Rebuilt web/device bundles: **17 editor/browser checks** pass, including separation of old/new membership journals with pending bytes retained. Real provider/journal recovery: **17 checks on each database**, including owned browser-process restart. These desktop browser tests do not establish packaged WebView behavior.
- **14 Node tests**, scoped TypeScript checking, both editor builds and shared Node schema generation pass. Detailed evidence is in `artifacts/realtime-p8-{regression,sqlserver,node,browser,browser-sqlserver}.log` and `artifacts/realtime-p8-editor/browser-results.json`.

Remaining gates include packaged Windows/Android and actual iOS WebViews, cross-device account/navigation/suspend flows, hosted identity and verified-email claim configuration, real invitation distribution UX, product editor activation, other structure/lifecycle commands, AI approval/application/history, presence/comments, performance and backup/restore. No production, physical-device or external-tenant acceptance is claimed.
