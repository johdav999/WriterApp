# Consistency check redesign — 2026-10-04

The user reported that Consistency Coach showed facts, load/save controls and missing-link messages instead of inconsistencies. This implementation makes checking and reviewing contradictions the primary flow in the desktop/shared device panel and web editor.

## Result

Check consistency saves the relevant writing and prepares missing or stale character, place and timeline references automatically. Current references are reused. Recoverable reference availability failures and unresolved links do not block a check; coverage limitations remain visible. Account changes, stale source versions and invalid source identities still stop the operation.

The check compares the target text with original passages from the same manuscript. Each new finding must include both exact quoted passages and their source section IDs. The server verifies both against the supplied, owned writing before accepting the report. Missing links, ordinary new facts, open questions and unexplained motivation are explicitly excluded from contradiction findings. Reference extraction and refresh instructions preserve distinct conflicting claims instead of reconciling them into one fact.

The result shows both passages, a readable source location and a way to inspect the comparison passage. Device navigation uses the existing passage callback; the web editor opens a source preview without losing the report and provides a link to the source scene. Existing reviewed Apply, Discard, rich formatting and history/undo behavior remain connected. Mark intentional suppresses the proposed fix, persists across another check and can be reversed with Check this again. Decisions are local to the device/browser and scoped by backend, account, document and exact evidence; changes to either quoted passage produce a new decision key. They are not synchronized between devices.

Story reference and its manual maintenance controls are secondary, collapsed content. Limited comparison coverage is shown for manuscripts exceeding the 200,000-character serialized source budget. Whole source sections are included; omitted sections are counted. The target is never silently omitted. The existing web target remains the saved active page; the device target includes the selected scene's pages.

The user explicitly approved sending other scenes' manuscript passages to the configured OpenAI provider for consistency checks. That request path forwards the original passages and the two-passage output contract. Reference extraction/refresh use fixed evidence-preservation instructions; no additional evidence-policy payload was forwarded through those request paths.

## Acceptance ledger

| Issue | Acceptance | Evidence |
|---|---|---|
| CC-04 — P1: setup controls obscure checking | One primary check; automatically prepare references; unavailable references and missing links do not require a technical checkbox | Service and actual device/web handler tests verify initial preparation, reuse, fallback and stale/identity rejection. Browser initial view has collapsed references and no blocker checkbox. |
| CC-05 — P1: fact summaries erase or masquerade as contradictions | Preserve original opposing claims; findings show both quotes and trusted locations; reject invented evidence | Action-to-OpenAI serialized request test verifies both original claims and the new contract. Server source-validation tests reject forged quotes/IDs and unsupported findings. Browser shows the two passages and correct source navigation. |
| CC-06 — P2: repeated intentional differences and hard-to-review fixes | Mark intentional survives reload/new check, reverses, isolates accounts; changes resurface; Apply/Discard remain usable | Storage, presentation, device/web handler and browser tests verify decisions, preview persistence and source preservation. Browser review passes at 390px and 710px without horizontal overflow; Apply preserves bold formatting. |

## Verification and evidence

106 focused tests pass across consistency contracts, presentation, local/web handlers, automatic reference preparation, reference version guards and continuity coaching. The Windows desktop Release build succeeds with zero warnings/errors. The focused test compilation retains an unrelated pre-existing xUnit2031 warning in LocalStoryboardTests.

Five headless Edge acceptance groups pass with zero browser page errors, using real production device/shared Razor components and the shipped editor module in an isolated host. The host uses synthetic identity, AI and sync transports and separate storage. Its reference snapshots are already current; automatic remote extraction is verified by service/handler tests rather than this browser fixture. Source navigation in the fixture verifies the typed passage callback, not the full native workspace navigation.

Evidence is under `artifacts/consistency-redesign/`: `results/consistency.trx`, `browser-results.json`, `initial.png`, `finding.png`, `intentional.png`, `review-390.png`, `review-710.png`, plus the retained `Host/` fixture and `verify.mjs` driver. The driver verifies reload persistence, account scope isolation, Mark intentional/Check this again, review, Discard and explicit Apply. Original writing is `<p>Elin carried a <strong>heavy</strong> suitcase. The train arrived.</p>`; only Apply changes `heavy` to `light`, preserving the surrounding HTML.

Run tests:

```powershell
dotnet test Tests/WriterApp.Tests/WriterApp.Tests.csproj --no-restore -p:BaseOutputPath=C:/Users/Johan/source/repos/WriterApp/artifacts/consistency-redesign/build/ --filter "FullyQualifiedName~Consistency|FullyQualifiedName~DeviceBible|FullyQualifiedName~ContinuityCoach" --logger "trx;LogFileName=consistency.trx" --results-directory artifacts/consistency-redesign/results
```

Build desktop:

```powershell
dotnet build WriterApp.Desktop/WriterApp.Desktop.csproj -c Release --no-restore -p:BaseOutputPath=C:/Users/Johan/source/repos/WriterApp/artifacts/consistency-redesign/desktop-build/
```

For browser reproduction, use fresh fixture storage (the driver applies a change), build the retained Host in Release, run it on unused loopback port 5420 with `ASPNETCORE_ENVIRONMENT=Development`, then run `node artifacts/consistency-redesign/verify.mjs`. The task-owned fixture host was stopped after verification.

## Remaining acceptance boundaries

The user's existing desktop and server processes were preserved. The updated desktop executable is `artifacts/consistency-redesign/desktop-build/Release/net10.0-windows10.0.19041.0/win-x64/WriterApp.Desktop.exe`; the already-running Debug app does not contain these changes. Native UI acceptance and live OpenAI contradiction quality have not been verified. No deployed backend was updated. Automated tests and the synthetic browser fixture establish the implemented flow and source safeguards, not detection quality on a real manuscript.
