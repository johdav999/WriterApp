# Prompt 5 workspace acceptance packet

Date: 2026-10-07. Product: Prosa web and Windows desktop manuscript editors.
Revision: uncommitted P1–5 in the original checkout, based on
`09352ded5c4f115578329323cf48569f141babd6`. Role: author, synthetic owned data only.
Environment: local .NET authenticated TestServer/SQLite, headless Edge 154.0.4258.62,
isolated rebuilt Windows executable startup. No real manuscript or provider secret.

Launch/verification:

```powershell
./scripts/verify-brainstorming.ps1 -Phase p05 -Browser -PlaywrightModule 'C:/Users/Johan/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright' -NativeStartup
```

Evidence: ignored `artifacts/brainstorming-p05` contains TRX, build/test/browser logs,
JSON reports and wide/narrow PNGs. The desktop executable and isolated data paths are
recorded in `native-startup.json` and the implementation status. Startup checks close
only their launched PID; they do not exercise native controls. Browser screenshots
contain synthetic data. Test fixture HTML uses the actual shared Razor shell and
production JS/editor modules; pane clicks are fixture bindings, not a signed-in WASM
session. C# production host/component handlers are exercised separately.

## Flow packets

| Flow | Preconditions and steps | Expected and observed | Scope/evidence |
|---|---|---|---|
| P5-01 Enter/leave without losing writing | Actual web `PageEditor` and device `DeviceTextEditor` handler fixtures; enter/leave Brainstorm with pending writing. Open/close/collapse/change pane in rendered shared shell. | Same editor instance, pending text retained, no forced manuscript save on entry/exit. Device DOM flush records pending text. Ordinary offline writing initializes independently of Brainstorm. Passed. | Production adapter tests, workspace tests; exact rich device DOM retention in `workspace-result.json`. |
| P5-02 Create/reopen a private conversation | Real atomic local session store; create, author multiline Unicode message, prepare context, create another session, reopen first, leave/reenter. Repeat Available/Offline/Unsupported capability states. | Composer, source inclusion choices and captured target restored; a fresh review required before next send. No generation or automatic remote exchange on reopen. Changing document clears active private/busy state but preserves earlier cache. Passed. | `BrainstormingWorkspaceTests`; real IndexedDB reload/partition tests separately. Picker covers current scoped cache, not remote-only discovery. |
| P5-03 Scene selection and original proposals | Captured target plus terminal inert draft; change selected section, inspect middle preview and provenance; invoke production navigation guards with wrong document and device same-page target. | Next request readiness invalidated; earlier draft keeps original destination. Wrong-document navigation refuses with writing retained; same-page navigation retains pending device prose. Rich/provider HTML remains literal encoded data. Passed. | Workspace and adapter tests, rendered wide screenshot. Existing checked navigation/save paths reused; full interactive cross-page journey pending. |
| P5-04 Send/stop/inspect/retry | Real authenticated HTTP NDJSON with deterministic provider waiting after acceptance; actual shared Send and Stop; inspect until owned Cancelled; explicit second Send. Also quota/offline/source-change failure fixtures. | One accepted stopped operation, inspection does not redispatch, explicit retry creates a distinct second operation. Two provider calls and two usage settlements, four ordered turns, no duplicate operations or partial draft. Composer survives pre-send failure; unknown operations retain blocking. Passed. | `SharedStopInspectionAndExplicitRetryUseRealAuthenticatedTransport`, P4 service/workflow/auth/recovery regressions. Deterministic provider, not live billable acceptance. |
| P5-05 Narrow layout/keyboard/long conversation | Actual Razor with 62 stored turns and 40 initially rendered, shipped device editor with rich Unicode pending HTML; Edge at 1600×1000 then 390×844. Resize by keyboard/pointer, visit all panes, type multiline text, restore caret/focus. | Bounds/storage work, panes reachable without horizontal overflow, earlier-message control visible, encoded prose, exact editor HTML unchanged, focus and selection restored. Passed; both PNGs visually inspected. | `workspace-result.json`, `workspace-wide.png`, `workspace-narrow.png`; real production modules with fixture pane binding. |

## Issue ledger

| ID | Severity | Flow | Type | Summary and regression | Status |
|---|---|---|---|---|---|
| P5-I01 | P1 | P5-01 | defect | Eager hidden workspace initialization accessed new dependencies during ordinary writing. Lazy first entry retains ordinary host operation; existing LocalWritingPanel regressions pass. | Fixed |
| P5-I02 | P1 | P5-02 | defect | Late async composer restoration could overwrite typing while a conversation opens. Composer is read-only during restoration; scope epochs reject older loads. | Fixed |
| P5-I03 | P1 | P5-02 | defect | Document change could retain private proposal/busy state. Clear active cache/capability/loading/generation on document/account change, guard preview document and older Run completion; durable original cache retained in regression. | Fixed |
| P5-I04 | P2 | P5-02 | defect | Reopening displayed sources without restoring their choice inputs. Restore inclusion, summaries/source attribution and support selection; require fresh review; source-choice reopen regression passes. | Fixed |
| P5-I05 | P2 | P5-05 | polish | New resize handles overlapped conversation text. Position on outline/right pane edges; inspected final wide/narrow captures and actual resize checks pass. | Fixed |
| P5-I06 | P2 | P5-03 | clarity | Restored review described current selection instead of the captured target. Host target labels now identify original destination, and middle preview warns on another selection. | Fixed |
| P5-I07 | P2 | P5-03 | defect | Navigation refusal threw InvalidDataException beyond both hosts' error filters. Catch it explicitly and show the retained-writing error; both production wrong-document navigation checks pass. | Fixed |
| P5-G01 | — | All | acceptance gate | Signed-in interactive web/native end-to-end journey and configured live-provider response/cancellation/settlement were not exercised by these fixtures. | Pending external acceptance |
| P5-G02 | — | P5-02 | integration gate | Sessions that exist only on another device/browser require deliberate remote discovery/import with identity mapping and conflict review. Current picker is scoped cached history; P9 completes integrated sync/recovery acceptance. | Pending P9 |

## Interactive gate reproduction

Use disposable synthetic standalone and project manuscripts with Chapter 1 spanning
multiple pages, Chapter 2, synopsis, explicitly selected notes/support and rich text.
Start the rebuilt isolated Windows executable recorded in the status; for web use the
local authenticated client editor `/documents/{DocumentId}`. Use a configured backend
and test account; keep secrets server-side. Do not use real manuscripts as test data.

1. Type unsaved rich Unicode prose, enter/leave Brainstorm, resize/collapse panes,
   and narrow the window. Confirm editor content/caret remain and every pane is reachable.
2. Create a conversation, review the destination/sources, type several lines, create
   another, reopen the first, then restart/reload. Confirm retained input/history/choices
   and fresh-review requirement. Switch account/backend/document; no prior private
   context should appear under the new identity.
3. Save/synchronize through existing controls and explicitly review server context.
   Ask for three openings; confirm exploration does not create/apply prose. Request a
   draft; confirm validated prose is separate in the middle and writing stays unchanged.
4. Choose another scene, refresh the next request's context, then explicitly navigate
   the older proposal's original destination. Test a failed save/deleted target; writing
   and proposal remain with an actionable refusal. Full cross-page host UAT is this gate.
5. Stop a streamed answer, inspect the retained original operation, and explicitly retry
   only after known terminal failure/cancellation. Disconnect/reconnect or reload during
   an unknown request; no paid replay should occur. Compare owned operation and usage
   records with actual provider settlement before claiming live acceptance.
6. Review a long conversation offline, then reconnect. Confirm cached writing/history
   remain usable and neither generation nor application happens automatically.

Live SQL Server, preexisting SQLite multi-manuscript migration-index acceptance,
packaged/MSIX, microphone and audio gates remain as recorded in the main status.
The next implementation is prompt 6: durable version/manual proposal review. Manuscript
and planning mutation still require prompts 7/8.
