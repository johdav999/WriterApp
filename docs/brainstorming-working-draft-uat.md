# Continuous working draft — 2026-10-09

## Delivered behavior

Brainstorm has one continuous working draft. Every completed prose generation is
an immutable saved version; normal follow-ups do not create tabs. For continuations,
the model returns new paragraphs and an explicit continuation flag. The server
appends escaped prose to the exact reviewed draft, retaining its rich formatting.
Rewrites return a complete revised draft. This is semantic model intent, not a
hard-coded list of author phrases. Manual edits are retained and checkpointed
before sending, switching alternatives, or restoring.

The shared web/desktop interface follows the reference: a central draft, optional
outline, conversation alongside it, named alternative tabs, visible Version history,
paragraph change highlighting, and Review & keep. Preview / compare leaves the
draft intact. Restore creates another version; later versions remain available.
+ Alternative creates a named copy; subsequent generation stays on that branch.
Applying prose still uses exact review/approval/Keep; no Keep is needed between
draft turns. Highlighting is paragraph-level, not a character-level redline.

Old drafts without branch metadata appear under Main draft. Optional fields are
omitted when null/default, preserving legacy fingerprints. Branches travel through
existing local storage and synchronization. No migration is required. Generated
rich snapshots must be the exact previous HTML plus escaped new prose; arbitrary
provider HTML is not accepted. Existing manual snapshot compatibility remains.

## Verification

- 857 combined regressions passed, zero failures/skips. New tests cover production
  server generation with a synthetic provider, rich continuation, alternative
  generation, restore, immutable history, reopen, sync and mounted Razor controls.
- Release server/client and Debug desktop builds passed with zero warnings/errors.
- Ten browser suites passed. Draft tests additionally verify highlight toggling
  without altering saved HTML in both shipped editor factories.
- The rebuilt app passed `Tests/brainstorming-focused.mjs` on disposable SQLite
  data: alternative editing/switching, history preview/restore, 1600/900/390px
  layouts, retained composer, rich editing, long-draft scrolling, exact Keep
  preview/cancellation. Original manuscript unchanged; zero generation requests,
  browser errors or failed stylesheets.
- The two-factory proposal lifecycle suite passed. All 209 initial checkout status
  entries remain. No author cache was edited or user process stopped.

Evidence: `artifacts/brainstorming-p12-working-draft/` contains the combined TRX,
build logs and ten browser reports. `artifacts/brainstorming-working-draft/` contains
actual-app browser results/screenshots, lifecycle log, normal-data desktop build
log and final verification receipt. The initial app check raced asynchronous
context preparation; the runner now waits before opening History. Its failure
capture is superseded by `browser/result.json`.

![Continuous draft](../artifacts/brainstorming-working-draft/browser/continuous-working-draft.png)

![Version preview](../artifacts/brainstorming-working-draft/browser/working-draft-history.png)

Native interaction and live model compliance remain acceptance gates. Automated
synthetic-provider testing verifies the protocol and persistence. Broader P12
audio/account/package gates remain separate. No live provider request was sent.

## Run and handoff

Both backend and desktop changed. Earlier instructions allowing the old backend
to remain running are superseded. No author window was replaced. The disposable
test server PID 36484 on 5399 was stopped; no agent listener remains from this task.

Save work and close the old Prosa desktop and backend terminal. From this checkout,
start the backend normally so it rebuilds without locked files:

```powershell
dotnet run --project BlazorApp.csproj
```

Then launch the verified separate desktop build, which uses normal Prosa data:

```powershell
& '.\WriterApp.Desktop\artifacts\brainstorming-working-draft\user-build\Debug\net10.0-windows10.0.19041.0\win-x64\WriterApp.Desktop.exe'
```

Do not use the `native-build` validation executable for author work: it uses an
isolated data directory. A configured remote backend also needs the server update.

Continue in this same checkout with unrelated uncommitted work intact. Preserve
identity/recovery, SQL retry/idempotency, second-send readiness, editor lifecycle
and Keep guards. Never replay retained messages or uncertain operations. Rerun
`scripts/verify-brainstorming.ps1 -Phase p12 -EvidenceSuffix <new-name> -Browser
-PlaywrightModule <installed-module>` for automated verification. Next native
acceptance: initial draft, two continuations without Keep, alternative generation,
preview/restore, then keep the assembled draft once. Confirm versions and
alternatives reopen after restart.
