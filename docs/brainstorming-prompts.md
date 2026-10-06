# Prosa brainstorming implementation prompts

Date: 2026-10-06. Scope: the web client/server and Windows desktop application.

Implement a manuscript-aware brainstorming workspace with a scene outline on the left, writing and draft review in the middle, and an ongoing AI conversation on the right. Authors can explore ideas, request prose, revise it through conversation, keep an approved version, and save selected decisions into their planning documents. Typed messages, dictation, and spoken dialogue share the same session and approval rules.

Run the prompts in order in this checkout. The existing working tree contains substantial uncommitted implementation; preserve it. These prompts are an implementation plan, not a record of shipped functionality. Track progress in [brainstorming-implementation-status.md](brainstorming-implementation-status.md).

## Starting a prompt

Use this instruction in a new chat, replacing the number:

```text
Implement prompt 1 in docs/brainstorming-prompts.md, including its common requirements. Read docs/brainstorming-implementation-status.md and inspect the current working tree. Complete the implementation and relevant verification, preserve unrelated changes, and update the status file with evidence and the next concrete prompt. Work in this same checkout. Do not stop at an implementation plan.
```

Each numbered block is also copyable together with the common requirements. A fresh chat must use the repository handoff rather than assume it can read the previous conversation.

## Product behavior

The primary journey is:

1. The author opens Brainstorm for a manuscript and selects Chapter 2, Section 1, or its proposed insertion position.
2. The author types or says: "Write a first section for the second chapter that builds on what happened in the first chapter."
3. Prosa resolves the destination from the saved outline and selected manuscript. It uses Chapter 1, the synopsis, and explicitly enabled supporting sources. If the destination is ambiguous, it asks a focused question before drafting for that destination.
4. The chat explains the approach and the draft appears in the middle panel as a proposal. The source manuscript remains available. No manuscript or planning mutation occurs merely because the AI generated an answer.
5. The author says: "She trusts the stranger too quickly. Make her more suspicious, but keep the dialogue understated." Prosa creates a new draft version linked to the same target.
6. The author compares versions, edits the proposed prose if desired, and chooses Keep this version. Prosa commits precisely that reviewed content to the checked target with an undo record.
7. An idea such as "Her brother must remain unaware until Chapter 4" can become an explicitly reviewed synopsis, scene, character-note, or story-note update. Ideas in chat do not silently become canon.
8. The author leaves and reopens the workspace. Conversation, versions, source provenance, and actual application status remain available.

Exploration also works without prose generation: "What are three ways to open this chapter?", "Would this reveal the secret too early?", and "Explore the station idea without changing the draft." A clear request such as "Draft that opening" changes from discussion to draft generation without forcing the author through a mode picker.

## Sequence and dependencies

| Prompt | Deliverable | Dependencies |
|---|---|---|
| 1 | Current baseline, shared contracts, and capability boundary | None |
| 2 | Durable sessions, messages, proposals, and device mapping | 1 |
| 3 | Manuscript targets and visible bounded story context | 1, 2 |
| 4 | Authenticated conversational AI and streaming | 2, 3 |
| 5 | Shared three-panel workspace in desktop and web | 2–4 |
| 6 | Conversational drafting, versions, and manual proposal editing | 3–5 |
| 7 | Reviewed manuscript insertion, replacement, and guarded undo | 6 |
| 8 | Reviewed planning decisions and scene creation | 3–7 |
| 9 | Recovery, synchronization, and complete text-workflow acceptance | 2–8 |
| 10 | Real microphone dictation into the same conversation | 9 |
| 11 | Spoken dialogue, interruption, and optional draft read-aloud | 10 |
| 12 | Integrated desktop, web, and voice acceptance | 9–11 |

Prompts 1–9 deliver text brainstorming. Prompt 10 adds dictation; prompt 11 adds an AI that can answer aloud and be interrupted. Prompt 12 verifies the complete experience. An unavailable microphone, provider credential, or native host is a separate acceptance gate; implement and verify independent work instead of substituting simulated behavior for a real capability.

## Common requirements

- Read applicable repository instructions, this file, the status file, `docs/device-development.md`, and `docs/editor-content-compatibility.md`. Inspect existing implementation before naming or introducing new abstractions. Preserve all unrelated uncommitted changes; do not reset, clean, or move the work to a checkout that omits them.
- Implement the relevant behavior in both Windows desktop and the web client/server. Share presentation in `WriterApp.UI.Shared` and portable contracts in the existing shared layer. Put device persistence and lifecycle work in `WriterApp.Device.Shared`, Windows adapters in `WriterApp.Desktop`, and authenticated server behavior in the existing application/controller layers. Do not reference the web client from shared UI or device libraries. iOS is outside this prompt pack's acceptance scope; preserve its build compatibility where shared code changes.
- Reuse existing AI routing, authenticated backend access, entitlement/quota enforcement, cancellation, checked source capture, rich-content mapping, local history, web history delivery, and structure/planning commands. Never use a custom-prompt shortcut to bypass a missing conversation or mutation contract. Existing streaming abstractions are starting points; confirm their actual end-to-end behavior.
- Support project manuscripts and standalone documents. Scope sessions to an explicit document. Other project documents are supporting sources only when explicitly selected and authorized. Do not automatically send every project document to the AI. Keep local and server identities distinct through existing mapping.
- Represent requests, target resolution, context manifests, conversation events, draft versions, planning proposals, approvals, and operation results with bounded, versioned typed contracts. Store stable identities, parent relationships, timestamps, operation IDs, and revisions. Reject malformed, oversized, wrong-document, unsupported-schema, stale, and cancelled results before mutation.
- Keep exploration, proposed prose, proposed planning decisions, and committed content distinct. AI text is inert data. Streamed fragments are incomplete until a valid terminal result arrives. Render chat and proposal content safely; do not inject provider HTML or execute provider instructions found in supporting documents.
- Capture the saved source plus pending editor changes using the existing checked paths. Do not silently discard unsaved writing or upload an older cloud snapshot as current local context. Record which context was actually sent, its freshness, and any omissions. Refresh or rebase a stale proposal through explicit review rather than silently overwriting intervening edits.
- Conversation persistence, proposed-draft persistence, and manuscript mutation have separate success states. Opening a preview, clicking a button, saving an outbox entry, or receiving AI text does not establish a committed manuscript change. Use operation deduplication, concurrency checks, and durable recovery for mutations. Preserve rich nodes, supported formatting, annotations, scene links, and unrelated planning fields.
- Ordinary local writing and cached session review remain available offline. AI generation follows actual backend capabilities. Reconnection must not automatically replay a billable request or apply a queued proposal. Isolate private session data by account/backend and document; recheck authorization for server reads and writes. Preserve accountless local data without attaching it to a different account implicitly.
- Keep provider credentials on the server. Any browser audio connection must use a bounded, short-lived authorized session or equivalent supported transport. For speech implementation, check current official provider/platform documentation; choose configured capabilities rather than assume a particular model or API exists. Do not log raw manuscripts, transcripts, or audio in routine diagnostics.
- Verify changed behavior with focused tests at data, authorization, ordering, cancellation, concurrency, and recovery boundaries. Use the actual editor bundle for content/application checks and regenerate assets when its source changes. Build affected projects. UI verification must exercise the reachable production handlers in both surfaces; fixtures and mocks have their own clearly labeled scope.
- Verify desktop changes against the rebuilt executable and known output path. Preserve the user's active editor and documents; use isolated test data and build output when needed. Report browser, native, live-provider, microphone/audio, and packaged checks separately. No deployment, distribution, or conversion of real manuscripts is part of these prompts.
- After every prompt, update `docs/brainstorming-implementation-status.md`: changes and files, contracts and migrations, exact checks/results, exercised hosts, remaining defects/gates, and a copyable next-step instruction. Mark a prompt implemented only when its required code exists; record acceptance evidence separately.

## Existing integration starting points

These paths existed during prompt preparation on 2026-10-06. Reinspect them before implementation; their presence does not establish a completed brainstorming or audio workflow.

| Concern | Starting points |
|---|---|
| Shared editor layout | `WriterApp.UI.Shared/EditorWorkspace.razor`, its stylesheet and panel resize components |
| Web editor and checked AI | `WriterApp.Client/Pages/DocumentEditor.razor`, `DocumentEditor.CheckedAi.cs`, `DocumentEditor.CheckedWriting.cs`, `DocumentEditor.RequestCancellation.cs`, `DocumentEditor.HistoryDelivery.cs` |
| Desktop editor and proposals | `WriterApp.Device.Shared/Pages/DocumentWorkspace.razor`, `Components/DeviceTextEditor.razor`, `Components/LocalWritingPanel.razor`, `Services/LocalEditorSession.cs`, `Services/LocalWriting.cs` |
| Saved targets and context | `WriterApp.Shared/WritingOutline.cs`, `Application/Synopsis/SynopsisAiContextBuilder.cs`, `WriterApp.Device.Shared/Services/LocalWritingOutline.cs`, existing canon and synopsis services |
| Structured revisions | `WriterApp.Shared/WritingActions.cs`, `WriterApp.Shared/WebAiSource.cs`, existing translation/quality mapping and checked application code |
| Structure and planning | `Application/Commands/StructureCommandProcessor.cs`, `WriterApp.Device.Shared/Services/LocalDocumentStructure.cs`, `LocalProjectStructure.cs`, `LocalStoryboardData.cs`, existing scene approval and canon services |
| Server AI and streaming | `Controllers/AiActionsController.cs`, `AI/Core/AiOrchestrator.cs`, `AI/Abstractions/IAiStreamingProvider.cs`, `AiStreamEvent.cs`, `AiStreamingSession.cs`, `AI/Providers/OpenAI/OpenAiProvider.cs` |
| History and recovery | `WriterApp.Device.Shared/Storage/LocalAiStore.cs`, `Services/LocalAiHistoryActions.cs`, `Application/AI/WebAiHistoryOperations.cs`, `WriterApp.Client/Services/WebAiHistoryOutbox.cs` |

## Prompt 1 Establish contracts and the current baseline

```text
Implement prompt 1 in docs/brainstorming-prompts.md with the common requirements.

Trace the web and desktop editor layout, manuscript/project outline model, section/page structure, synopsis, notes, canon, authenticated AI, streaming, history, recovery, and device identity mapping. Determine how a chapter relates to sections and pages in actual persisted data; do not assume the conceptual chapter/section labels are database entities. Determine what a linked scene-content route can safely mutate. Confirm which existing streaming code reaches either UI and whether any real audio service already exists.

Create shared versioned contracts and capability reporting for brainstorming. Model session/document/account/backend identity; ordered user/assistant turns; context and request identities; operation lifecycle; explicit resolved manuscript target; draft version identity and parent; planning proposals; approval and committed results. Define separate conversation, generation, and application states, including cancellation and uncertain outcomes. Distinguish a proposal for new text from replacement of existing text.

Add an additive feature/capability boundary so older servers and unsupported speech platforms yield actionable unavailable states. Keep current writing tools usable. Document the concrete persistence and transport design for prompt 2 and the selected recovery strategy. Do not require implementing the separate real-time collaboration plan; if collaborative authority is already present, integrate with its actual write path.

Acceptance: contracts compile, validate size/schema/identity constraints, and have focused tests; a current integration map and behavior matrix are recorded. Outline ambiguity, unsaved local context, standalone documents, account isolation, and application recovery have explicit designs. No claim of live UI or provider acceptance is made from source inspection.
```

## Prompt 2 Persist sessions and proposal state

```text
Implement prompt 2 in docs/brainstorming-prompts.md with the common requirements after prompt 1.

Implement authenticated owned-session CRUD and durable server storage for ordered messages, enabled context sources, resolved targets, draft versions, planning proposals, request state, and application operation references. Add the required database migrations for supported database providers using repository conventions. Session writes use revision checks and operation IDs; duplicate sends or reconnects must not duplicate a turn. Provide bounded pagination instead of loading all conversations at once.

Implement versioned atomic local storage and session services in the device layer. Include local/server document and session mapping, account/backend isolation, accountless local session ownership, offline cached review, unsent user messages, and restart recovery. Implement the web persistence adapter, including retention of unsent text when a send fails or the page reloads. Establish a shared adapter boundary for presentation without copying storage logic into Razor components.

Define and implement explicit synchronization for owned mapped sessions: acknowledgements, optimistic concurrency, tombstones, and conflict retention. Conflicting branches remain inspectable; never silently merge incompatible conversation/proposal histories. Do not synchronize accountless sessions into an account without an explicit transfer. Keep the conversation record separate from existing AI application history.

Acceptance: both adapters survive restart/reload; message order and draft ancestry remain stable; retry does not duplicate turns; wrong-account/backend/document access is rejected. Migration, interrupted write, conflicting revisions, invalid local file, deletion, and mapping tests preserve existing data. Record any still-unverified live database gate.
```

## Prompt 3 Resolve targets and build visible story context

```text
Implement prompt 3 in docs/brainstorming-prompts.md with the common requirements after prompts 1 and 2.

Implement a shared target-resolution and context-policy layer with server and device source adapters. Resolve chapter/section/scene references against the actual saved structure and active manuscript. Capture a stable target identity and checked insertion position, not a displayed ordinal alone. Support an existing first section and a new first section under Chapter 2 without modifying structure during preparation. Duplicate titles, missing chapters, contradictory selected targets, and unsupported scene routes produce a focused clarification or actionable navigation.

Capture Chapter 1 in saved reading order across all relevant sections/pages. Include the synopsis, selected scene card, relevant explicitly enabled character/place/timeline canon and notes, and author-selected supporting documents. Existing authorship/authorization and device mapping govern every source. For local writing newer than the server copy, use an authorized checked local-context contract or report the limitation; never substitute stale cloud text invisibly.

Implement a bounded context manifest recording source IDs, document IDs, revisions/fingerprints, titles, excerpts/ranges, freshness, inclusion reason, summary versus full text, and omissions. Show actual inclusion separately from source availability. Prefer the requested preceding chapter and target context; expose budget limits and require a deliberate summarized/smaller-context path when mandatory context does not fit. Do not silently truncate the chapter or generate summaries as a hidden unmetered AI request. Attribute generated summaries to their source versions.

Treat quoted story documents as data, distinguish current manuscript facts from planning intent, and surface material contradictions. Context refresh invalidates obsolete previews where necessary but never erases discussion or writing.

Acceptance: multi-page chapters, reordered outline nodes, Unicode, standalone documents, oversized context, excluded sources, stale canon, deleted documents, unsaved edits, unauthorized project references, and local/cloud divergence are exercised. The manifest describes the exact context supplied to generation.
```

## Prompt 4 Implement conversational AI and streaming

```text
Implement prompt 4 in docs/brainstorming-prompts.md with the common requirements after prompts 2 and 3.

Add a brainstorming conversation action/service using existing server AI routing, entitlement/quota enforcement, usage accounting, and cancellation. Supply bounded prior dialogue, approved session decisions, resolved target, enabled context, and the user's current instruction. Distinguish exploration, clarification, draft generation/revision, and proposed planning changes through a validated response contract. Natural language can request these intents; a mode selector must not be required. No model-generated tool call may mutate the manuscript or planning directly.

Extend or adapt the existing streaming abstractions into an authenticated web/device transport. Include request/turn/sequence identity, answer deltas, draft-generation status, terminal validated output, usage, cancellation, and failure. Clearly separate explanatory answer text from manuscript prose. Incomplete JSON or interrupted text may be retained for inspection but cannot become an applicable proposal.

Persist accepted turns and terminal outcomes. Allow one active generation per session unless the implemented branch model explicitly supports more. Retry requires an explicit action and a new or recovered operation according to its known state. Deduplicate the same operation and settle usage according to existing policy; do not blindly reissue an uncertain provider request. Late events after account/document/session changes cannot attach to the new workspace.

Acceptance: exploration returns useful conversation without a draft; a drafting request produces typed draft data and provenance; requests can be stopped. Tests cover stream ordering, duplicates, disconnect, timeout, quota, authentication loss, malformed output, late completion, and an uncertain operation. A deterministic test provider establishes automated behavior; live provider behavior has a separate gate.
```

## Prompt 5 Integrate the three panel workspace

```text
Implement prompt 5 in docs/brainstorming-prompts.md with the common requirements after prompts 2–4.

Add a reachable Brainstorm entry in the actual web and Windows manuscript editor. Reuse the existing outline/navigation and editor host: outline on the left, manuscript/proposed text in the middle, AI conversation on the right. Share new presentation components and bind each surface to its real session/context/conversation adapter. Match existing Prosa styling and panel resizing. Preserve the ordinary writing workflow and unsaved editor state when entering, leaving, collapsing, or resizing Brainstorm.

Implement session creation/reopen, ordered messages, an accessible multiline composer, send/stop/retry, source inclusion review, target indication/clarification, and reading-progress states for context and generation. Selecting a different scene affects subsequent requests; it does not retarget a proposal already captured for another scene. Show that original proposal's target and offer explicit navigation. Narrow windows must retain all three functions through accessible collapsible panes rather than clipped fixed columns.

Use real streaming from prompt 4. Distinguish empty, saved, generating, stopped, interrupted, offline, unsupported-server, quota, and error states. Retain unsent composer text and focus/selection through failures. Never expose a decorative microphone as a working capability before audio implementation.

Acceptance: the actual desktop and web handlers can start/reopen a conversation, resolve a target, inspect context, send an exploratory turn, stop generation, and navigate without losing text. Verify keyboard composition, multiline messages, focus, long transcripts, panel resizing, narrow layouts, and empty/error/offline states. Record fixture and actual-host evidence separately.
```

## Prompt 6 Draft and revise through conversation

```text
Implement prompt 6 in docs/brainstorming-prompts.md with the common requirements after prompts 3–5.

Connect draft/revision results to the middle panel. Support "Write a first section for the second chapter that builds on what happened in the first chapter" and follow-up instructions addressing the same draft. A proposal contains actual prose, checked target/source context, style/POV/tense constraints, and any deliberately introduced story facts. The chat explains choices without duplicating the whole passage or rendering prose as chat-only output.

Persist a new draft version for each accepted generation/revision with parent version, instruction, source manifest, and created time. Provide original manuscript versus proposal review, previous/next version selection, and editing of the proposed text using compatible rich-content handling. Manual edits create or checkpoint a durable version, survive reload/restart, and become the reviewed candidate. A later AI revision must use that candidate when the author asks to revise it, not an older generated version.

Allow selection-based follow-ups such as "Keep the first paragraph, change the dialogue" using verified proposal anchors. Exploration such as "Why would she approach him?" does not alter the draft. Explicit drafting intent produces a new proposal; ambiguous wording gets a narrow clarification. Preserve earlier versions and keep their committed status independent of which candidate is currently visible.

Acceptance: the main sample journey produces two distinct persistent drafts; manual edits survive and inform subsequent revision; comparisons and version navigation work in both clients. Tests cover ambiguous intent, lost selection, invalid prose, incompatible rich content, cancelled partial versions, and navigation during generation. The manuscript remains unchanged until prompt 7's approved commit path runs.
```

## Prompt 7 Keep a reviewed version in the manuscript

```text
Implement prompt 7 in docs/brainstorming-prompts.md with the common requirements after prompt 6.

Implement Keep this version with explicit insertion/replacement review through existing checked writing and structure paths. The review shows the exact candidate, destination chapter/section/page or new-section position, and whether existing text is being replaced. A new first section is created only when approved; existing Chapter 2 content and order must be retained. Do not flatten a multi-page chapter or confuse a linked scene card's text with the manuscript section.

Capture and durably record the approved candidate and source/target snapshots before mutation. Flush necessary editor changes, recheck account/backend ownership, target existence/order, document/content/structure revisions, and rich-content support. Commit text plus any required structural creation as one operation, or use an explicit durable recoverable journal when the existing storage cannot make them atomic. Interrupted application must have inspectable status and safe recovery rather than an unexplained empty section.

Use an idempotent application identity. Double click, timeout/retry, reopening a session, or selecting the same already-applied candidate cannot insert it twice. Distinguish failed, confirmed applied, and unknown outcome. Record application in session and existing AI history only when committed. Provide guarded undo/redo that preserves later edits and unrelated structure; if the source has diverged, use existing recovery-copy behavior instead of destructive undo.

Acceptance: insert into an empty target, insert before an existing section, replace an existing checked passage, keep a manually edited proposal, restart, and undo/redo are verified with the actual editor mapping. Tests cover stale sources/targets/order, duplicate application, unsupported nodes, failed text save after structure creation, uncertain server results, and later user edits. Both clients persist exactly the approved prose.
```

## Prompt 8 Save selected ideas into planning

```text
Implement prompt 8 in docs/brainstorming-prompts.md with the common requirements after prompts 3–7.

Add contextual actions to selected discussion messages and typed decisions: Save as scene idea, Add to story notes, Propose synopsis update, and Propose character-note update. Use the actual existing storage models; distinguish authored notes from extracted canon. If canon mutation is supported, route it through its current checked approval contract rather than overwrite a generated bible wholesale.

Let the author choose the owned target, inspect before/after or new-item content, edit the candidate, approve only selected fields, and cancel. An AI suggestion of where an idea belongs remains a suggestion until reviewed. Proposed outline scenes are visually distinct from saved scenes; creating a scene idea must not create prose or an empty manuscript section implicitly. Do not apply all planning suggestions because the author approved one.

Persist provenance linking the approved planning change to its session/turn/version and source revisions. Reuse scene, synopsis, note, and structure commands with concurrency, operation deduplication, local/cloud mappings, durable recovery, and history/undo where the existing target supports it. Refresh the visible outline/planning and subsequent context after a confirmed save; preserve the exact old context manifest for earlier turns.

Acceptance: save a scene idea, a story note, one synopsis field, and a character note in both clients, then reopen the destination and session. Validate partial approval, duplicate save, missing/deleted target, stale planning, account change, failure/retry, undo or documented recovery, and preservation of unrelated links/tags/status/fields. Discussion alone never modifies canon.
```

## Prompt 9 Complete recovery and verify text brainstorming

```text
Implement prompt 9 in docs/brainstorming-prompts.md with the common requirements after prompts 2–8.

Audit the complete text journey across session, generation, preview, approval, manuscript/planning commits, local history, and synchronization. Finish missing production recovery paths. On restart/reload, distinguish unsent user text, accepted turns awaiting completion, interrupted generations, saved candidates, approved-but-pending operations, confirmed commits, and unknown outcomes. Reconcile known operations with durable server/local results instead of blindly regenerating, charging, inserting, or reporting success.

Verify session synchronization across authorized device/web copies of the same mapped manuscript. Preserve conflicting conversation branches, authored proposal changes, and pending application journals. Server receipts are authoritative for confirmed server operations; local snapshots remain authoritative for unsynchronized local writing under existing sync rules. Revoked access/sign-out removes the previous account's session from the active UI without deleting the author's local manuscript.

Run the text acceptance scenarios in the status file using isolated data through the actual editor routes: the Chapter 2 drafting/revision/keep/undo journey, exploration-only turns, selected supporting documents, planning saves, standalone manuscripts, offline review, restart, conflicts, and duplicate/uncertain operations. Fix defects within this scope rather than only record them. Add measured limits for long conversations and bounded context based on the implemented contracts.

Acceptance: prompts 1–8 have implementation evidence and the text workflow is usable in both surfaces. Record automated, browser, native, database, and live-provider results separately. Leave any unavailable live gate explicit and give a concrete reproduction; do not claim audio functionality. Handoff to prompt 10 with the stable conversation and application contracts.
```

## Prompt 10 Add real microphone dictation

```text
Implement prompt 10 in docs/brainstorming-prompts.md with the common requirements after prompt 9.

Add a real speech-input abstraction with web and Windows adapters. Inspect supported microphone capture and server/provider transcription capabilities, verify current official APIs, and implement the configured transport. The product should handle English and Swedish, with an explicit language or supported automatic detection. Use the existing authenticated backend policies, bounded audio duration/size, cancellation, usage accounting, and account/session isolation. Request microphone permission only after an explicit microphone action. Release capture resources on stop, navigation, sign-out, and disposal.

Dictation fills the existing message composer. Show listening/transcribing state and a readable final transcript that the author can correct before sending. Partial recognition is not a submitted user turn. Typed text and dictated text can be combined. Dictated manuscript prose can be placed into the proposed draft through an explicit destination action; never route it directly into saved manuscript content just because recording finished.

Implement permission-denied, unsupported-platform, no-device, silence, poor recognition, disconnected provider, timeout, cancellation, and authentication states. Preserve the composer if transcription fails. Do not store raw audio by default; bound any transient server buffers and document cleanup. Do not substitute a prerecorded clip or hard-coded transcript for live capture.

Acceptance: automated adapter/transport tests cover lifecycle and late results; live browser and rebuilt Windows tests capture the user's microphone into editable transcripts. Verify English and Swedish input, correction, sending into the same text session, interruption, and device disposal. Label simulated transport checks separately from real microphone/transcription evidence.
```

## Prompt 11 Add spoken dialogue and read aloud

```text
Implement prompt 11 in docs/brainstorming-prompts.md with the common requirements after prompt 10.

Add an explicitly started spoken conversation mode over the same session, sources, target, typed messages, proposals, and application history. Choose the configured supported speech-to-text plus text-to-speech pipeline or a supported real-time voice adapter after verifying current APIs. Audio orchestration must preserve the server's context, authorization, quota, and proposal validation; do not create a second unconstrained assistant or a second divergent conversation memory.

Implement turn taking, visible transcripts, concise spoken answers, mute/stop/end controls, and interruption while the AI is speaking. Stop stale playback and the relevant generation when interrupted; ensure echo/playback cannot submit the AI's own answer as an author instruction. Keep committed turns; mark interrupted assistant fragments as incomplete. Full prose generation updates the middle panel, while the AI can speak a brief explanation. Read draft aloud is an optional explicit action bound to a specific draft version; editing that draft stops or restarts playback clearly.

Support switching between typing and speaking without losing context. Voice may explore and revise proposed prose. An explicit spoken Keep this version can invoke the same reviewed commit boundary only when an identified version/target is visibly ready and the finalized transcript is unambiguous; otherwise request focused confirmation. Discussion, partial recognition, negation, interruption, or uncertain recognition must never trigger application. Use the identical rule for saving planning decisions.

Handle permission/device loss, unsupported output voice, audio failure, connection loss, backgrounding, sign-out, and account change without corrupting the session. Expose latency and playback state honestly. No sound starts on page load or from switching a design/view.

Acceptance: real two-way speech works in web and Windows using the same manuscript context as text. Verify interruption, echo protection, optional read-aloud, English/Swedish support, transcript ordering, voice-to-text switching, unchanged manuscript during exploration, explicit reviewed apply, and cancellation of stale audio/events. Keep native microphone, live-provider, and playback evidence distinct.
```

## Prompt 12 Verify the complete brainstorming experience

```text
Implement prompt 12 in docs/brainstorming-prompts.md with the common requirements after prompts 9–11.

Run all product journeys and acceptance scenarios in docs/brainstorming-implementation-status.md against the implemented web client/server and rebuilt Windows desktop host. Use an isolated manuscript with two chapters, multiple pages, synopsis, character/place notes, and a conflicting optional planning source. Include a standalone manuscript, a local manuscript awaiting server mapping, and authorized mapped device/web copies.

Trace each visible action to its actual handler, persisted result, source identity, and application/history record. Verify text brainstorming, conversational draft revision, manually edited versions, exact Keep behavior, new-section order, guarded undo/redo, planning-field review, visible source omissions, restart/offline/conflict recovery, real dictation, spoken answers, interruption, read-aloud, and text/voice switching. Exercise auth/quota/provider failures and ambiguous/noisy voice instructions. Fix defects and recheck the affected journeys.

Verify accessible keyboard operation, responsive panel behavior, screen-reader announcements, focus, large documents/conversations, and cancellation without freezes. Run appropriate affected builds and regression checks, including original writing tools, rich content, planning, local save/sync, and AI history. Packaged acceptance is a separate optional gate if packaging is available; development host evidence is not packaged evidence.

Record the exact launched binary/server versions, fixture data, commands, results, and UI evidence. Finish with a matrix separating implementation, automated checks, browser/native interaction, live AI, live microphone/playback, database/sync, and packaged status. Unavailable gates require concrete reproduction instructions and a next acceptance prompt. Do not mark the feature release-ready while required gates or known data-loss defects remain unresolved.
```
