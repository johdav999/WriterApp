# Azure SQL startup availability — 2026-10-11

The reported `dotnet run --project BlazorApp.csproj --launch-profile http` log
contains SQL error 40613 in the startup `SELECT 1` warmup. This is a transient
database availability error, not a compilation failure. The pasted excerpt
shows an EF retry, not a final unhandled exception.

Read-only Azure inspection confirmed that Prosa has a 30-minute auto-pause
delay and was Online during verification. A separate live `SELECT 1` succeeded.
Auto-resume is consistent with the reported failure; the original paused state
was not captured. Microsoft documents error 40613 on the first connection that
resumes a paused serverless database:
https://learn.microsoft.com/en-us/azure/azure-sql/database/serverless-tier-auto-pause-resume?view=azuresql

The old warmup reused the normal request retry policy (five retries, at most
ten seconds between retries), then continued into more database startup work
even after a failed warmup. A workspace server process was still running before
binding port 5387 and held the default build assemblies open.

`SqlServerStartupProbe` now uses a fresh read-only SQL connection on each
attempt, the SQL Server EF provider's maintained transient-error classifier,
and a three-minute cancellation deadline. Retry delays are 5, 10, 20, then
30 seconds. Expected availability failures have concise startup warnings.
Login and schema errors are not retried. Shutdown cancellation propagates.
Normal request retry behavior is unchanged.

If readiness fails, optional migration diagnostics, expired-trash cleanup,
and scene-content backfill are skipped. If startup migrations were explicitly
enabled, startup fails with an actionable database-availability message instead
of skipping required migrations. SQLite startup is unchanged. No cloud settings,
credentials, or migration configuration were changed.

Validation:

- 12 focused startup tests passed: recovery/backoff, immediate success,
  non-transient failures, deadline during connection and delay, shutdown during
  connection and delay, and provider classification of actual SQL errors
  40613/40197/40501 versus 18456/208.
- The combined startup and migration-safety run passed all 15 tests.
- The default Debug server build completed with zero warnings and errors.
- The verified old workspace server process was stopped and the rebuilt server
  was launched with the existing `http` profile. It remains running on port 5387.
- `/health`, `/healthz`, and `/__ping` returned HTTP 200; `/healthz` reported
  `Microsoft.EntityFrameworkCore.SqlServer` and `status: ok`.

Evidence is in `artifacts/sql-startup-fix/`: test TRX reports, server logs,
`host.json`, and `http-checks.json`. No intentional cloud pause or failover was
performed; resume retry behavior was verified through automated fault injection.
