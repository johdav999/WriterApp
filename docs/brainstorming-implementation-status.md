# Prosa brainstorming implementation status

Updated: 2026-10-06. Plan: [brainstorming-prompts.md](brainstorming-prompts.md).

The implementation prompts are prepared. No brainstorming feature implementation or acceptance run was performed as part of preparing this plan. Existing AI/editor capabilities must be reverified in prompt 1.

## Prompt progress

| Prompt | Scope | Implementation | Verification |
|---|---|---|---|
| 1 | Baseline, contracts, capabilities | Not started | Not run |
| 2 | Durable sessions and mapping | Not started | Not run |
| 3 | Target resolution and context | Not started | Not run |
| 4 | Conversational AI and streaming | Not started | Not run |
| 5 | Shared desktop/web workspace | Not started | Not run |
| 6 | Drafts, revisions, version editing | Not started | Not run |
| 7 | Reviewed manuscript apply and undo | Not started | Not run |
| 8 | Reviewed planning changes | Not started | Not run |
| 9 | Recovery, sync, text acceptance | Not started | Not run |
| 10 | Real microphone dictation | Not started | Not run |
| 11 | Spoken dialogue and read-aloud | Not started | Not run |
| 12 | Integrated acceptance | Not started | Not run |

## Acceptance scenarios

Create isolated test data with Chapter 1 containing multiple sections/pages, Chapter 2 with existing writing, a synopsis, character/place notes, rich text, and an optional note that contradicts the synopsis. Give the records stable identities and record expected context, order, and content before each mutation. Use actual scene/section relationships from the repository. Include a standalone document and local/server mapped copies.

| ID | Scenario | Expected behavior |
|---|---|---|
| B01 | Request the first section of Chapter 2 based on Chapter 1 | Target resolves; preceding chapter is included in reading order or a disclosed summary path is chosen; synopsis and selected sources appear in the actual context manifest; prose appears as a proposal. |
| B02 | Ask for three opening ideas, then discuss one | Conversation advances; manuscript, planning, and current proposed prose remain unchanged until an explicit drafting/revision request. |
| B03 | Ask for a more suspicious protagonist and understated dialogue | A new version uses the preceding draft and selected context; both versions remain inspectable. |
| B04 | Edit a proposed paragraph, reopen, and request another revision | Manual proposal edits survive and are the source for the next revision. |
| B05 | Keep an opening before an existing Chapter 2 section | Exactly the reviewed content is inserted once; existing prose and structure remain ordered and intact. |
| B06 | Keep a replacement for an existing passage | Exact reviewed range is changed; supported rich nodes, formatting, annotations, and unrelated content remain intact. Unsupported application retains the source. |
| B07 | Undo/redo, then attempt undo after editing affected content | Safe undo/redo works; divergent writing is preserved with an actionable refusal/recovery path. |
| B08 | Save one idea as a scene, note, synopsis field, or character note | Each approved target persists with provenance; unrelated fields remain intact; scene ideas do not silently create manuscript prose. |
| B09 | Change selected scene or reorder/delete the captured target | Subsequent requests use the new selection; prior proposals retain their target and cannot silently apply elsewhere. |
| B10 | Duplicate chapter titles or ambiguous voice/text target | Prosa requests a focused clarification and preserves all writing. |
| B11 | Exclude a source, include a conflicting source, or exceed context limits | Excluded source content is absent; material conflict/omission is visible; mandatory chapter context is not silently truncated. |
| B12 | Local writing differs from server writing | The actual checked local context is used through an authorized contract or the unavailable path is explicit; old cloud text is not presented as current. |
| B13 | Restart/reload during send, streaming, draft editing, or application | Accepted turns and candidates survive; interrupted/unknown operations remain inspectable; no duplicate billing, turns, insertion, or false success occurs. |
| B14 | Offline review followed by reconnection | Cached conversation/proposals and local writing remain usable; generation/application is not automatically replayed. |
| B15 | Synchronize sessions between mapped desktop/web copies with concurrent edits | Identities map correctly; retries deduplicate; conflicting branches/edits are retained for deliberate resolution. |
| B16 | Account/backend change, revoked access, or unauthorized source ID | No private context leaks or late event adopts into the new session; writing stays intact. |
| B17 | Timeout, quota failure, malformed output, stream cancellation, late completion | Composer and prior candidates survive; incomplete output cannot be applied; usage and operation states are accurate. |
| B18 | English and Swedish live dictation | Real microphone input creates an editable final transcript in the existing composer; partial recognition does not submit a turn. |
| B19 | Real spoken dialogue and interruption during an answer | Transcript and audio follow the same session; interruption stops stale output; playback/echo never becomes a user command. |
| B20 | Switch text/voice, then request a draft revision | No session/context fork; revised prose remains an unapplied candidate. |
| B21 | Say Keep this version, Don't keep this version, or an uncertain/partial phrase | Only an unambiguous finalized approval for the visible reviewed candidate reaches the checked application path; ambiguity requests clarification. |
| B22 | Read aloud, edit the draft, disconnect audio, or navigate away | Playback follows an identified version and stops cleanly; resources release; transcript, session, and writing survive. |
| B23 | Long conversation, narrow window, keyboard-only operation | Panels remain accessible; bounded context/pagination work; composer, focus, and save behavior remain reliable. |

## Evidence requirements

For each completed prompt, add a dated entry with:

- Implemented production behavior and changed files.
- Contract/schema/migration changes and compatibility requirements.
- Exact commands, checks, results, data used, and defects fixed.
- Browser/server versions and desktop executable/output path actually exercised.
- Separate implementation, automated, browser, native, live-provider, microphone/playback, database/sync, and packaged statuses.
- Unavailable gates with concrete reproduction instructions and the next copyable prompt.

Record test fixtures as fixtures. A successful build or mocked conversation is not evidence of live provider, actual microphone, real playback, native interaction, or packaged acceptance.

## Current handoff

Continue in `C:\Users\Johan\source\repos\WriterApp` with the existing uncommitted work intact. Next prompt:

```text
Implement prompt 1 in docs/brainstorming-prompts.md, including its common requirements. Read docs/brainstorming-implementation-status.md and inspect the current working tree. Establish the current desktop/web/editor/AI baseline, implement the shared brainstorming contracts and capability boundary, run relevant checks, and update this status file with evidence and the prompt 2 handoff. Preserve unrelated work and complete the authorized implementation rather than return another plan.
```

## Dated implementation evidence

No implementation entries yet.
