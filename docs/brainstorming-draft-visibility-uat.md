# Generated prose visibility repair — 2026-10-09

## Verified root cause

The reported patrol scene was generated and persisted successfully. Read-only inspection
matched the screenshot's user prompt and assistant response to saved session
`f22349df-dae9-4f88-b2df-a81322133f47`, request
`059afd62-8323-495b-bce9-81e8c3846683`, and selected draft
`626a14f3-783a-4e63-8a0a-f1b12a122857` (1,594 characters). The request was Completed,
with no pending generation. No cache files were manually edited.

The author was viewing the Manuscript pane. `EditorWorkspace` hides the proposal in
that pane, while `BrainstormingWorkspace` sets the conversation's `ShowDrafts=false`.
Consequently the reply announced a draft but offered neither its prose nor an action
to reveal it. Clicking the existing Brainstorm rail in the original native app showed
the saved scene immediately. This is a view/discoverability defect, not lost provider
output or missing persistence. The earlier proposal-lifecycle repair was already in
the original running executable; see `brainstorming-follow-up-panel-uat.md` for that
separate defect.

Native retesting also exposed an asynchronous resize-handler race: an import could
finish after its handle had been removed, causing `handle.closest` to throw and stop
Blazor rendering while opening saved conversations. Both component lifetime checks
and a missing/disconnected-element JavaScript guard now cover that race.

## Changes and issue ledger

| ID | Severity | Flow | Acceptance and regression | Status |
|---|---|---|---|---|
| PROSE-001 | P1 | Read generated scene | A newly selected/generated candidate opens Working draft. Completed replies also expose escaped Generated prose and View working draft. Ordinary updates to the same candidate preserve the user's chosen pane. Both native/device and web hosts forward the navigation action. | Verified in actual native and rebuilt web UI, plus shared/host tests. |
| PROSE-002 | P1 | Switch panes/open saved conversation | Import completing after component disposal does not attach; overlapping renders share one import; absent/disconnected DOM handles do not throw. | Verified with delayed-import component and JS regressions, and successful rebuilt native reopening. |

The existing review/keep workflow remains the point at which prose enters the
manuscript. Displaying a proposal does not apply it. No provider request, draft edit,
or Keep action was performed against the author's data during verification.

## Product profile and evidence

- Surface/role: Windows MAUI WebView, existing author session; web editor, disposable
  LocalDev SQLite acceptance data. Flows: library double-click, saved conversation
  reopening, automatic proposal display, conversation preview, and draft navigation.
- Rebuilt desktop: `WriterApp.Desktop/artifacts/brainstorming-missing-prose/user-build/Debug/net10.0-windows10.0.19041.0/win-x64/WriterApp.Desktop.exe`.
  The final runtime receipt contains the executable path, PID, timestamp and shared UI
  assembly hash. This build uses the normal author data directory.
- Browser: rebuilt Debug server/client at `http://127.0.0.1:5399`, disposable
  `artifacts/brainstorming-missing-prose/interactive-seed.db`. The validation host was
  stopped after checks; the author's existing localhost:5387 backend was preserved.
- Session evidence: ignored `artifacts/brainstorming-missing-prose/`.

Native acceptance opened the original document by double-click, entered Brainstorm,
selected the exact saved conversation in History, and confirmed Working draft text
equaled the saved 1,594-character scene. It then selected Manuscript, expanded
Generated prose, and used View working draft to return to the same proposal.
`native-result.json` records that drafts, turns, requests and composer were retained;
`native-before.png`, `native-draft-navigation.png`, `native-conversation-preview.png`,
and `native-fixed.png` show the baseline, diagnosis and repaired flow. The corrected
native application was left running with that draft selected.

The original user process was closed through its normal save-on-close path before
relaunch. Recovery of an agent-launched renderer failure checked the live manuscript
against its saved page before stopping only that owned process. A later inspection
retry incorrectly navigated the MAUI WebView as a standalone page; the owned app was
restarted and the final native acceptance rerun passed. Failed inspection captures
are superseded by `native-result.json`; they are not new production defects.

## Verification

| Check | Result |
|---|---|
| Focused .NET tests | 70 passed, 0 failed/skipped: workspace, drafts, conversation adapters and resize lifetime. `focused.trx`, `focused.log`. |
| Resize JavaScript | `node --test Tests/panel-resize.test.mjs`: 6 passed. |
| Actual rebuilt web UI | `Tests/brainstorming-draft-visibility.mjs`: automatic candidate reveal and conversation preview/navigation at 1600, 900 and 390 pixels; manuscript unchanged, no generation sends, no page errors. `browser/result.json`. |
| Actual rebuilt native | Original saved scene matches both preview and editor; saved conversation content preserved; no model request or manuscript application. `native-result.json`. Also rechecked the previous library double-click change. |
| Windows build | Passed, 0 warnings/errors. `desktop-build.log`. |
| Whitespace | `git diff --check` passed; Git emitted existing CRLF conversion notices. |

Reproduce the focused .NET check with:

```powershell
dotnet test Tests/WriterApp.Tests/WriterApp.Tests.csproj --no-restore --filter 'FullyQualifiedName~BrainstormingWorkspaceTests|FullyQualifiedName~BrainstormingDraftTests|FullyQualifiedName~BrainstormingConversationAdapterTests|FullyQualifiedName~PanelResizeHandleTests' -p:BaseOutputPath=C:/Users/Johan/source/repos/WriterApp/artifacts/brainstorming-missing-prose/build/ --logger 'trx;LogFileName=focused.trx' --results-directory artifacts/brainstorming-missing-prose -v minimal
node --test Tests/panel-resize.test.mjs
```

Browser replay requires `PLAYWRIGHT_MODULE`, `BRAINSTORMING_WEB_SEED` pointing to the
acceptance seed JSON, and `BRAINSTORMING_EVIDENCE`. Use disposable seeded data as above.
The native replay artifact is intentionally specific to this reported saved scene.
No deployment, MSIX packaging, microphone/audio, fresh live provider generation, or
new manuscript-application acceptance was claimed. All source changes remain
uncommitted alongside the existing phased work in the same checkout.
