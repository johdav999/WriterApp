# Brainstorm generation SQL transaction correction

Date: 2026-10-08. Generation transaction correction implemented, SQL-tested and
loaded into the author's localhost backend. Actual native/provider Send remains
an explicit author action and acceptance gate.

## Report and root cause

After the approved schema update, the author received "Conversation unavailable.
Inspect the operation before retrying." The composer remained visible, with
"A saved operation needs attention", disabled preparation/Send and the inspect
button. The schema update resolved missing-table failures, but the earlier
Read/Inspect probes did not exercise generation acceptance or terminal persistence.

`BrainstormingConversationService` began serializable transactions directly in
both generation acceptance and terminal persistence. Production SQL Server uses
`EnableRetryOnFailure`; its execution strategy rejects queries inside those
transactions unless the complete transaction runs through `CreateExecutionStrategy`.
The controller masked this `InvalidOperationException` with the reported message.
SQLite tests could not reproduce the SQL-specific constraint. The previous SQL
acceptance runner tested session CAS and recovery, but not generation.

The new opt-in SQL conversation check reproduced the exact execution-strategy
exception before a provider call in a fresh owned SQL Express database. The owned
database was removed. Read-only inspection of the configured live database found
the retained session, no generation ledger for the pending operation, and no
pending Brainstorm migrations. Cache metadata confirmed a 114-character composer
and a retained send. No manuscript/composer text was logged or dispatched.

## Correction and regression boundary

Only acceptance and terminal database work run through the retrying execution
strategy. Each retry clears tracking and starts from durable records. Acceptance
rechecks scope, session revision and active requests; its exact immutable operation
hash remains authoritative. A lost acceptance-commit acknowledgment returns the
existing events for inspection and never dispatches that operation to a provider.

Terminal retries recognize the exact persisted terminal snapshot before adding an
assistant turn or advancing the session revision. A different terminal outcome is
rejected. Provider dispatch, usage charging and metering remain outside these
transaction retry delegates. Ownership, source/context checks, quota policy and
the inspect-before-retry rule remain unchanged. No migration or wire change.

`Tests/Brainstorming.SqlServerAcceptance/ConversationAcceptance.cs` now exercises
the real service against SQL Server with its production retry strategy and a local
synthetic streaming provider. Four conversation cases cover normal completion,
transient failure before acceptance commit, a lost acceptance acknowledgment and
a lost terminal acknowledgment. Each checks restart inspection, immutable replay,
no duplicate provider dispatch/charge/assistant turn, and unchanged rich writing.
The ambiguous acceptance case proves zero provider calls/charges and an inspectable
Unknown outcome. The complete runner passes nine groups and removes its exact
fresh GUID-named database.

## Verification and actual runtime

- SQL red/green evidence: `artifacts/brainstorming-conversation-sql/sql-before.json`
  and `sql-after.json`; detailed logs in `artifacts/brainstorming-save-read/`.
- Combined regressions: 849 passed, zero failed/skipped. Server/client Release and
  isolated desktop Debug builds pass. No browser assets or native UI changed;
  the previous ten-browser-suite evidence remains available. The first Windows
  PowerShell wrapper aborted on a LocalWritingPanel account-case failure; a complete
  PowerShell 7 rerun passed all 849 without changing unrelated production code.
  Two existing test-build warnings remain. Evidence:
  `artifacts/brainstorming-p12-conversation-sql-verified/`.
- Explicit backend Debug staging and normal Debug builds pass. The staged executable
  passed SQL health and nonexistent-record Read/Inspect checks before replacement.
- Localhost backend PID 2916 was replaced by verified fixed PID 13616 at
  `http://localhost:5387`, running
  `artifacts/brainstorming-conversation-sql/backend/Debug/net10.0/BlazorApp.exe`.
  SQL health is 200. Its DLL SHA-256 is
  `A1263F0E2F62201BAD4C928C0A912FD69CE7A2E584F903C0D10645B56824947B`.
  The temporary staging host was stopped. No desktop process was stopped by the
  agent; the original desktop PID was no longer running at the post-check.
- The retained cache file SHA-256 matches its pre-fix value. No author operation,
  composer, manuscript or original recovery file was edited by verification.
  No live AI requests or additional database migrations were issued.
  See `conversation-live-inspection.log`, `backend-replacement-verified.json` and
  `conversation-checkout-preservation.json` in `artifacts/brainstorming-save-read/`.

## Recovery and handoff

Open the same document and saved conversation. Choose **Reopen / inspect saved
conversation**. The authenticated inspection should confirm the rejected operation
was never accepted and clear only its pending-send blocker, retaining the composer
and an inspection record. Choose **Prepare context for sending**, complete the
destination-specific Save, sync & prepare confirmation if offered, review current
context, then explicitly choose **Send message**. Inspect any genuinely uncertain
accepted operation instead of clearing it or resending it automatically.

Continue in this checkout with all unrelated work intact. Preserve the approved
six-migration database correction and ten unrelated pending migrations, canonical
native/backend identity and account-generation guards, original-copy recovery,
explicit source review and immutable operation identities. The normal backend
output is rebuilt for future launches; do not run a second listener on 5387 while
the fixed backend is active. Real native/provider generation, live speech, physical
device exchange and broader release acceptance remain separate gates. Read-only
SQL metadata and endpoint health do not establish an actual provider answer.
