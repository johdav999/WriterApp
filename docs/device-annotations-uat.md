# Desktop annotation flow — 2026-10-04

## Compact titles, visible color and long-passage jumps — 2026-10-04

Profile: author, local synthetic document; production Razor components and shipped editor assets in headless Edge at `http://127.0.0.1:5179/WriterApp.Client/tests/device-editor.html`; Windows Debug desktop build.

| ID | Severity | Flow | Correction and evidence | State |
| --- | --- | --- | --- | --- |
| ANN-007 | P2 | Read and activate an annotation title | The comment's first nonempty line is the title (at most 100 characters). The title is the passage link. Long comment bodies and the quoted manuscript are collapsed under details. `PanelUsesACompactTitleLinkAndKeepsTheQuoteAndCommentBodyCollapsed` verifies the production markup; workspace tests verify repeat navigation requests. | Component verified |
| ANN-008 | P1 | Jump to a long annotated passage | The previous selection-scroll behavior left the start of a long quote above the writing viewport. A new browser regression failed before the correction. Navigation now selects the exact quote, focuses synchronously and scrolls its first decoration into view. Title keyboard activation also focuses and selects the exact text without changing prose or save version. | Browser verified |
| ANN-009 | P2 | See comment/task markings in the main editor | The device bundle now supplies purple comment, yellow task and green highlight colors with an underline. A standalone-host regression verifies visible distinct colors without a web editor wrapper; existing checks verify quoted text, resolve/reopen and unchanged stored HTML. | Browser verified |

Validation: 24/24 annotation/planning tests pass using `artifacts/annotation-title-fix/AnnotationTests.csproj`, which links the repository's actual test files and production device project. The full test project is currently blocked by unrelated `SectionDto`, `PageDto` and `WriterApp.Application.State` compile errors in `WriterApp.Client/Pages/DocumentEditor.CheckedCanon.cs`. The focused project avoids changing that unrelated work. All 75 shipped-editor checks pass with no page errors, plus title keyboard activation. Final Windows Debug desktop build succeeds with zero warnings/errors.

Evidence: `artifacts/annotation-title-fix/before.json`, `browser-results.json` and `editor-colors.png`. Native UI automation could not initialize (`helper_unknown_error: apply deny-read ACLs`); the native click flow remains an acceptance check, despite the successful desktop build.

Repeat in the rebuilt desktop: select a unique paragraph, add a comment, confirm its purple marking, move/scroll elsewhere, then activate its title or **Show in text**. Confirm the start of the passage is visible and the editor has focus. Repeat with a task, resolve/reopen, and save/reopen. Quotes that are absent or repeated, and scene notes without a quote, still require an explicit **Link to selected text** action; no text location is guessed.

## Quote markup and navigation repair — 2026-10-04

Profile: Windows Debug desktop, author, existing local document inspected read-only; focused synthetic component/store tests; headless Edge with the shipped editor at `http://127.0.0.1:5179/WriterApp.Client/tests/device-editor.html`.

The reported annotation retains an older quote whose wording differs from the current scene. Its `AnchorDetached` flag is true, so it had no editor decorations or navigation link. The other reported comment has an empty quote and is a scene-level note. The quoted passage in the right panel was also a plain blockquote with no navigation callback, even for valid linked annotations.

| ID | Severity | Flow | Correction and acceptance evidence | State |
| --- | --- | --- | --- | --- |
| ANN-004 | P2 | Undo or restore the exact quoted passage | Markup and navigation revalidate the current unique quote instead of permanently excluding a saved detached flag. `RestoredUniqueQuoteIgnoresStaleDetachedFlagWithoutChangingSavedWriting` and store undo/restore cases pass. | Component/store verified |
| ANN-005 | P2 | Click quoted text in the right panel | Linked quotes now render as keyboard-accessible buttons using the existing page navigation callback. The workspace regression checks the quote link and annotation request; shipped-editor browser checks focus, selection and visible decoration. | Component/browser verified |
| ANN-006 | P2 | Recover a comment after its passage changes | Select its current passage and choose **Link to selected text**. Existing scene notes support the same action. The save preserves annotation identity, content, kind and resolution; absent, repeated and cross-page quotes are rejected. Panel/store regressions verify save and reopen. | Component/store verified |

Validation: 23/23 focused .NET tests; 73/73 shipped-editor browser checks, no browser errors; Windows Debug build succeeded with zero warnings and errors in both isolated and standard output. The actual workspace executable was closed normally through its save handler, rebuilt, and relaunched. Native clicking was not automated: the browser automation runtime failed to initialize with `helper_unknown_error: apply deny-read ACLs`, so the browser checks used the repository-compatible headless Edge runner. The signed-in native relink interaction remains a manual acceptance case.

Repeat in the updated native app:

1. Select the current passage for the detached comment and choose **Link to selected text**. Confirm immediate markup and unchanged prose.
2. Click another place in the editor, then click the quoted passage in the right panel. Confirm editor focus, scrolling and selection at the passage.
3. Resolve/reopen and save/reopen; confirm the link persists. Remove the passage, save, restore it exactly and confirm markup returns.

These current rules supersede the earlier permanent-detachment rule below. Missing and repeated quotes still have no inferred text location.

## Earlier annotation flow — 2026-10-01

The device editor now captures selected writing when the author starts a comment or task. Adding the annotation refreshes the workspace immediately, and linked comment text navigates back to the passage. The existing **Show in text** action remains available.

## Cause and correction

`LocalPlanningPanel` calls `DocumentWorkspace.CommitPanelMutationAsync` through a `Func` delegate. The save rendered the workspace before adopting the committed document, but did not render it afterward. Blazor does not automatically rerender the parent for this delegate call. The annotation could appear in the panel while `DeviceTextEditor` still had the previous annotation list.

The commit now refreshes the workspace after adopting the document and releasing the editor. This also refreshes resolve/reopen styling. The composer captures the selected quote on focus, keeps that quote stable during typing, and captures on Add as a fallback. Linked comment content is a keyboard-accessible button that uses the existing annotation navigation callback.

## Evidence and issue ledger

Profile: Prosa Windows desktop; author; local synthetic project; working-tree build. The browser adapter serves the shipped editor assets using `node WriterApp.Client/tests/serve-device-editor.mjs` at `http://127.0.0.1:5179/WriterApp.Client/tests/device-editor.html`.

| ID | Severity | Flow and acceptance | Evidence | Result |
| --- | --- | --- | --- | --- |
| ANN-001 | P1 | Save an annotation; the editor receives its markup immediately without another user action. Resolve refreshes its status. | `DeviceAnnotationWorkflowTests.SavingThroughPanelDelegateRefreshesTheWorkspaceImmediately` failed before the refresh fix and passes afterward. It renders the actual workspace, checks the editor's annotation parameter and annotation navigation request. | Verified by component regression |
| ANN-002 | P2 | Select writing, enter a comment and Add; the quote is linked automatically and remains stable while typing. | Two component cases cover focus capture and Add fallback. Browser regression moves focus to an external comment box, checks selection retention, creates markup and navigates back with editor focus and the correct selected passage. | Verified by component and browser regressions |
| ANN-003 | P2 | Linked comment text is clickable; annotation markup is visible. | Workspace rendering checks the content link. Browser checks a nontransparent comment highlight and a visual fixture shows purple comments and yellow tasks. | Verified by component and browser fixtures |

Validation:

- 19/19 focused .NET annotation/planning tests pass.
- 31/31 shipped-editor browser checks pass, including Unicode, formatting boundaries, selection focus, navigation, quote revalidation and unchanged saved HTML.
- Windows Release desktop build succeeds with zero errors using `-p:BaseOutputPath=artifacts/annotation-fix/`. NuGet reports that vulnerability metadata could not be fetched from its package feed.

## Native acceptance still to repeat

The running native app was not restarted or exercised in this pass. Load the updated desktop build and repeat:

1. Open a project scene and select a unique passage.
2. Open **Notes & Tasks → Annotations**, enter a comment and choose **Add comment** without using **Use selected text**.
3. Confirm the passage is marked immediately, click another location in the writing, then click the comment text. Confirm the editor focuses, scrolls and selects the annotated passage.
4. Resolve/reopen, save and reopen the document; confirm annotation identity and markup persist.

Existing conservative quote rules remain: unquoted scene notes have no text location; missing, repeated or previously detached quotes stay available for review without inventing a location. This pass does not validate live cloud sync or change its contracts.
