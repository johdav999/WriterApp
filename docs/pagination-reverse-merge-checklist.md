# Pagination Reverse-Merge Manual Checklist

1. Open a document in real pagination/print mode.
2. Type `Enter` near a page boundary until a paragraph moves from page `N` to page `N+1`.
3. Place the caret at the very start of the moved paragraph on page `N+1`.
4. Press `Backspace` once.
5. Verify the paragraph moves back to page `N` when it fits.
6. Repeat with additional small deletions so content shrinks incrementally.
7. Verify no repeated `spacer layout did not converge` spam for this scenario.
8. Verify no repeated `run halted maxPasses` spam while holding `Backspace`.
9. Verify regular typing in mid-page still paginates normally.
10. Verify zoom/resize still triggers one stable recompute.
