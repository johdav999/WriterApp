# Shared tools in the normal Prosa editor

Local integration status, 2026-10-18. This is not a production or packaged-client acceptance record.

## Implemented paths

- Normal toolbar edits continue through the shared rich-text document and its durable journal.
- Selection/page AI Apply calculates its candidate in a detached editor. The candidate does not enter the shared document before approval. The server checks the captured manuscript, membership, exact original page, and prepared actor-owned history intent in one transaction.
- Page replacement retires the previous generation, retains its writing, revokes its write leases, and creates a new generation. Connected editors reopen a clean committed generation; pending local work is retained rather than replayed onto a different generation.
- AI Undo/Redo uses committed actor-owned receipts and exact current-content checks. A later collaborator edit prevents an older replacement from overwriting it.
- Whole-section writing actions use committed page projections and the current manuscript outline. All affected page generations and the aggregate history receipt commit together. Scoped Undo/Redo checks every affected page and its structure before changing any page.
- History lists retained shared generations and earlier legacy page versions. Compare and whole-page Restore use the normal History panel.
- Normal section controls create, rename, duplicate, reorder, and delete sections, and create pages. Deleted sections retain their underlying writing. Duplication copies current projected writing and section notes. Navigation refreshes in connected editors.
- Shared section notes use a checked original-value save. A conflicting other-account edit is refused, and note changes invalidate older AI sources. Viewers cannot save notes.
- Image uploads authorize the current editor and use the manuscript owner's asset folder, so both accounts resolve the same image. Viewers can read images but cannot upload them; revoked accounts cannot retrieve them.
- Deleting the section another collaborator has open retains its journal and moves that editor to a remaining section without signing the account out. A failed checked preview reopens a changed generation before returning to writing.
- Sharing returns to the section from which it was opened, including after section reordering. Invitation acceptance uses the authenticated inbox; no manual invitation ID/code is needed.

## Boundaries still requiring integration

Whole-section/document translation and translated copies, private original-copy recovery from a shared manuscript, planning/scene-card/canon/synopsis mutations and their AI approval paths, project-tree mutations, import, and anchored annotation parity are not covered by these shared commands. Existing storage fences remain active. The presence of the normal editor surface is not a claim that every tool has been converted.

## Verification

`CollaborationCheckedEditorTests` covers owner/editor replacement, immutable/idempotent receipts, stale sources, Viewer/revoked/foreign denial, retained page versions, shared notes conflicts, structure changes, aggregate application/recovery, and the HTTP section approval/commit/Undo flow.

`normal-ui-browser-probe.mjs` uses actual normal Prosa components with two isolated browser contexts, Kestrel/SQLite and the Node collaboration service. Authentication and provider generation are deterministic test fixtures. Source validation, approval, commit, history and recovery use production implementations. It exercises email invitation acceptance, live edits, formatting, page isolation, page AI Apply/Undo, section AI Apply/scoped Undo, whole-page Restore, section organization, Viewer live updates, and revocation without sign-out.

Current-run logs are under `artifacts/shared-tools-*`. The browser probe writes `artifacts/realtime-full/browser.json` and screenshots on success. Real Gmail/Hotmail account verification requires those users to finish sign-in in the opened isolated profiles. Synthetic-account passes must not be reported as real-account acceptance. SQL Server migrations and packaged Windows/Android/iOS behavior require their own runs.

Latest local results: 89 regression checks passed with one optional performance check skipped; the final browser flow, interrupted aggregate rollback, and shared image permissions checks all passed (3/3). Node codec/protocol checks passed (22/22). The final browser run also deleted the section open in the guest browser and verified navigation to the surviving section with the account still signed in. The local application was rebuilt and restarted on port 5398 with the retained test database.

## Manual two-account check

1. Open `/app/collaboration` in the two isolated signed-in profiles and verify the email in each profile.
2. Open the same shared manuscript with Editor access. Type from each side and check the other side, including after refresh.
3. Select text, generate a suggestion, and confirm the other browser does not see the proposal before Apply. Apply, then use AI Undo/Redo.
4. Generate a whole-section writing proposal and Apply. Check every page in both browsers, then use that operation's scoped Undo/Redo.
5. Open History, compare a retained version, and Restore. Both browsers should receive the restored page.
6. Create, rename, duplicate, move and delete a test section. Check navigation in the other browser. Keep one section in the manuscript.
7. Change the collaborator to Viewer, then revoke access. Writing must stop while the Prosa account remains signed in.
