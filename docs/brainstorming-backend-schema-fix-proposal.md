# Brainstorm conversation save/read failure — verified backend schema gap

Date: 2026-10-08. The user explicitly approved the migration. The exact reviewed
six-migration script has been applied to the configured Azure SQL database and
verified. The original failing Read/Inspect probes now return 404 instead of 500.

The author's subsequent generation failure exposed a separate transaction defect,
now corrected and loaded in the localhost backend. See
[conversation SQL correction and recovery](brainstorming-conversation-sql-uat.md).
Inspect the retained rejected operation before preparing and sending again; the
schema probes alone did not verify generation.

## Runtime evidence

The user's next Send now reaches conversation synchronization, after the approved
native identity correction. The retained cache has a composer, context/target and
one initial session-save operation, with no pending AI generation. Its payload
validates against the shared assembly used by the running backend. No manuscript
or composer text was logged or sent by the diagnostic probes.

The desktop's ordinary Development backend at `http://localhost:5387/` is running
PID 2916 from `artifacts/bin/Debug/net10.0/BlazorApp.exe`. It responds to health with
SQL Server provider and recognizes the conversation routes. However, both Read and
Inspect for nonexistent conversation/operation IDs return HTTP 500 / `server.error`.
These read-only probes neither create a conversation nor dispatch an AI request.
The generic client error hid the server-side failure as sign-in/reconnect guidance.

A read-only query before the update confirmed zero Brainstorm tables,
zero Brainstorm columns and zero applied Brainstorm migrations. Database startup
migration is disabled by default in `Program.cs`; compiling the backend does not
install the schema. There are sixteen pending migrations: six Brainstorm migrations
and ten unrelated migrations. This missing schema caused the reported failure.

Evidence: `artifacts/brainstorming-save-read/schema-probe.log` and
`artifacts/brainstorming-save-read/sql-script-validation.json`.

## Concrete reviewed update

The generated idempotent script is
[brainstorming-sql-update.sql](../artifacts/brainstorming-save-read/brainstorming-sql-update.sql).
SHA-256: `62E7FBE4357A6137CD189771C364FAD329D54C96C94E8542C4C8152DDB24B94A`.
Target: the configured Azure SQL database `Prosa` on
`prosa-sql-server.database.windows.net`; recheck the metadata report and target
before applying, and stop if either differs.

It applies only:

1. `20261007060409_BrainstormingSessionsSqlServer`
2. `20261007080044_BrainstormingGenerationsSqlServer`
3. `20261007102900_BrainstormingKeepsSqlServer`
4. `20261007150543_BrainstormingPlanningSavesSqlServer`
5. `20261007154719_BrainstormingRecoveryHistorySqlServer`
6. `20261007164539_BrainstormingTranscriptionsSqlServer`

The script creates six Brainstorm tables, their indexes/internal foreign keys, the
nullable `BrainstormingSessions.RecoveryJson` column and corresponding migration
history entries. It does not delete/update manuscript rows or apply unrelated
migrations. Each migration is transactional and guarded by its history entry.
An interrupted update can be inspected and resumed with this same reviewed script.
The earlier migration ID used as the script range boundary is a generation marker;
it is not being installed by this script and is still pending on the live database.
Do not run an unrestricted `dotnet ef database update`: that would include the ten
unrelated pending migrations.

## Verification and product correction

The exact script was executed twice against a newly created, GUID-named local
SQL Express database after the pre-Brainstorm baseline. Six acceptance groups pass:
idempotent DDL, matched migration chain, session write/receipt inspection, concurrent
session CAS, concurrent recovery/restart, foreign-account rejection and unchanged
rich manuscript/history. The owned disposable database was removed; live Azure SQL
was read only during this pre-approval validation. Console reproduction:

```powershell
dotnet run --project Tests/Brainstorming.SqlServerAcceptance/Brainstorming.SqlServerAcceptance.csproj -p:BaseOutputPath=artifacts/brainstorming-save-read/sql-validation-build/ -- artifacts/brainstorming-save-read/sql-script-validation.json artifacts/brainstorming-save-read/brainstorming-sql-update.sql
```

The capabilities controller now reports Brainstorm unavailable when its migrations
are pending, with clear database-update guidance and retained-message wording.
HTTP 5xx conversation failures now identify the backend/storage failure and status
without directing the author to sign in again. New regressions cover unavailable →
ready across a database update and exact composer/outbox retention for 500/503.
Combined regressions: 849 passed, zero failed/skipped. All ten browser suites,
server/client Release, isolated/normal desktop Debug builds (zero warnings/errors)
and isolated Windows startup pass. Two existing warnings remain in the test build.
The normal desktop launch folder is rebuilt. All 200 initial Git status entries
remain, and the live backend process was preserved. Evidence is in
`artifacts/brainstorming-p12-backend-schema`; sanitized runtime/schema evidence is
`artifacts/brainstorming-save-read/runtime-evidence.json`. No actual user message or
operation was modified or dispatched by verification.

## Approved application and handoff

Automatic approval review initially rejected a live-database write mode because
database changes were not yet authorized. That rejected patch was not applied.
The user subsequently said "i approve the migration", explicitly authorizing the
exact script above. The operational runner now requires `--apply-reviewed`, the
exact server/database, script SHA-256 and previously reviewed pending-migration
inventory before executing that script. Preview remains read only.

Application completed at 2026-10-08 19:19 UTC. All six Brainstorm tables, six migration
history entries and `RecoveryJson` are present. The ten unrelated migrations remain
pending with their original inventory. All previously applied history entries remain;
non-Brainstorm table columns and Documents/Pages/Projects counts are unchanged.
The original localhost nonexistent-session Read/Inspect probes both return ordinary
404; SQL health is 200. Backend PID 2916 was preserved without restarting it.

Evidence: `artifacts/brainstorming-save-read/migration-before.json`,
`migration-application.json` (`verified: true`) and `migration-runtime-after.json`.
The original `runtime-evidence.json` remains the pre-update baseline. No author
manuscript, cached composer or pending operation was edited by this application;
zero AI requests were sent. All unrelated checkout work remains intact.

The schema failure is resolved. Return to the existing conversation and explicitly
retry Send; inspect an uncertain operation before retrying it. Actual signed-in
native Send/provider acceptance remains separate from the successful SQL metadata
and running-backend probes. Preserve the retained composer and exact operation;
never automatically replay or dispatch the author's message.

Continue in the same checkout with all uncommitted P1–12 and unrelated work intact.
Preserve canonical backend identity, native account-generation/revocation guards,
destination-specific save/sync confirmation, original recovery files and immutable
operations. Do not restore the old native/backend-ID equality assumption or clear
the unsent message as a workaround for missing server tables.
