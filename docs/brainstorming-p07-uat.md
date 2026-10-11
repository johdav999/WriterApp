# Prompt 7 manuscript Keep acceptance packet

Date: 2026-10-07. Product: Prosa web/server and Windows desktop Brainstorm workspace.
Role: author. Revision: original checkout's accumulated uncommitted P1–7 and unrelated
work. Synthetic owned standalone/project manuscripts, rich Unicode author version,
Chapter 1 and existing multi-page Chapter 2. No real manuscript/provider credential used.
Environment: migrated local SQLite, authenticated in-process TestServer, real atomic
device files, both actual shipped editor bundles in headless Edge 154.0.4258.62,
isolated rebuilt Windows startup. Fixtures are distinct from signed-in interactive UAT.

```powershell
# WriterApp.Client
npm run build
# Repository root
./scripts/verify-brainstorming.ps1 -Phase p07 -Browser -PlaywrightModule 'C:/Users/Johan/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright' -NativeStartup
```

Evidence: ignored `artifacts/brainstorming-p07`, including validated exact Keep JSON,
production-host review HTML, TRX/logs, browser JSON/screenshots and native startup JSON.
Production C# handlers run with synthetic JS transport; separate browser checks run
the actual editor transaction, content mapper, factories and reload. Neither alone is
a full signed-in browser or native Keep journey.

## Flow packets

| Flow | Preconditions and actions | Expected and observed | Evidence/scope |
|---|---|---|---|
| P7-01 Review and approve exact rich version | Owned completed candidate, manual bold/Unicode version; shared Review then Approve through each actual host handler. | Review shows original destination, mode and exact candidate; checkpoint/source capture before durable intent; confirmed receipt only after actual persistence. Both reachable handlers passed. | `BrainstormingKeepHostTests`, exported `keep-review-web/device.html`; synthetic interop/authentication. |
| P7-02 Empty insertion, passage replacement and new first section | Empty page, nonempty rich page, checked UTF-16 quote, or Chapter 2 first-section position before existing multi-page prose. | Exact reviewed HTML committed; original pages retained in order; no multi-page flattening; filled new section/page/scene committed together. Actual web/device mapper and reload preserve manual marks/Unicode and unrelated list/table/link. | `BrainstormingKeepTests`, `keep-{empty,insert,replace,new}.json`, `browser/keep-result.json`; migrated SQLite/device files plus real editor modules. |
| P7-03 Duplicate, restart and uncertain response | Replay approved operation, use another operation for same candidate, reopen services, force lost server commit response or interrupted local journal. | Same identity deduplicates; competing identity refuses; Unknown must inspect; committed response loss reconciles without another insertion. Local Applying proves saved/before/divergent state without replay. Missing record is Failed only after owned session proof. | Authenticated HTTP lost-response test; local journal tests; shared workflow no-dispatch assertion. |
| P7-04 Atomic failure and stale/private refusal | SQLite trigger fails page insert after new structure begins; change source/order/delete target; foreign account/backend/candidate; incompatible rich nodes. | Transaction rolls back new section/node/text/history; receipt remains Approved; same-identity explicit retry succeeds after removing fixture fault. Stale/private/unsupported operations retain originals and cannot confirm history. | Trigger, source/ownership/order/schema tests; actual browser refusal. No live SQL Server claim. |
| P7-05 Guarded undo/redo and separate recovery | Confirmed application; undo/redo with transition identity, interrupt desktop transition, then edit affected writing/order/planning. | Safe undo/redo preserves unrelated content; inspection changes only proven history metadata; later edits/order/planning refuse destructive restore. Separate original copy leaves current writing intact. Server copy retries return same document. Existing server annotations refuse unsupported mutation and preserve comments. | Device/Web history/Keep tests, original-copy assertions, ordinary page save/restart/mirror regression. |

Final scoped verification: **529 passed, zero failed/skipped**, including 32 Keep
cases and two ordinary page/history checks. Server/web Release and Windows Debug
builds passed with zero warnings/errors. Existing LocalStoryboard test analyzer
warning remains. Browser Keep has 24 checks; existing storage, 109 common editor,
context/conversation/workspace and proposal editing checks passed. Four Keep review
screenshots cover wide/narrow web/device layouts and were visually inspected. The
review stays accessible at 390px and explicit approval receives keyboard focus.

## Issue ledger

| ID | Severity | Flow | Finding and acceptance regression | Status |
|---|---|---|---|---|
| P7-I01 | P1 | P7-02 | Keep previously had no writing operation. Exact durable review, server atomic ledger, device atomic aggregate journal, committed history and session-cache receipts now exist. | Fixed; fixtures/modules verified |
| P7-I02 | P1 | P7-03 | Uncertain response could allow duplicate insertion. Immutable per-candidate application, Unknown-before-dispatch, inspect-before-retry and authoritative owned absence now prevent replay. | Fixed; lost-response/restart verified |
| P7-I03 | P1 | P7-05 | New-section undo compared approval-time creation timestamps with store-normalized timestamps and refused safe undo. Ignore only synchronization IDs and generated timestamps while comparing all authored section/node metadata. | Fixed; atomic local undo/redo tests |
| P7-I04 | P1 | P7-02 | Actual ProseMirror node equality refused nonempty insertion because preview fragments/results used another schema instance. All transaction nodes now use the actual editor's schema. | Fixed; both rebuilt bundles verified |
| P7-I05 | P1 | P7-01/05 | Late identity adoption and reload during writable editor could expose old private review or discard pending edits. Epoch/lease checks and save/freeze through receipt adoption guard both paths. | Implemented; ownership/host regressions; interactive pending-edit UAT below |
| P7-G01 | P2 | All | Production-host fixtures and actual modules are separate from signed-in interactive browser/native Keep. | Open acceptance gate |
| P7-G02 | P2 | P7-04 | Live SQL Server migration/transaction, live provider quality and installed packaging have no current evidence. | Open acceptance gates |
| P7-D01 | P2 | Separate quality workflow | An expanded class filter selected targeted-quality retry `valid` and `429-between` tests, which failed. The diagnostic was stopped and retained; final P7 suite includes only relevant ordinary page/history methods. No preexisting/P7 attribution established. | Separate reproduction required |

## Supported boundaries and follow-up

Replacement supports one ordinary top-level paragraph/heading. Multiple proposal
blocks require a complete paragraph. Nested image/lossy markup, unsupported ranges,
or server destinations with anchored annotations refuse while retaining originals;
use a new section or the annotation-aware manuscript editor. No silent flattening.
Device annotation reconciliation continues through the existing local planning store.

Local writing receipts stay in their scoped session cache/history. They are not
server writing confirmations and are not manufactured through conversation CRUD.
Cloud review of mapped local writing proves server stamp plus mapped page/outline
identity/content before approval. A durably approved fresh local operation can finish
offline. Full cross-device receipt/history transfer remains prompt 9.

For P7-G01, launch the exact rebuilt isolated executable in the status file and use
its isolated document data. In a matching authenticated Development web host and
rebuilt desktop, type pending manuscript text, select the original passage, review
the rich author-edited candidate, approve, reopen, inspect, undo/redo and recover after
a later edit. Repeat new-first-section insertion before multi-page Chapter 2. Verify
account/backend changes while requests are delayed and mapped local/server scope.
Preserve user-owned processes/documents. Startup evidence alone does not close this gate.

For P7-G02, use an authorized SQL Server test database with both migration/model
snapshots, perform the atomic failure and concurrent duplicate journeys, then separately
verify configured live provider draft quality, usage and final Keep. No provider call
is needed by Keep itself. Packaging and speech remain separate.

Reproduce P7-D01 with a bounded `FullyQualifiedName~ActualClientValidOrNonSemanticFailuresNeverCauseUnboundedOrIneligibleRetry`
run and diagnose against that workflow's unchanged production path. See retained
`expanded-quality-diagnostic.log`; no successful full expanded-suite claim.

Existing SQLite multi-manuscript index-migration and P14 list-ending editor
normalization gates remain. Next implementation is prompt 8: selected planning
decisions with partial approval; saving a scene idea cannot implicitly create a prose
section. Read the copyable handoff in the implementation status.
