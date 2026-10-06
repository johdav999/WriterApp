# Custom prompt and practice-demo diagnosis — 2026-10-05

The original screenshot displays the generic `DeviceAiApi.CheckAsync` failure message. That message alone does not identify a provider outage: it also covers rate limiting, backend misconfiguration and HTTP 502 section-result validation failures. The subsequent diagnostic retry captured `System.IO.InvalidDataException: Incomplete or reordered translation page.` from the writing-result mapper. The remaining custom-preview failure is a rejected page/run mapping, rather than evidence of a provider outage.

The user retried and reported no proposal, with `Practice demo unavailable` replacing the earlier message. A temporary EventPipe listener on the running localhost backend (port 5387, PID 9108 at inspection) captured repeated `Microsoft.Data.SqlClient.SqlException: Invalid object name 'OnboardingDemoWorkspaces'.` A read-only endpoint probe returned HTTP 503 and `onboarding.storage_unavailable`. Read-only SQL inspection confirmed the configured shared Azure database's latest recorded migration is `20261002061110_MultiDocumentProjectsSqlServer`; the newer demo and AI-history tables are absent. The practice-demo error is an optional availability warning, separate from a custom-generation result. Do not claim that repairing it proves custom generation works.

## Changes and validation

- Section-result validation failures now return the stable code `ai.invalid_section_revision`; desktop/device clients explain that the returned section could not be validated and suggest a shorter selection.
- Known rate-limit and backend-configuration errors now receive specific allowlisted messages. Arbitrary backend detail and manuscript text are never displayed.
- 125 focused tests passed, covering error classification, invalid mapped results, custom prompts, writing panel behavior and onboarding migration contracts. Evidence: `artifacts/custom-prompt-service-fix/results/custom-prompt-service.trx`.
- Isolated desktop build passed with zero warnings/errors: `artifacts/custom-prompt-service-fix/desktop-build/Debug/net10.0-windows10.0.19041.0/win-x64/WriterApp.Desktop.exe`.
- Changes remain uncommitted alongside existing checkout work. The desktop was subsequently rebuilt/reopened for the input binding repair. The backend was subsequently restarted into the validated provider fix, as detailed below. Native helper initialization failed; no automated native UI verification is claimed.

## Approved database repair applied

`artifacts/custom-prompt-service-fix/owned-demo-schema.sql` is the generated idempotent SQL for exactly `20261004064042_OwnedOnboardingDemoSqlServer`. It creates `OnboardingDemoWorkspaces`, `OnboardingProgressOperations`, their primary/unique indexes, and the migration-history record. It contains no manuscript updates or deletions. It does not apply the other pending cover/preset/history migrations or create a practice manuscript.

Automatic approval review initially rejected the shared database mutation. The user then explicitly approved the migration. The exact demo-only script was applied successfully on 2026-10-05; its migration record is present. All 14 manuscript page IDs, document/section ownership and SHA-256 content hashes matched before and after. Evidence: `migration-result.json`, `writing-before.json` and `writing-after.json` under `artifacts/custom-prompt-service-fix/`.

The running local backend now returns HTTP 200 for `/api/onboarding/demo/status`. The development account reports `not-created`, with no workspace, which is a valid status rather than a storage error. No demo manuscript was created or reseeded. `demo-status-after.json` records the response. Other pending migrations were not applied.

## Confirmed custom-input binding defect

After the database repair, the user reported `Enter a custom instruction of at most 2,000 characters.` The real custom textarea used the default `onchange` binding. Its Preview button prevents the mousedown default to retain the manuscript selection. Consequently the textarea does not blur, its change callback is not dispatched, and the action can read an empty or previous instruction while newly typed text remains visible. This is separate from the database warning and the provider mapping failure.

The textarea now binds on `oninput`. The regression inspects the actual generated textarea callback, delivers typed text without blur/change, invokes the real workspace custom handler, and asserts the same instruction reaches section-replacement review. Before the fix it failed with expected `oninput`, actual `onchange` (`results/input-before.trx`). After the fix all 125 focused tests passed (`results/input-after.trx`). The normal desktop build passed with zero warnings/errors. The desktop was gracefully closed through its normal draft-save handler and reopened into that rebuilt launch folder. No forced termination was used.

During the preceding diagnostic retry, the separate active chat `Fix style check response` had gracefully closed the same desktop for its own rebuild. This chat reopened it; the diagnostic listener did not send a close command. Do not infer a diagnostic crash from that coincident exit.

## Confirmed provider mapping failure and production repair

After the input binding fix, the user again reported the generic service-unavailable error. A backend EventPipe listener captured `Incomplete or reordered translation page.` on the corresponding retry. `WritingActions.Result` reuses the complete translation mapper; this exception means the output's page ID/order or run count differs from the captured source. Its original HTTP 502 response lacked a stable code, which the device presented as a service outage.

Structured section/custom requests previously sent ordinary Responses API text generation with prose instructions to reproduce the full document/page/run JSON. They had no schema constraint. They also inherited the general 800-token output default, which does not scale to complete sections. Validation correctly rejected the incompatible proposal before applying any writing.

The OpenAI adapter now requests a strict source-specific JSON schema with one required text field for every existing run. No additional keys are allowed. The application restores the captured document, section, page and run IDs/order; empty pages are retained. Schema string patterns preserve whitespace boundaries and exclude line-break/control markers. The complete existing Unicode, boundary and prose validators still run after mapping. Structured streaming delivers a complete validated editor result instead of partial transport JSON. Selection-only output and its configured budget remain unchanged.

Structured output capacity now accounts for source text and run-key overhead, bounded at 32,768 tokens unless configuration explicitly requests more. There are no automatic billable retries. Incomplete/refused/invalid structured responses return `ai.invalid_section_revision`, including failures detected inside the provider, rather than the generic service message.

A synthetic real-provider check also exposed a nested JSON Unicode-escape failure: a source `Å` escape was returned as a NUL followed by `C5`. The provider-facing source now contains direct Unicode characters. It remains JSON data inside an API text message and is never rendered as HTML. Schema patterns prohibit NUL at the beginning/end of core text as well as internally; the existing validator independently rejects it.

Implementation follows [OpenAI Structured Outputs documentation](https://developers.openai.com/api/docs/guides/structured-outputs). The unchanged default [GPT-4.1 Mini model](https://developers.openai.com/api/docs/models/gpt-4.1-mini) supports strict structured output and a 32,768-token maximum output.

### Evidence and running state

- 150 focused tests passed (`results/provider-contract.trx`): actual custom-input binding, action request construction, exact mapped page/run preservation, Unicode, whitespace-only runs, empty pages, reordered wire keys, missing/extra/duplicate keys, invalid prose/control characters, incomplete responses, structured streaming, endpoint error codes and existing ownership/version gates.
- The real OpenAI adapter returned a valid changed revision from a synthetic forest instruction, including 3 pages, 4 runs and `Åsa`. `live-provider-result.json` records validation metadata and token usage. This test read/applied no manuscript content and did not exercise the desktop UI.
- The local backend is running the tested build at `artifacts/custom-prompt-service-fix/tests/Debug/net10.0/BlazorApp.exe`. `backend-fixed.pid` records its PID. Its writing availability and demo status endpoints both returned HTTP 200. Startup migrations were explicitly disabled; no additional database migration was applied.
- Desktop PID 50056 remained open throughout the provider repair/backend restart. A user retry of native Preview/review is pending; native Apply/undo is not claimed by the adapter test.
