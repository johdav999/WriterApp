# Brainstorm manuscript continuation rejected — 2026-10-10

## Root cause

Read-only inspection found the exact request and retained provider response from
the screenshot. The completed model response contains all required structured
fields, `intent: Draft`, nonempty prose and `draft.continuation: true`, while the
checked request has `parentDraftId: null`. Its usage matches the screenshot:
3,684 input and 506 output tokens.

The author's request continues the manuscript as a whole. The application's
`draft.continuation` flag instead controls composition with an existing editable
Brainstorm proposal. Those concepts were conflated. The response validator rejected
this first proposal with: "A continuation needs a complete reviewed working draft,
without a passage selection."

The new section is already represented correctly by the supplied prose. Appending
it to the manuscript is handled by the reviewed Keep destination, independently
of the proposal-composition flag. No existing proposal needs to be invented and
the earlier manuscript must not be copied into the new proposal.

## Correction

The shared response boundary now derives proposal composition from the checked
request: without a parent draft and without rewrite authority, a valid Draft
response starts a proposal even if the model labels it a continuation. With a
reviewed parent, new story beats continue to append by default. A Revise response
without a parent, invented rewrite authority, passage-selection errors, malformed
responses and other validation failures are still rejected.

The provider instruction also distinguishes manuscript continuation from proposal
composition. A null `previousDraft` requests only the new prose, with Draft intent
and an initial proposal flag, even when the manuscript contains earlier sections.

This correction is shared by desktop and web. It does not alter the Keep placement
rules, auto-apply prose, rewrite saved author data or retry the failed paid request.

## Verification

Read-only replay against the old backend assembly reproduces the exact rejection.
Replay of the same cached response against the corrected assembly succeeds and
preserves its prose exactly. Both runs have the same cache SHA-256:
`DF6779BFC4BDE702CF7F8C417E36B28D754BCDEE1691BDBE5D9974B44D7A32A9`.

New service regressions exercise streaming and buffered generation, persisted
reviewable prose, unchanged raw inspection, settled usage and idempotent replay.
They then explicitly review/Keep the proposal and verify that it follows the
existing manuscript text. Additional regressions reject parentless Revise and
parentless rewrite authority.

All 462 Brainstorming/OpenAI-provider/orchestrator regressions passed, with zero
failures or skips. The earlier focused run passed 88 tests. The test build compiled
the backend, web client, shared UI and device projects. The isolated Windows
desktop build passed with zero warnings or errors. Existing test analyzer warnings
BL0006 and xUnit2031 are unrelated to this correction.

Evidence is under `artifacts/brainstorming-initial-continuation/evidence/`:
`retained-old.log`, `retained-fixed.log`, `focused.trx` and `regressions.trx`.
No new live-provider request or interactive native acceptance is claimed.

## Runtime

The observed backend PID 48872 still runs the old normal Debug assembly from
`artifacts/bin/Debug/net10.0/`. The isolated corrected backend is built under
`artifacts/brainstorming-initial-continuation/Debug/net10.0/`.
Stop the backend through its usual controls, rebuild/relaunch its normal output,
then explicitly retry the failed message. Restarting the old binary without
rebuilding will not activate this correction.

The isolated desktop output is
`WriterApp.Desktop/artifacts/brainstorming-initial-continuation/native-build/Debug/net10.0-windows10.0.19041.0/win-x64/`.
No author application was stopped or author cache changed during this work.
