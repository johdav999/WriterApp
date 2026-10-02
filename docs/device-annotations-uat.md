# Desktop annotation flow — 2026-10-01

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
