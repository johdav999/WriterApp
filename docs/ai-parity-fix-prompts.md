# AI client / desktop parity fix prompts

Date: 2026-10-05. Baseline: [fresh parity audit](desktopai-gap-analysis.md), G01–G12. These prompts implement gaps in the current checkout; they do not repeat the older desktop rollout. Reinspect each finding before changing code because this checkout contains ongoing work.

All twelve findings are addressable through Codex implementation. Live provider, signed-in browser, native application and packaged-release acceptance depend on available environments. Those are verification gates, not reasons to leave independently implementable code unfinished. G09–G12 are policy/workflow differences; the policies proposed below are explicit scope choices, rather than already approved product requirements. Running an optional prompt adopts its stated policy.

Start a chat with:

> Implement prompt N in docs/ai-parity-fix-prompts.md, including the common requirements. Complete the implementation and available verification, preserve unrelated work, and record remaining acceptance gates.

## Sequence

| Prompt | Finding | Deliverable | Dependency |
|---|---|---|---|
| 1 | G01 / P1 | Preserve formatting in client targeted fixes | None |
| 2 | G08 / P2 | Cancel remaining client AI requests | None |
| 3 | G05 / P2 | Select scene-card fields in client proposals | None |
| 4 | G06 / P2 | Supply saved glossary to desktop checks | None |
| 5 | G07 / P2 | Resolve client consistency findings across pages | 1 |
| 6 | G02 / P2 | Full explained client Style & quality review | 1, 2 |
| 7 | G03 / P2 | Execute recommended writing tools in both hosts | 2 |
| 8 | G04 / P2 | Client scene/synopsis scoped history recovery | 3 |
| 9 | G04 / P2 | Client aggregate writing/translation recovery | 7, 8 |
| 10 | G09 / P3, optional | Durable desktop quality dismissal | 4 |
| 11 | G10 / P3, optional | Shared, bounded targeted-result retry policy | 1, 2 |
| 12 | G11 / P3, optional | Explain explicit reusable-prompt transfer | None |
| 13 | G12 / P3, optional | Shared advertised tone choices | None |
| 14 | All implemented findings | Fresh integration check and gap table | 1–9; include optional prompts actually run |

Execute tasks sequentially in this existing checkout. Dependencies refer to working implementation with available focused checks passing; unavailable provider/device acceptance must remain separately recorded. Do not continue content-changing dependent work past a reproduced data-loss defect.

## Common requirements

- Read applicable repository instructions, this file, the fresh section of `docs/desktopai-gap-analysis.md`, `docs/device-development.md`, `docs/editor-content-compatibility.md`, and relevant entries in `docs/desktopai-uat.md`. Inspect current source and working-tree changes. Preserve unrelated staged, unstaged and untracked work; do not reset, clean, move to a checkout without those changes, or recreate already completed functionality.
- Trace reachable UI through request preparation, authorized backend execution, review, Apply, persistence and recovery. Reuse `WriterApp.UI.Shared` presentation and existing typed contracts. Keep host orchestration in its current layer; never reference the client from shared/device libraries. Choose the working reference flow per finding rather than copying a known defect.
- Preserve save-before-request, source identity/revision checks, rich content, bounded requests, explicit review and approval, entitlements, quotas, ownership, account/backend isolation, sync conflicts and durable recovery. Treat provider output as inert data. Stale, malformed, ambiguous or cancelled results must not change authored content.
- Use synthetic documents and existing test seams. Never edit the user's manuscript for a probe. Do not expose provider secrets, weaken authentication, deploy, distribute packages or change external identity registrations. Avoid new dependencies unless current facilities cannot implement the task.
- Add focused behavioral regression tests for meaningful content, recovery, cancellation and persistence boundaries. Extend the real-editor harness for changed editor behavior and regenerate shipped assets from source. Build affected projects, using isolated output when running assemblies are locked. Do not repeat unrelated suites merely to increase test counts.
- Exercise meaningful UI changes in both relevant hosts when available. Before claiming native acceptance, verify the running binary contains the change and relaunch safely with isolated data. Source checks and tests do not establish live provider, native or deployed acceptance. Record unavailable prerequisites and keep implementing independent work.
- Append dated implementation and verification evidence to `docs/desktopai-uat.md`; update the fresh audit with current status without deleting historical evidence. Include files/contracts changed, commands and actual results, checks not run, remaining defects and the next concrete task. If an audited gap has already been fixed, verify it and document the result instead of duplicating it.

## Prompt 1 — Formatting-preserving client targeted fixes

```text
Implement prompt 1 with the common requirements. Close G01 by giving client targeted quality fixes the formatting-preserving behavior already implemented on desktop.

Trace PageEditor.ApplyQualityIssueFixAsync, rewrite-to-replace normalization, tiptap-editor-patch.js applyQualityIssueFixDetailed, and device-editor.ts previewTargetedQualityRevision. Reuse or extract the safe range/minimal-change logic where schemas permit. Retain unchanged marks and unrelated content, check the complete resulting text, and reject ambiguous anchors, embedded content, unsupported block crossings and incompatible formatting within changed wording without mutation. Do not solve this by stripping formatting or by removing structure guards. Keep existing preview/Apply, revision and Undo behavior.

Inspect consistency replacements using the same client helper and cover their supported path too. Do not claim a consistency defect was reproduced solely from the quality probe.

Regression: a sentence containing bold “clock” whose last plain “clock” becomes “chime” must retain the unchanged bold word in both shipped editors. Cover italic/link marks, deletion, insertion, Unicode/combining boundaries, mixed changed spans, stale source, duplicate anchors and embedded content. Rebuild bundles and run the real-editor probe from artifacts/ai-parity-2026-10-05 or its maintained equivalent.

Acceptance: supported targeted fixes preserve marks and surrounding structure; rejected cases leave the document identical; Apply and Undo restore exact supported content. Record fresh browser evidence and remaining native/provider gates.
```

## Prompt 2 — Client request cancellation

```text
Implement prompt 2 with the common requirements. Close G08 for ordinary writing, targeted quality and consistency; preserve existing structured-translation cancellation.

Trace DocumentEditor PostAiActionAsync and all preparation, generation and strict-retry paths in these flows. Use one owned cancellation lifetime per operation, propagate its token through supported asynchronous calls, and expose a clear Cancel control while pending. Cancel on relevant document/navigation/disposal transitions. Preserve save atomicity: cancelling an AI operation must not interrupt a durable save in a way that loses writing.

Protect against late results and races: cancelled or superseded responses cannot populate an active proposal or become applicable, and an old operation's cleanup cannot clear a newer operation's state. Cancellation is distinct from Dismiss after generation. Do not promise that cancellation refunds quotas or always stops a remote provider after dispatch.

Verify cancellation during preparation, generation, strict retry, navigation and disposal; test late completion after cancellation and a fresh request immediately afterward. Confirm the UI returns to a usable state, authored content is unchanged, and ordinary errors remain distinguishable from cancellation.

Acceptance: each named client flow has working in-flight cancellation with propagated tokens and stale-result protection. Match desktop semantics where applicable and record transport/provider cancellation limits accurately.
```

## Prompt 3 — Client scene-card partial approval

```text
Implement prompt 3 with the common requirements. Close G05 in the document editor and storyboard scene inspector.

Reuse SceneCoachingReview and the desktop approved-field selection contract. For a single generated card proposal, show current/proposed values and allow any subset of changed fields to be selected. Support explicit full-card approval as a convenience. Empty selection must not save; unchanged and unselected fields must retain their exact current values. Preserve separately requested single-field actions.

Trace DocumentEditor ApplySceneAiProposalAsync, StoryboardSceneInspector, LocalAiPanel and LocalSceneCoaching. Bind proposal selection to document/scene/source revision. Persist the selected fields in one validated operation; reject stale proposals instead of silently overwriting newer authored fields. Preserve lifecycle fields and existing durable recovery/history reporting. Do not implement client-side filtering that a backend full-card write subsequently defeats.

Verify arbitrary subsets, clear/all selection, cancelled review, empty selection, stale source, save failure and reopen. Check both editor and inspector routes with the shared review UI.

Acceptance: one proposal supports partial approval through persistence, with unselected fields unchanged and a precise history record of the approved changes.
```

## Prompt 4 — Desktop glossary-aware quality checks

```text
Implement prompt 4 with the common requirements. Close G06 by supplying the document's saved glossary to desktop quality analysis.

Compare server QualityCheckService.RunChecksAsync and LocalQualityChecks.Analyze. Trace existing glossary ownership, storage and synchronization before adding contracts. Load the current document/account glossary into the shared QualityCheckContext. If no device representation exists, implement the smallest versioned cache/sync addition needed, with identity mapping and safe migration rather than a separate hardcoded vocabulary.

Use available cached glossary data offline. Define and display an accurate freshness/unavailable state when needed; an unavailable glossary is distinct from a verified empty glossary. Never use another document/account's terms. Preserve ordinary offline writing and existing checks when context cannot be refreshed. No server call should be required for every local keystroke.

Verify shared-rule parity for term casing and near matches using equivalent glossary fixtures. Cover empty data, cached offline checks, updates/deletions, account/document switches and failed refresh. Verify existing local data migrates without loss.

Acceptance: equivalent saved glossary and prose produce equivalent supported glossary findings in both hosts, with the desktop freshness/offline behavior documented and tested.
```

## Prompt 5 — Client consistency findings across section pages

```text
Implement prompt 5 with the common requirements after prompt 1. Close G07 without rebuilding cross-scene comparison evidence already available in both hosts.

Trace ExecuteContinuityActionAsync, BuildContinuityApplyRangeAsync, DocumentEditor.CheckedCanon.cs, LocalConsistencyContext and LocalConsistencyRevisions. Capture the analyzed section's ordered page identities and checked source revisions using the client's existing structured-source contracts. Bind each primary finding to its actual page and a unique verified anchor.

Add Jump/review/fix for a primary passage on a noncurrent section page. Save pending edits before navigation, open the correct page, resolve against its current source and apply through the safe formatting-preserving path. Duplicate text, moved/deleted pages, changed source or unsupported rich spans must produce a useful stale/ambiguous state without applying elsewhere. Keep conflicting-evidence navigation and existing decisions working.

Verify a finding on page two while page one is active, identical prose on multiple pages, edits after analysis, page reorder/delete, unsaved active-page changes, save failure and recovery. Maintain bounded context and the backend's authorization checks.

Acceptance: primary findings anywhere in the supported analyzed section can be navigated, reviewed and safely fixed; source drift cannot redirect the replacement to another passage.
```

## Prompt 6 — Full explained client Style & quality review

```text
Implement prompt 6 with the common requirements after prompts 1 and 2. Close G02 using the desktop's existing shared report/review contract.

Trace client StyleQualityOptions TargetedOnly=true, shared StyleQualityReview contracts, StyleQualityRevisionReview and desktop orchestration. Add reachable selection/current-page explained review to the client alongside individual targeted findings. Reuse the shared review UI for correction versus preference, reasons/tradeoffs, individual edit selection and partial approval.

Capture checked rich source and revision before generation. Validate every proposed edit, prevent overlapping/ambiguous replacements and preserve untouched structure. Review must show what approval changes. Use existing atomic/recoverable application mechanisms; if a multi-edit apply cannot be made atomic, implement explicit recovery rather than leaving an undocumented partial result. Provider HTML remains inert. Support cancellation, empty/no-valid-edit results, Dismiss and stale proposals.

Verify selection and page scopes, approve one/subset/all, overlapping edits, malformed results, mixed formatting, source drift, cancellation, save failure and Undo. Exercise the reachable client flow with synthetic prose and the shared desktop reference.

Acceptance: client supports the complete explained-review journey with safe partial approval and recovery, while existing targeted findings continue working.
```

## Prompt 7 — Matching recommended writing tools

```text
Implement prompt 7 with the common requirements after prompt 2. Close G03 in both hosts using WritingRecommendations as the shared action source.

Trace CreateRecommendedToolOption, CaptureStructuredWriting, reusable-prompt checked contracts, LocalWriting and LocalWritingPanel. Define typed supported targets and parameter contracts for every currently advertised recommendation, including Deepen Character, Raise Stakes, Generate Headlines and Summarize Clearly. Preserve the real user/system templates and their intended output semantics. Do not bypass preflight by marking arbitrary custom_transform requests trusted or by attaching a fake reusable-prompt contract.

Route client recommendations through a valid checked execution contract so ordinary section recommendations no longer fail for missing contract metadata. Add actual recommendation execution to desktop instead of representing it only as next-paragraph craft focus. Use matching parameters, bounded context, review/Apply, source checks, persistence and recovery in both hosts. Retain next-paragraph focus if useful as a separately identified operation.

Classify non-manuscript outputs, such as candidate headlines, explicitly. Show them for selection/copy or apply to a validated intended field; never replace an entire section with a list merely to satisfy a generic transform route.

Verify catalog-wide request construction, supported targets and parameters, malformed output, stale source, rich multi-page content, cancellation and recovery. Exercise representative prose-transform and candidate-list actions in both hosts.

Acceptance: each advertised recommendation has a reachable matching action with its actual template and safe output-specific approval; unsupported combinations are explained before generation.
```

## Prompt 8 — Client planning-field AI history recovery

```text
Implement prompt 8 with the common requirements after prompt 3. Close the scene/synopsis portion of G04.

Trace EfCoreAiActionHistoryStore replay eligibility, client Synopsis and scene proposal application, LocalAiHistoryActions and DeviceAiHistoryService. Add durable client history snapshots and authorized Undo/Redo for the exact approved scene-card and synopsis fields. Do not treat provider request/response text or a comparison-only cloud entry as a complete recovery snapshot.

Capture before/after state and expected target revision at the persistence boundary. Undo/Redo must validate target ownership and current state and restore only fields changed by the approved action, preserving unrelated later edits. Define explicit conflict/refusal behavior when the affected fields changed. Make application/history persistence transactional where existing storage permits; preserve the current durable reporting/outbox behavior and handle failure without inventing a recoverable record.

Expose recovery from the reachable planning/history UI. Existing older records without sufficient snapshots remain comparison-only with an explanation. Add safe migrations/contracts if needed; do not invent before-state for historical records.

Verify partial scene approval, synopsis fields, unrelated later edits, affected-field conflicts, repeated Undo/Redo, reload, deleted targets, account isolation and history-save failure.

Acceptance: newly approved client planning changes have durable scoped recovery; unsupported legacy records are accurately identified, and desktop recovery remains working.
```

## Prompt 9 — Client aggregate AI history recovery

```text
Implement prompt 9 with the common requirements after prompts 7 and 8. Close the section-writing/translation portion of G04 while retaining original-copy recovery.

Trace client checked writing/translation, aggregate history contracts, server replay storage and desktop section recovery. Implement durable before/after snapshots for supported multi-page writing/translation actions, including page identity, ordering and structural changes where the action actually changes them. A generic flat-text snapshot is insufficient.

Define and implement atomic or explicitly recoverable Undo/Redo with revision checks across all affected pages. Preserve unrelated edits and refuse unsafe recovery after affected content changes. Failure halfway through a multi-page save/recovery must not leave an untracked partial manuscript. Use existing command/transaction/recovery mechanisms and preserve sync conflict behavior. For translated-copy creation, define the supported recovery semantics explicitly; do not delete a copy that has subsequently been edited.

Keep existing original-copy recovery available. Do not silently convert older comparison records into portable recovery entries. If cross-host replay is supported, use an explicit versioned snapshot/capability contract; otherwise label its scope truthfully instead of treating local identifiers as cloud identifiers.

Verify multi-page formatting and order, reload, apply failure, mid-recovery failure, stale one-page revision, unrelated-page edits, translated-copy edits, retries and Undo/Redo idempotence.

Acceptance: supported new aggregate actions have safe durable client recovery with clearly advertised scope; copy recovery and desktop history do not regress.
```

## Prompt 10 — Optional: durable desktop quality dismissal

Proposed policy: retain dismissal across desktop reruns/restarts and converge with the backend when the corresponding finding can be mapped safely. A materially changed passage is eligible for a new finding.

```text
Implement prompt 10 with the common requirements and the stated proposed policy after prompt 4. Address G09 by replacing desktop current-check-only dismissal with durable document-scoped decisions.

Inspect server DismissIssueAsync and its actual finding identity/lifetime before designing storage. Reuse compatible rule, source and occurrence identity; never suppress all occurrences merely because they share a rule or word. Persist local decisions with safe migration and account/document isolation. Restore them on rerun/restart only when they still identify the same supported finding.

Synchronize dismissals only through a verified backend mapping. Queue offline decisions with idempotent retry/conflict behavior; expose pending/unmapped status where relevant. If current identities cannot establish equivalence, add an explicit compatible mapping contract rather than claiming cross-host continuity. Provide a way to restore/review dismissed findings using shared presentation where practical.

Verify duplicate occurrences, rerun/restart, changed passage, rule/context changes, offline dismissal/reconnect, account isolation and failed synchronization. Preserve already persisted client decisions.

Acceptance: dismissal lifetime follows the documented policy, changed findings are not accidentally hidden, and any remaining mapping limitation is visible in the audit.
```

## Prompt 11 — Optional: shared targeted-result retry policy

Proposed policy: both hosts explicitly offer the same opt-in automatic strict retry, capped at one additional generation for eligible invalid targeted results. Explain that the extra request may consume quota. Leave it disabled unless the writer opts in.

```text
Implement prompt 11 with the common requirements and the stated proposed policy after prompts 1 and 2. Address G10 for repetition, sentence-length and passive-voice targeted rewrites.

Trace the client's EnsureRepeatedWordRewriteFixAsync and equivalent validators/retry handlers and desktop LocalQualityPanel. Extract compatible validation and retry eligibility into shared contracts/services without moving provider credentials or backend execution into presentation. Add the same explicit retry choice and explanation to both hosts.

With opt-in enabled, allow at most one strict additional request only after an eligible semantic validation failure. With it disabled, show the invalid-result explanation and manual retry. Authentication, quota, network, cancellation, stale source, embedded-content and anchor failures must not trigger automatic generation. Validate the second result fully and never loop. A cancelled/superseded operation cannot schedule a retry or offer its output for Apply.

Verify zero/one retry behavior, valid first result, invalid second result, cancellation between attempts, stale source and quota/network failures. Confirm actual request counts in both host adapters.

Acceptance: both hosts follow the explicit bounded policy, disclose possible quota consumption and preserve writing when no valid candidate is available.
```

## Prompt 12 — Optional: explain reusable-prompt continuity

Proposed policy: retain explicit local/cloud transfer and conflict review; clarify it in both hosts rather than introducing automatic synchronization.

```text
Implement prompt 12 with the common requirements and the stated proposed policy. Address the explainability portion of G11; automatic library synchronization remains outside this task.

Trace the client preset library, LocalPromptPanel and DevicePromptLibrary.Transfers. Make local versus cloud origin and transfer state clear. Explain how to import/copy/send a preset, when another host sees a change, and how retry/conflict choices work. Reuse current transfer actions and shared presentation. Surface existing comparison/conflict information before replacement; do not create duplicate prompts or bypass explicit resolution merely to simplify wording.

Make the relevant help/status reachable from both preset libraries. Preserve offline local execution, account isolation, explicit deletions and existing pin/edit/run semantics. Do not imply automatic synchronization or remote completion from a queued local transfer.

Verify the synthetic edit → explicit transfer → other-host refresh journey where the backend is available, plus offline/pending, failed retry and conflict states. For these reversible presentation changes, use existing transfer tests and focused UI checks; add behavioral tests only if transfer behavior changes.

Acceptance: writers can understand and complete the existing transfer journey in either host. Record G11 as an intentional, explained manual workflow, not automatic-sync parity.
```

## Prompt 13 — Optional: align tone presets

Proposed policy: preserve existing tones and add Executive to the shared advertised choices, keeping its existing client meaning.

```text
Implement prompt 13 with the common requirements and the stated proposed policy. Address G12 by deriving the advertised tone choices from a shared supported descriptor list.

Trace client _aiActionPresets, shared WritingActions.Tones, WritingOptions and backend tone validation/prompt construction. Preserve existing stable identifiers and saved presets. Add Executive consistently to the supported shared options and map it to the existing client prompt meaning. Update both reachable menus so labels, values and request semantics agree; do not change model/provider configuration.

Verify all existing choices remain accepted and Executive constructs equivalent requests in both hosts. Reuse relevant writing endpoint tests; add a focused contract check only where validation changes. Check both menus and saved-preset compatibility. No live generation is necessary to establish the menu/request contract, and provider prose quality remains a separate acceptance gate.

Acceptance: both hosts advertise and execute the same supported tone options without breaking existing presets.
```

## Prompt 14 — Fresh integrated parity acceptance

```text
Execute prompt 14 with the common requirements after prompts 1–9, including optional prompts actually implemented. Perform a new parity audit rather than copying the 2026-10-05 results.

For G01–G12 trace reachable controls, checked request construction, backend execution, proposal review, partial approval, persistence and recovery. Mark each as implemented/verified, remaining defect, intentional documented difference or pending environment-dependent acceptance. Do not mark unrun optional policy changes complete.

Run affected focused suites and real shipped-editor regression harnesses. Build the relevant client/server and Windows host using repository-supported commands. Exercise synthetic browser and actual desktop journeys when available, including formatting preservation, full style review, recommendation execution, scene subset approval, glossary checks, other-page consistency fixes, cancellation and new scoped recovery. Verify request counts for retry policies and persistence across reopen.

Use existing supported provider configuration only when available. Distinguish deterministic contract checks from live generation quality, native loading and deployed/package acceptance. Do not modify user manuscripts, deploy, distribute or invent passing live evidence.

Update docs/desktopai-gap-analysis.md with a dated table covering every original finding and any newly reproduced regression. Append commands/artifacts and exact scope to docs/desktopai-uat.md. Include remaining prerequisites and concrete reproduction steps. Core parity cannot be declared complete while a known unsafe Apply/recovery path remains.

Acceptance: a reviewable current gap table, fresh evidence for checks actually run and clearly identified remaining gates; no blanket parity claim based only on source or unit tests.
```
