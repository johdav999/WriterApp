# Brainstorm story continuation intent — 2026-10-09

## Cause and correction

The reported successive story requests describe the patrol encounter and then
its departure. They belong in sequence. Previously the model instruction singled
out explicit continuation phrases, while `BrainstormingProse.Continuation` defaulted
to false. The server accepted false or an omitted flag as permission to replace
the entire reviewed candidate with the latest generated passage.

The production model instruction now treats each new prose request as an append
when a previous working draft exists. Another event, line of dialogue or section
in the same storyline does not require the author to say “continue.” The prompt
uses the reported patrol messages as examples and distinguishes explicit edits,
new versions, negation, hypothetical discussion and mixed edit/append requests.
Intent detection remains semantic, in the author's language, with one generation
request and no keyword classifier.

Replacement responses now include optional `draft.revisionInstruction`, quoting
the exact current-author clause asking to edit existing prose. The shared response
validator checks this against the current message, rejects empty/invented quotes
and contradictory continuation metadata, and requires evidence for selected-passage
edits. With a previous draft and no revision quote, prose defaults to continuation
even when the model returns false or omits the flag. The prompt must still identify
the meaning of a quoted clause correctly; substring validation alone does not
prove its semantics.

The existing composition path appends escaped paragraphs to the reviewed rich
snapshot. The editable working draft therefore contains both sections in order.
Version history records accumulated snapshots. Explicit rewrites return the full
updated draft, retaining all requested unchanged paragraphs. Keep remains the
reviewed manuscript application action.

The optional field is omitted when null. Existing send identities, cached sessions,
draft fingerprints and database schema do not change. Existing author conversations
and running author processes were not edited or stopped. This fixes new requests;
it does not retrospectively combine the author's older saved versions.

## Verification

- 488 relevant .NET regressions passed, zero failures or skips: Brainstorming,
  DeviceSyncEngine, OpenAiProvider and AiOrchestrator.
- The new regression mounts the shared conversation and both Web/Device proposal
  surfaces, sends successive patrol beats without continuation command words,
  reproduces explicit false and omitted model flags, and verifies paragraph order,
  preserved bold formatting, pinned parent context, immutable history and reopen.
  An explicit new-version instruction replaces the complete draft. Keep then commits
  the retained accumulated candidate containing both story sections.
- Four invalid-authority cases retain the original draft: invented quote, empty
  quote, selected edit without a quote and conflicting append/rewrite metadata.
- Existing typed, spoken, recovery and selected-passage rewrite fixtures now supply
  the current-author edit clause. The final focused draft run passed all 31 tests.
- Release server/web and Debug desktop builds passed with zero warnings/errors.
  The test compilation reports existing BL0006 and xUnit2031 warnings.

Evidence: `artifacts/brainstorming-append-intent/combined-regressions.trx`,
`draft-regressions.trx`, their logs, `server-build.log` and `desktop-build.log`.
The first broader run had two test-only record comparisons fail because collection
equality uses reference identity after reload; those assertions now compare the
serialized retained snapshot. The final combined receipt supersedes that run.

These checks use synthetic model responses. Live model interpretation and a new
interactive native session were not exercised. No browser assets changed.

## Runtime and handoff

Both backend and desktop must load the updated shared contract. During this work,
the author backend was PID 34768 at `artifacts/bin/Debug/net10.0/BlazorApp.exe`,
and the author desktop was PID 36052 from its normal Debug output. They remained
running with their earlier binaries. Process IDs are observations, not instructions.

Build outputs ready for inspection are:

- Server/web: `artifacts/brainstorming-append-intent/release-build/Release/net10.0/`.
- Desktop: `WriterApp.Desktop/artifacts/brainstorming-append-intent/native-build/Debug/net10.0-windows10.0.19041.0/win-x64/`.

For the normal author launch, close Prosa and stop the backend through their normal
controls, rebuild both normal outputs, and restart them. Restarting the old binaries
alone will not load this change. Preserve all uncommitted work and author caches;
do not automatically replay historical or uncertain generations.

Continue in this checkout. Preserve append-by-default composition, exact-current-
instruction rewrite evidence, rich formatting, branch/history identity and the
existing review/Keep workflow. Verify live model behavior through an author-driven
new story beat and an explicit rewrite before claiming native/provider acceptance.
