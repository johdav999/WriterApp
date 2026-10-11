# Brainstorm dialogue quote controls — 2026-10-10

## Root cause

Read-only inspection of the desktop cache for the reported Arctic drilling passage
found ten occurrences of U+001C/U+001D in the retained provider `InspectionText`
(escaped in JSON as `\u001c`/`\u001d`). Decoding that provider JSON gives the same
ten controls in `draft.text`. The saved draft also contains those controls and has
no rich HTML snapshot. The author instruction and included story-source text have
no unexpected controls, and this response has no parent draft.

The provider therefore supplied the invalid punctuation before the editor mounted.
UTF-8 transport and JSON decoding preserved it. There is no evidence of an editor
encoding conversion or of the provider's internal reason for producing those codes.
They appear exactly in the positions of opening/closing dialogue quotes.

The application defect was permissive text validation: the response contract only
rejected NUL, accepting the other C0 controls into otherwise valid structured output.
The strict response schema checks JSON structure, not prose punctuation.

## Correction

The shared conversation response contract repairs U+001C/U+001D to U+201C/U+201D
after JSON decoding and before creating drafts, rich content, or fingerprints.
The same repair applies to explanatory chat, constraints, introduced facts,
contradictions and proposed planning text. Source identifiers, planning before-values
and the exact author rewrite-authority quote remain unchanged.

Other C0/C1 control characters fail validation; tabs, CR and LF remain permitted.
Streaming answer previews use the same quote repair and withhold unsupported
control characters. The provider instruction explicitly asks for ordinary Unicode
quotation marks and forbids control codes in prose.

The server retains the exact raw provider response in inspection. Existing saved
drafts and author files are not rewritten, and generation is not retried. A corrected
copy of the reported passage is available at
`artifacts/brainstorming-quote-controls/corrected-passage.txt` (ten quotes restored,
zero unexpected controls).

## Verification

- 84 tests passed, zero failures/skips: BrainstormingTextEncodingTests,
  BrainstormingProviderTests, BrainstormingConversationTests and BrainstormingDraftTests.
- Real provider HTTP adapter fixtures cover literal UTF-8 and escaped Unicode,
  fragmented JSON escape sequences, and unchanged raw transport.
- Shared service tests cover decoded quote repair before durable draft/turn creation,
  HTML/plain-text round trips, untouched raw inspection, preserved international
  characters, and rejection of NUL, SOH, VT, ESC, DEL and NEL.
- Existing append/rewrite, manual edit, proposal selection, provider-failure and
  persistence regressions passed. No manuscript was applied by these tests.
- The test build compiled server, web, shared UI and device projects. Existing
  NU1900 warnings reflect unavailable NuGet audit networking; existing test warnings
  BL0006 and xUnit2031 are unrelated.
- The isolated Windows desktop build passed with zero warnings/errors. Output:
  `WriterApp.Desktop/artifacts/brainstorming-quote-controls/native-build/Debug/net10.0-windows10.0.19041.0/win-x64/`.

Evidence: `artifacts/brainstorming-quote-controls/evidence/quote-controls.trx`;
`artifacts-brainstorming-quote-controls-test.log`.
Desktop build log: `artifacts-brainstorming-quote-controls-desktop-build.log`.

## Runtime handoff

The existing backend PID 48872 runs from `artifacts/bin/Debug/net10.0/BlazorApp.exe`.
It was not restarted or replaced. Rebuild and restart the normal backend to load
the response-boundary correction. The isolated validated backend output is
`artifacts/brainstorming-quote-controls/Debug/net10.0/`.

No new live model call or interactive native acceptance is claimed.
