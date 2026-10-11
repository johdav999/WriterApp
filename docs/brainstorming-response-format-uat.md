# Brainstorm rejected JSON appearing in chat — 2026-10-09

## Root cause

The screenshot shows a Failed operation and an Incomplete assistant turn. Its
provider JSON ends with `planning: []` and omits the required `contradictions`
array. The response validator rejects that shape. This is reproduced by the new
missing-contradictions fixture; the response is not adopted into a working draft.

Two defects combined:

1. The OpenAI conversation request relied on prose instructions for its JSON shape.
   It supplied no structured response schema, allowing required fields to disappear
   in an otherwise complete response.
2. On failure, the server used `InspectionText` as the saved assistant turn's text.
   The shared conversation component rendered that text as a normal chat bubble.
   The raw wire response consequently appeared twice: visibly in chat and inside
   the intended collapsed inspection panel.

## Correction

The provider now supplies `text.format` with a strict `json_schema`. All response
fields, nested draft fields (including continuation/revision instruction), and
planning fields are required. Optional values use nullable types; objects disallow
extra properties. This follows the official [Structured Outputs guide](https://developers.openai.com/api/docs/guides/structured-outputs).
Both streaming and buffered conversation routes use this same provider method.
Application validation of source ownership, semantics, bounds and complete output
still runs before any draft is saved. The previous append/rewrite correction is
preserved. There is no automatic retry or extra provider call.

Failed assistant turns now persist a readable status message. Raw output remains
in the generation inspection ledger. The shared desktop/web transcript also handles
legacy cached Incomplete turns: their raw text appears only in a collapsed inspection
section, without rewriting historical records. When the current generation already
provides that inspection panel, the old turn does not duplicate it. Response validation
failures now tell the author the response could not be validated and that retrying
is explicit, rather than incorrectly suggesting that refreshing story context is
the only remedy.

## Evidence and limits

- 428 relevant regressions passed, zero failures/skips (Brainstorming, OpenAiProvider,
  AiOrchestrator); the final focused failure run passed all seven cases after removing
  a test analyzer warning.
- The production provider HTTP fixture checks that strict mode reaches the outgoing
  request, all nested object fields are required, extra properties are prohibited,
  and contradictions/revision instruction are in the schema.
- The reported sequence is reproduced: two successful passages, then a complete
  JSON response missing contradictions. Existing drafts remain byte-for-byte equal,
  the raw response is retained for inspection, and the assistant turn contains status
  text. Mounted shared Razor tests cover both new failures and old cached raw-JSON
  failures, collapsed inspection, preserved earlier replies and retained composer.
- Existing malformed/partial/tool-output/disconnect/quota/provider-failure cases
  still retain inspection output and cannot become applicable prose.
- Release server/web and Debug desktop builds passed with zero warnings/errors.
  Existing unrelated test warnings remain BL0006 and LocalStoryboardTests xUnit2031.

Evidence lives in `artifacts/brainstorming-response-format/`: `regressions.trx`,
`failure-regressions.trx`, their logs, `server-build.log` and `desktop-build.log`.
Model replies are synthetic in these tests; no new live-provider or interactive
native acceptance is claimed.

## Runtime handoff

The inspected backend is still PID 34768, the same process observed before the
append-intent correction, running `artifacts/bin/Debug/net10.0/BlazorApp.exe`.
Desktop PID 46944 runs from the normal Debug folder. These author processes were
not stopped, and author conversation files were not changed.

The validated builds use isolated outputs:

- `artifacts/brainstorming-response-format/release-build/Release/net10.0/`
- `WriterApp.Desktop/artifacts/brainstorming-response-format/native-build/Debug/net10.0-windows10.0.19041.0/win-x64/`

Close the normal desktop and stop the backend through their usual controls, rebuild
both normal launch outputs, and restart them to load the corrections. An explicit
retry of the failed message is then available; do not automatically replay any old
operation or adopt its unvalidated draft.
