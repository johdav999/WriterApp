# Brainstorm prose intent correction

Finalized: 2026-10-09. Preserve all uncommitted work in the same WriterApp checkout.

Latest runtime state: the isolated agent backend PID 34268 was stopped after the
author's normal launch hit an address-in-use error. Port 5387 is verified free;
no agent backend was restarted. The author can use the normal HTTP launch profile.
`author-launch-port-release.json` records the exact owned-process release. The
active-runtime descriptions below are historical verification evidence.

Runtime follow-up on the same date: the agent-launched normal-output backend PID
30980 locked DLLs during the next author build. The verified owned process was
stopped, and normal Debug rebuilt with zero warnings/errors. The backend now runs
from `artifacts/brainstorming-prose-intent/runtime-build/Debug/net10.0`, PID 34268,
at localhost:5387 with health 200. A second normal Debug build passed while that
isolated runtime was running. All 18 original cached conversations remain unchanged.
See `build-lock-release.json`, `build-while-runtime-active.log`,
`isolated-runtime.json` and `build-lock-verification.json` in the evidence folder.
PID 30980 below records the original finalization and is no longer active.

## Root cause and resulting behavior

The reported instruction was: “Add a scene where scientist anna is with her team
in the arctics and drilling ice cores for environmental research”. Read-only
metadata from the saved conversation confirms that this request completed with
intent Explore and no draft. The model returned discussion, so the existing draft
editor correctly had no prose to display. A later draft exists in that conversation
and is preserved; do not replay the original completed request automatically.

The previous model instruction distinguished exploration from requested prose but
did not clearly explain that describing a scene to add is itself a prose request.
`AI/Actions/BrainstormingConversationAction.cs` now instructs the model to infer the
author's intent from the whole instruction and conversation, fulfill actionable
scene requests with actual manuscript prose, and keep discussion, hypothetical or
negated requests and planning records distinct. Reviewed revision references and
checked destinations retain their existing guards.

There is no keyword/regex classifier, forced intent field or extra classification
request. The author message is unchanged and the existing structured response
contract is unchanged. This correction changes only the backend model instruction;
it adds no migration, editor asset or desktop UI change.

Generated prose appears in the editable middle proposal editor with its checked
destination. Keep remains the separate action that commits the reviewed text to
the manuscript. The model must describe a draft as ready for review and must not
claim it has already saved or applied it.

## Verification

- 852 combined regressions passed, with zero failures or skipped tests.
- All ten browser suites passed, covering context, conversation, workspace, drafts,
  Keep, planning, recovery, dictation, voice and the base editor projections.
- Server/client Release and isolated desktop Debug builds passed without warnings
  or errors. The test build retains two existing BL0006/xUnit2031 warnings.
- The real OpenAI adapter was exercised with a synthetic HTTP response: the exact
  production instruction and unchanged author request travel together, with one
  provider call and `store=false`. No forced intent is injected into input data.
- Both mounted Web and Device proposal components receive the generated prose,
  checked destination and separate Keep action. Generation leaves original writing
  and history intact. These fixtures use a synthetic provider.
- The normal backend Debug build also passed with zero warnings/errors. Its exact
  rebuilt executable is running at `http://localhost:5387`, PID 30980 at finalization.
  Health returned 200/OK, its listener belongs to that process and the loaded launch
  DLL hash matches the rebuilt output. Process IDs are observations, not permanent
  launch instructions.

Evidence is in `artifacts/brainstorming-p12-prose-intent` and
`artifacts/brainstorming-prose-intent/active-backend-verified.json`.
`reported-request-metadata.json` records only the matched request's intent and draft
counts. `checkout-preservation.json` records original status/cache preservation.

The revised instruction has **not** been tested in a new live model request.
Synthetic transport/component checks establish routing and rendering, not actual
model interpretation. No new native interactive acceptance was performed for this
backend-only change. The broader P12 native, provider, account, audio and physical
exchange gates remain separate; this slice does not make the feature release-ready.

A review-only fictional request is prepared in `fictional-request.json` and
`reviewed-provider-payload.json`. `provider-review.json` records the configured
model (`gpt-4.1-mini`), destination (`https://api.openai.com/v1/responses`), exact
payload hashes and zero requests sent. It contains the quoted instruction with
fictional context, not the author's manuscript. Preparing this packet is not live
provider acceptance or permission to transmit it.

## Preservation and handoff

Original conversation files, composer buffers, pending operations and the later
authored draft are retained. The canonical native identity/recovery correction,
approved six-migration schema update and SQL execution-strategy/idempotency fixes
remain intact. No migration or diagnostic provider call was added in this slice;
no user process was stopped. Unrelated checkout work remains uncommitted and intact.

For hands-on use, reopen the retained conversation and inspect any pending operation
before retrying it. Review the existing later draft first. A new explicit prose
request should create an editable proposal without requiring special mode words;
Keep applies its exact reviewed version. Do not automatically resend completed
requests, delete caches, replay uncertain operations or overwrite original writing.

```text
Continue in C:\Users\Johan\source\repos\WriterApp with all uncommitted P1–12 and unrelated work intact. Read docs/brainstorming-prose-intent-uat.md and the current implementation-status handoff. Preserve semantic model intent inference: actionable prose requests produce draft.text, discussion and planning remain distinct, and there is no keyword classifier or extra intent API request. Preserve the unchanged author input, checked destination, editable middle proposal and separate exact-version Keep. The reported original request was Explore with no draft; a later draft is already saved and must not be duplicated or removed. Latest automated evidence is 852 passing regressions, ten browser suites and both host builds; the rebuilt normal backend returned health 200 at localhost:5387. Synthetic provider/component tests do not prove live model interpretation. A review-only fictional payload/model/destination/hash packet exists in artifacts/brainstorming-prose-intent/provider-review.json; obtain explicit transmission authorization before a diagnostic provider send. Record actual author-driven prose generation and native proposal/Keep behavior without automatically replaying the old request. Preserve canonical identity/recovery, the completed six Brainstorm migrations and the SQL retry/idempotency boundaries; never retry provider dispatch inside database transactions or clear uncertain operations. Update exact evidence and remaining gates without marking the broader feature release-ready.
```
