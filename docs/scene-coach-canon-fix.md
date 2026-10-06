# Scene-card coach canon preparation fix

Date: 2026-10-05

The desktop **Update all bibles** flow can report `Canon returned invalid structured data` before scene coaching runs. `DeviceBibleApi` translates a backend HTTP 422 into that message; `DeviceBiblePanel.Run` appends the notice that earlier completed bible updates remain saved.

## Reproduced cause

Both the timeline extraction prompt and `BibleRefreshService`'s deterministic timeline fallback emitted `locationId: ""` when no place was known. `CanonContent.Parse` requires a nonempty reference or null. Thus even the server's fallback could pass patch application and then fail the endpoint's canon validation. Two endpoint regression tests reproduced HTTP 422 before the fix. The original live response was not available, so this establishes a concrete matching failure path rather than the exact provider payload from the reported session.

The patch applier previously checked collection shape without validating the complete canon contract. Its repair path handled only malformed character JSON, excluding schema failures and place/timeline responses.

## Changes

- Timeline prompts and fallback use null for an unknown location.
- Patch results are normalized and checked using the shared canon parser before refresh preparation succeeds. Empty legacy timeline locations become null; names, IDs, evidence and participant validation remain required.
- Saved legacy timelines can be read, and unchanged refreshes reuse them without another provider call. The stored snapshot token and concurrency checks remain authoritative.
- Each bible type can make one repair attempt for malformed JSON or invalid canon structure. Place/timeline action builders and provider prompts now carry the original output and validation reason into that attempt.
- Timeline fallback retains its existing role after repair fails, and now passes the canon contract. Failed character/place repair does not save a new snapshot or advance its cursor.

## Verification

Final run: **160 passed, 0 failed**, including canon endpoints/cache, patch application, service repair, provider request construction, scene coaching, and continuity coach tests. The test invocation built the server, web client, device shared and UI shared projects successfully. One existing `LocalStoryboardTests` xUnit analyzer warning remains.

```powershell
dotnet test Tests/WriterApp.Tests/WriterApp.Tests.csproj --no-restore --filter 'FullyQualifiedName~Bible|FullyQualifiedName~Canon|FullyQualifiedName~LocalSceneCoaching|FullyQualifiedName~SceneCoachingEndpoint|FullyQualifiedName~ContinuityCoach' -p:BaseOutputPath=C:/Users/Johan/source/repos/WriterApp/artifacts/canon-fix/bin/ --verbosity minimal
```

No live provider request, production deployment, backend restart, or native desktop acceptance was performed. Apply this backend source update to the environment the app uses, then load saved canon and retry Update all bibles / scene coaching. Changes remain uncommitted in the existing checkout with the pre-existing work preserved. No database migration is required for this correction.

## Follow-up: local backend restarted

The subsequent server startup log reported a bind failure on `localhost:5387`. The listener was an older test host, PID 45080, running `artifacts/style-request-fix/tests/Debug/net10.0/BlazorApp.exe`. That exact process was stopped and replaced with the tested `artifacts/canon-fix/bin/Debug/net10.0/BlazorApp.exe`, PID 46092, on the same port and repository content root. The existing `Development` environment and `WriterApp:Database:AutoMigrateOnStartup=false` setting were preserved.

The replacement started successfully. `/health` returned HTTP 200 with `OK`; `/healthz` returned HTTP 200 with SQL Server status `ok`. Both IPv4 and IPv6 port 5387 listeners belong to PID 46092. Logs are under `artifacts/canon-fix/server-stdout.log` and `server-stderr.log`. This applies the canon fix to the local backend; a live canon/provider retry, native desktop acceptance and production deployment remain unverified.

## Follow-up: port released for the user's terminal launch

The user subsequently launched the server from their own PowerShell terminal and encountered the same port conflict because the agent-started PID 46092 was still listening. That verified instance was stopped; a fresh TCP listener check confirmed zero listeners on port 5387. No replacement background host was started. The user can now run `dotnet run --launch-profile http` from the repository to build and run the updated source in their terminal. The earlier health checks above describe the prior instance, which is no longer running.
