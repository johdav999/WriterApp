# Brainstorm native identity correction and message recovery

Date: 2026-10-08. User explicitly approved the identity fix and message recovery.
The implemented fix and synthetic regressions are complete. Live Microsoft account,
native interactive Send and provider acceptance remain separate open gates.

## Root cause

MSAL's `HomeAccountId.Identifier` identifies the native account/cache; it is not
necessarily the backend's canonical `IUserIdResolver` user ID. Device Brainstorm
previously hashed the native value into both conversation scopes. The backend
correctly rejected the scope, signalled access loss and cleared the selected chat.
Prior success fixtures used the same value for both IDs and missed this defect.
The revised mounted regression uses native `author.native-tenant` and backend
`author`, reproducing the return to Start a conversation in all four cases before
the fix (`artifacts/brainstorming-native-owner-before.log`).

The desktop parent also passed the literal string
`BrainstormSessions.SynchronizationDestination`. Its missing Razor `@` is fixed.
The regression inspects the compiled production parent render fragment, rather
than supplying the correct destination by hand.

## Implemented correction

`DeviceBrainstormingSessions` resolves the canonical user ID through authenticated
`api/auth/me`, bound to the configured backend and native account generation. It
checks generation/account again before accepting or persisting the result. Shared
mapping equality, server ownership, bearer authentication and revocation checks
remain unchanged. Same-account token notifications do not reset a checked chat;
sign-out, reauthentication, rejection and genuine lost document access still do.

On authenticated scope creation, `LocalBrainstormingStore` copies that native
account's existing discussion, proposals and unsent composer for the current local
document into the verified canonical namespace. Original files, mappings,
acknowledgments, local receipts and pending operations remain byte-for-byte intact.
Copies have no mapping, outbox, pending generation, application approval or operation
journal. Unknown/accepted/streaming requests become interrupted discussion evidence.
The process skips deleted conversations and existing canonical copies, retaining
later canonical edits. Other native accounts and backend partitions are excluded.
Recovery neither writes a manuscript nor dispatches an AI request.

A backend/native keyed canonical hash supports offline reopening after verification.
This cache authorizes no server operation. Before first online verification, signed-in
offline writing remains in its native local namespace; remote leases require online
verification. A fresh online adapter verifies the bearer profile again.

The earlier destination-specific Save, sync & prepare confirmation is retained. It
flushes advancing pending DOM text through ordinary save/recovery/sync v5, refreshes
matching acknowledgment metadata and checks exact server context. Preparation does
not Send. Recovered sessions require fresh context before an explicit Send.

## Verification

Evidence: `artifacts/brainstorming-p12-native-identity`.

| Check | Result and limit |
| --- | --- |
| Combined writing/Brainstorm/sync/auth/provider regressions | 846 passed, zero failed/skipped; `combined-regressions.log` and TRX. Includes all ten focused identity/binding cases. |
| Mounted desktop preparation and Send | All four distinct-ID cases pass through real device authentication, save/recovery, sync v5 and context controllers. Composer and selected session survive confirmed preparation; explicit Send reaches one deterministic test provider call. All four genuine invalidations clear active review. |
| Recovery and isolation | Original file bytes and pending operations retained; discussion/proposals/Unicode composer recovered; no authority/outbox copied; foreign account/backend and tombstones excluded; canonical edits survive repeated recovery; offline reopening and sign-out guards pass. |
| Failed canonical verification | Unauthenticated/missing-owner/403 profiles and an account change during verification reject without caching an identity or recovering data. |
| Device authentication | 14 passed separately; `device-authentication.log`. |
| Production browser modules/rendered components | All ten browser suites pass. Scripted editor/IndexedDB/voice boundaries; no real microphone/provider/native interaction claim. |
| Server/client Release and desktop Debug | Isolated and normal launch-folder builds pass with zero warnings/errors. Two existing test-project warnings remain in the combined test build. |
| Windows startup | Rebuilt isolated process starts and initializes isolated storage, then only that owned PID is closed. Startup does not establish interactive native acceptance. |
| Preservation and binary match | All 197 initial Git status entries retained. Normal device/UI/shared assemblies match verified isolated hashes. Normal desktop metadata has an empty validation-data override and uses ordinary device storage. No user process or document was modified during verification. |

The earlier failed focused recovery logs are superseded by the successful combined
TRX; their failures were test fixture construction/assertion errors, not a passing
recovery claim. The shared identity/mapping relaxation was rejected and remains
unapplied. The narrower device-only correction proceeded only after explicit user
approval. See [approved proposal](brainstorming-native-identity-fix-proposal.md).

## Current handoff and author flow

Continue in the same checkout with all uncommitted work intact. The normal rebuilt
executable is `WriterApp.Desktop/bin/Debug/net10.0-windows10.0.19041.0/win-x64/WriterApp.Desktop.exe`.
It targets the ordinary Development backend; retain any existing intended backend
override when launching. Do not use the isolated validation-data build for real
conversations. Reopen Prosa, sign in to the same backend/account, and open the same
document's Brainstorm. Recovery runs on scope discovery; History exposes the
recovered conversation and opening it restores its unsent message. Prepare context,
confirm Save, sync & prepare, then explicitly Send. No user-data migration was run
outside this authenticated application path.

Collect live Microsoft-account/native interactive evidence for that exact flow.
Do not repeat the equal-ID fixture or relax server ownership as a workaround.
Keep original operations for separate inspection; do not replay them as recovered
authority. Remaining P12 provider, audio, accessibility, real device exchange and
deployed/SQL acceptance gates stay open as documented in the status and P12 UAT.
