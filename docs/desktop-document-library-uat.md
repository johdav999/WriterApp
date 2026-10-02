# Desktop document library reference implementation — 2026-10-01

The desktop Documents landing page follows the approved reference: a 256px full-height sidebar, the existing Prosa logo and warm design tokens, a wide document list with manuscript thumbnails and aligned metadata, search and sorting, quiet storage status, row action menus, and an import area below the documents.

The library uses an optional sidebar composition in the shared `AppShell`. Other routes retain their existing shell and editor layout. Library actions continue to use `LocalDocumentLibrary`, native file selection, and `DocumentSyncStatus`; recovery notices, storage errors, trash/restore, guarded permanent deletion, and cloud enrollment remain available. Dropped files use the same format validation and document creation pipeline as picker imports. Cloud journal conflicts, errors, and deleted copies remain visible, with detailed controls in each row menu.

## Profile and evidence

- Product: Prosa Windows MAUI/Blazor desktop; author role.
- Normal build: `dotnet build WriterApp.Desktop/WriterApp.Desktop.csproj -c Debug -p:TreatWarningsAsErrors=true --no-restore`.
- Isolated visual build: `BaseOutputPath=artifacts/library-uat/`, `ProsaValidationDataDirectory=<absolute workspace>/artifacts/uat/library-reference/offline-data`, Development environment, unreachable local API for offline checks. Sample documents use the reference titles, section counts, and dates. They are separate from normal app storage.
- Native visual comparison: 1586×1016 window, compared with the 1586×992 generated reference; a larger 2566×1016 window and maximized display were also inspected. The 24px height difference is window chrome/size, not a claim of identical image pixels.
- Ignored evidence: `artifacts/uat/library-reference/desktop-sorting.jpg` and `desktop-sync-error.jpg`. The latter shows the deliberate offline backend failure, preserved local documents, and a single scrolling document pane.

## Checks

| ID | Flow / acceptance | Result |
|---|---|---|
| LIB-001 | Reference proportions, palette, typography, logo, rows, thumbnails, tabs, import area | Native comparison passed at reference width |
| LIB-002 | Search `Test` shows the two matching documents and `2 documents of 4`; clearing restores all four | Native passed |
| LIB-003 | Oldest edited sorts dates from May through September to October | Native passed; sorting capture saved |
| LIB-004 | Ellipsis opens Rename, Duplicate, Move to trash, and Cloud sync details; Rename focuses the title; Escape cancels with the document intact | Native passed |
| LIB-005 | Existing documents appear after updating and reopening the normal desktop executable | Native passed |
| LIB-006 | Drop rejects multiple files and files over 5 MB; a valid file triggers one import event; nested drag highlighting and disposal work | Three Node tests passed |
| LIB-007 | Repository storage, import validation, sync engine, and workspace view regression coverage | 96 existing focused .NET tests passed |

Builds succeed with zero compilation errors. NuGet reports `NU1900` because its vulnerability feed cannot be reached; this is not a verified vulnerability audit.

The final source builds in both the normal desktop output and `artifacts/library-reference/` with normal app storage/configuration. Building into the normal output while Prosa is running can fail with locked DLLs; close the app through its save-and-close path before rebuilding that output. The final normal executable was reopened with the user's existing documents during this pass.

## Remaining acceptance

Native Explorer-to-WebView file dropping and picker import were not repeated in this pass. Native permanent deletion, restore persistence, live cloud synchronization, and high-DPI/phone-sized acceptance were not claimed from the automated checks. Responsive layouts are implemented for smaller windows, but the exact reference-size comparison is a close visual match, not a pixel-difference certification.
