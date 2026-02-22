# UX Fixes Manual Checklist

1. Project rename in Projects page
- Open `/projects/{projectId}`.
- Click `Edit name`, change project title, click `Save`.
- Confirm title updates immediately in page header area and project switcher.
- Reload page and confirm title persists.
- Trigger a failing rename (network fail) and confirm status message shows and title reverts.

2. Editor keyboard shortcuts
- In editor, verify:
  - `Ctrl+B` toggles bold.
  - `Ctrl+I` toggles italic.
  - `Ctrl+K` opens link prompt.
  - `Ctrl+Z` undo, `Ctrl+Y` redo, `Ctrl+Shift+Z` redo.
  - `Ctrl+Shift+X` toggles strike-through.
- Verify shortcuts do not trigger formatting outside editor focus.

3. Tooltips coverage
- Hover main editor buttons/tabs (toolbar, document menu actions, right-panel tabs).
- Confirm tooltip is present and meaningful for all visible buttons/tabs.

4. Link interactions
- Insert a link in editor content.
- `Ctrl + left click` opens in a new tab.
- Right-click link opens link context menu with `Open link`, `Edit link`, `Remove link`.
- `Edit link` updates href, `Remove link` removes link mark.
- Press `Esc` to close menu.

5. Image insert UI
- Verify no visible `Choose File` input/button next to image action.
- Click `Image`, choose a file, confirm image inserts and upload flow still works.

6. Pagination and zoom
- With print layout enabled, zoom browser in/out (`Ctrl +` / `Ctrl -`).
- Confirm pagination reflows after a short delay and page boundaries remain correct.
- Resize browser window and confirm pagination stays aligned.

7. Document menu page-break wording
- Open document dropdown.
- Confirm option text `Switch to simple page breaks` is not present.

8. Export content selector
- Open export dialog.
- Confirm `Content` selector has `Document` and `Synopsis`.
- Export `Document` and `Synopsis` successfully from same dialog.
- Confirm `Document` remains default selection when opening export dialog.

9. Coach context awareness by right-panel tab
- Switch right-panel top tabs: `Coach`, `Story`, `Navigator`, `Notes & Tasks`, `History`, `Advanced`.
- In `Coach`, switch sub-tabs: `Writing tools`, `Consistency`, `Style & quality`.
- Confirm coach card text updates immediately to match active tab/sub-tab context.
- In non-scene routes/views, confirm coach card does not mention `Scene` and no scene-specific recommendation appears.
- In scene route/view, confirm scene-focused recommendation can appear when scene card fields are sparse.

10. AI tools command visibility and descriptions
- Open `Coach` -> `Writing tools` (`AI tools` panel).
- Confirm these commands are not shown: `Tighten selection`, `Change tone (selection)`, `Tighten section`, `Change tone (section)`.
- Confirm AI command buttons show concise labels only (no per-command description blocks under each button).
- Confirm layout has no extra empty spacing and keyboard tab order remains sequential.

11. AI proposal panes scrolling
- Trigger an AI proposal with long `Original` and `Proposed` text.
- Confirm `Original` and `Proposed` panes each scroll independently.
- Confirm pane labels remain visible while scrolling (sticky where supported).
- Confirm layout remains usable on smaller viewport widths and at browser zoom in/out.

12. Pagination convergence / no thrash
- Open a long document with print layout enabled and type continuously for 20-30 seconds.
- Confirm pagination logs do not show unbounded pass growth and there is no continuous rerender loop.
- Confirm typing remains responsive and CPU usage settles after brief recalculation bursts.
- Resize window and change browser zoom (`Ctrl +`, `Ctrl -`); confirm a single coalesced recalculation occurs and page breaks remain visible.
- Confirm `setPageBreaksEnabled` is not repeatedly re-applied during normal typing when mode/options are unchanged.

13. Writing tab naming
- Confirm right-panel top tab label shows `Writing` (not `Coach`).
- Confirm sub-tab labels under Writing are `Writing tools`, `Consistency`, `Style & quality`.
- Confirm headers in these sections do not use `Coach` wording (e.g., `Writing tools`, `Consistency`, `Style & quality`).

14. Change details scrolling
- Open AI proposal details (`Change details`) with long text in Before/After blocks.
- Confirm details container and long text regions show scrollbars only when content exceeds viewport-constrained height.
- Confirm surrounding dialog layout remains stable and actionable buttons stay reachable.

15. Writing tools AI suggestion height
- Open Writing -> Writing tools and trigger an AI suggestion.
- Confirm the AI suggestion panel is visibly taller (about double previous desktop height).
- Confirm long Original/Proposed content scrolls inside the AI suggestion panel.

16. Top toolbar file input visibility
- Verify the editor top toolbar has no visible native `Choose File` control and no `No file chosen` text.
- Click `Image` and confirm the file picker opens and image insertion/upload still works.

17. DOCX export TOC
- Export a document with headings to DOCX with `Include TOC` enabled.
- Open in Word and confirm TOC field appears near the top of the document.
- Run `Update field` in Word if needed and confirm heading entries populate.

18. Right-panel descriptive tooltips
- Hover Writing top tab, Writing subtabs, and right-panel action buttons.
- Confirm tooltips describe action intent (verb + outcome), not just label text.

19. User feedback flow
- Click `User Feedback` in top editor actions.
- Submit Bug and Enhancement examples with required fields; confirm validation blocks empty Title/Description.
- Confirm successful submit closes dialog and shows `Thanks—feedback sent.` banner.
- Confirm failed submit path shows retryable error message.

20. Preview synopsis
- Open Export dialog and set `Content` to `Synopsis`.
- Click `Preview Synopsis` and confirm the preview opens quickly.
- Confirm preview content reflects synopsis text from current document.
- Close and reopen preview; confirm second open is fast (session cache).

21. Export synopsis DOCX
- Open Export dialog and set `Content` to `Synopsis`, `Format` to `DOCX`.
- Confirm export tooltip describes downloading synopsis as Word.
- Export and verify filename follows `...-Synopsis.docx`.
- Open in Word and verify title + `Synopsis` heading and synopsis content paragraphs.

22. Existing document export unchanged
- Set `Content` to `Document`.
- Export `DOCX`, `HTML`, and `Markdown`; ensure each still succeeds.
- Open `Preview` and confirm document preview behavior is unchanged.
