# Consistency Coach passage highlighting

The device editor used by desktop/iOS now paints the exact passage yellow after
`navigateToConsistency` validates the current page text and anchor. This also
covers jumps to a finding's comparison passage. The highlight survives focus
returning to the coach and unchanged-content refreshes. Another jump replaces
it; failed targeting, document edits, page replacement, discarding a suggestion,
marking a finding intentional, and changing context tabs clear it.

The color is a ProseMirror decoration, so it never enters saved HTML, creates
an edit callback, or becomes an undoable formatting change. Unchanged external
content is compared as a schema document before resetting the editor. The web
client clears consistency decorations on discard, marking intentional, or a
context-tab change. A check finishing after leaving Consistency Coach cannot
repaint highlights in another tab.

## Highlight cleanup verification (2026-10-05)

- Both shipped editor bundles built successfully.
- 26 focused .NET tests passed, including the decline callbacks, null-passage
  reset and repeat jump, web discard, and web category/subview navigation.
- The isolated desktop build passed with zero warnings/errors at
  `artifacts/consistency-cleanup-desktop/`.
- The shipped device editor browser harness passed the new explicit-clear check:
  markup stays removed after unchanged-content and annotation refreshes; text,
  content version, selection, and save callbacks remain unchanged; a later jump
  can highlight again. The harness reported 81 passes out of 82 checks, zero
  browser errors, and one unrelated word-completion punctuation assertion
  (`stubbornly` versus `stubbornly,`). Full harness acceptance is therefore open.
- Browser evidence: `artifacts/consistency-cleanup/browser-results.json` and
  `artifacts/consistency-cleanup/passage-highlight.png`.
- Interactive native desktop acceptance is still unverified. Restart the desktop
  app to load the rebuilt assets, jump to a finding, then discard it or switch
  category/subview and confirm the yellow passage returns to normal.

## Verification (2026-10-04)

- `npm run build` in `WriterApp.Client`: passed for both shipped editor bundles.
- `tests/consistency-highlight.mjs` against the allowlisted local harness:
  78 editor checks passed in headless Edge, with no browser errors. Checks cover
  repeated marked phrases, paragraph breaks, coach focus, annotation refreshes,
  unchanged content, stale/invalid anchors, replacement, editing, undo, and
  saved HTML/version/callback stability.
- Evidence: `artifacts/consistency-highlight/browser-results.json` and
  `artifacts/consistency-highlight/passage-highlight.png`.
- Isolated desktop build:
  `dotnet build WriterApp.Desktop/WriterApp.Desktop.csproj --no-restore -p:BaseOutputPath=C:/Users/Johan/source/repos/WriterApp/artifacts/consistency-highlight-desktop/`.
  The initial build passed with no warnings/errors. The final rebuild after
  the unchanged-content fix failed with MSB3936 because C: ran out of space
  while extracting Windows runtime `resources.pri`. This task's temporary
  desktop output was removed, recovering about 27 MB. Final native build
  verification needs additional disk space.
- Native desktop interaction remains unverified. The browser and native UI
  tools failed to initialize with the sandbox's `apply deny-read ACLs` error;
  browser checks used the bundled Playwright runtime instead.

To repeat browser verification, build the assets, start
`node tests/serve-device-editor.mjs`, then run
`node tests/consistency-highlight.mjs` from `WriterApp.Client`. Set
`PLAYWRIGHT_MODULE` to the installed Playwright path if it is not on the local
module search path, and `EDITOR_TEST_HOST` if using a port other than 5179.

For native acceptance, open a rebuilt desktop app, jump to a finding and its
comparison passage, then return focus to the coach. Confirm the yellow range
remains visible and clears after editing or navigating to another page.
