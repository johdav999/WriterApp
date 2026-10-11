# Brainstorm follow-up proposal refresh

2026-10-09. Keep this work in the existing WriterApp checkout.

The reported patrol follow-up completed successfully. Read-only inspection of the
matching desktop conversation found two completed Draft requests, containing 1,844
and 1,281 characters of prose. The latest 1,281-character draft contains the patrol,
sergeant and research scene and is the selected candidate. No generation is pending.
The original conversation was only read; no provider request or cache repair was made.

The shared proposal component allowed concurrent asynchronous editor creation while
`_editor` was still null. A subsequent render or candidate change could mount another
instance. Cleanup of the older instance called the shipped editor's destroy function,
which clears its entire host element. Both instances used the same host, so late
cleanup could remove the new proposal's visible DOM while its prose remained saved.

The correction allows one creation at a time, captures and checks the candidate epoch
before creating an editor, and schedules the current candidate after stale work ends.
Each candidate has a keyed Razor host; each JavaScript editor owns a separate mount
inside it. Destruction removes only that instance's mount. Detached/stale cleanup
cannot erase the newer draft. Version changes also guard against late parameter work.

| ID | Severity | Flow | Acceptance | Status |
|---|---|---|---|---|
| FOLLOWUP-001 | P1 | Complete a prose follow-up while proposal renders overlap | Latest saved candidate remains visible; older cleanup cannot clear it | Verified with component lifecycle tests and both shipped editor factories |

Evidence is under `artifacts/brainstorming-follow-up`, with adjacent build/test logs.
The baseline component test failed for both overlapping-render cases. The baseline
browser reproduction failed with empty visible prose after old cleanup. After repair:

- 63 focused draft, conversation-adapter and workspace regressions passed.
- The lifecycle browser regression retained the follow-up prose with exactly one
  textbox and toolbar in both Web and Device factories; final cleanup left no nodes.
- Existing two-factory proposal browser checks passed for rich formatting, editing,
  selection, read-only controls, responsive layout and IndexedDB persistence.
- The desktop Debug build passed in isolated outputs. The test build also compiles
  the server and web client.

These checks use fictional prose and synthetic provider/interop fixtures. The
author's running desktop was not restarted, and no fresh native interactive
acceptance or live provider generation is claimed. Rebuild/relaunch the normal
desktop application to load the changed shared component. Reopen the saved
conversation to display its existing follow-up; it does not need regeneration.
