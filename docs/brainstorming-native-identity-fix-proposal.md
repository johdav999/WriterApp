# Brainstorm native account mismatch — approved fix

Date: 2026-10-08. This supersedes the prior claim that the live desktop preparation
flow was fixed. The user confirms that Save, sync & prepare returns to Start a
conversation. The root cause is reproduced without user data or external requests.

## Evidence

MSAL returns `HomeAccountId.Identifier` (a native account/cache identifier) to
`DeviceAccountService`. Device Brainstorm hashes that identifier for its conversation
scope. The backend independently requires the hash of its canonical user ID from
`IUserIdResolver`. These can differ for the same authenticated person. A context
request then fails the existing ownership check, correctly signals access loss and
clears private review. The earlier fixture used `author` for both identifiers.

The mounted regression now uses native ID `author.native-tenant` and authenticated
backend user `author`. All four cases reproduce loss of the selected conversation
on confirmed preparation. See `artifacts/brainstorming-native-owner-before.log`.
The compiled production parent is now inspected by the test instead of supplying
its destination parameter by hand. That independently reproduced the literal label
bug in `artifacts/brainstorming-confirmation-binding-before.log`.

The missing Razor expression marker is corrected. This repairs the displayed
destination only; it does not fix the authenticated scope mismatch.

## Approved change

Keep shared mapping validation and all server ownership checks unchanged. Within
the device adapter, resolve the canonical user ID from authenticated `/api/auth/me`
before constructing conversation scopes. Bind the result to the exact configured
backend, native account and account generation; reject an account change during the
request. Local and server conversation scopes then carry the same canonical key.
Keep native sign-out/generation/revocation checks separate and intact.

Retain prior native-scoped conversation files, immutable mappings, journals,
acknowledgments and pending operations exactly where they are. Recover only the
discussion, unapplied proposals and unsent composer of the currently authenticated
native account into its verified canonical local namespace. Recovered copies have
no server mapping, outbox, application approvals or pending generation. No operation
is replayed, no account is merged, and no manuscript or AI request is generated.
Copied accepted/unknown requests become interrupted discussion evidence. Require
fresh context before Send. Never import another native account or backend namespace.

For offline reopening, retain only the previously verified canonical account hash
under a backend/native-account hash key; it authorizes no server operation. The
server still checks the actual bearer identity on every request. Online verification
must check the current account generation before accepting or caching the result.

Validate distinct native/backend identities through the actual authentication and
sync/context controllers, same-account token notifications, genuine invalidation,
offline reopening, original-file preservation, idempotent recovery and rejection of
foreign namespaces. Rebuild the actual desktop launch folder after those pass.

## Approval boundary and outcome

Automatic approval review rejected both the shared-mapping relaxation and the
narrower device-only canonical-resolution/recovery implementation because they
change authenticated identity/data-isolation boundaries. Neither rejected patch
was applied at the time of rejection. On 2026-10-08 the user explicitly approved
the device-only identity fix and message recovery above. That narrower change is
now applied; the shared-mapping relaxation remains rejected and unapplied. Shared
mapping equality and backend authorization remain strict. Verification and current
handoff are recorded in [native identity UAT](brainstorming-native-identity-uat.md).
