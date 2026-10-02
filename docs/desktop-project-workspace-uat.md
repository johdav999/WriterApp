# Desktop project workspace UAT — 2026-09-29

The client project page and the Windows desktop page now use the same scoped project layout, panel frame, coach presentation, and design tokens. The desktop persists all structure operations through `LocalDocumentRepository`; document conversion and cloud synchronization remain available in compact sections. Cover and Progress are disabled with explanatory titles on desktop, where those workflows are not implemented. Storyboard opens the existing local planning workspace.

Verification:

- The regular desktop Debug app was rebuilt and reopened through its save-on-close path. Its project screen displayed the shared two-column layout, default local chapter and scene, coach, and selection Inspector. The isolated preview used a separate validation data directory and confirmed inline rename focus and save with Enter, then added a `New Part` through the Navigator and showed its Inspector without touching ordinary local manuscripts.
- The client project route rendered the shared two-column grid: 420px Navigator and flexible Inspector at a 1280px viewport. At 800px, both panels stacked at 581px with no horizontal overflow.
- `dotnet build WriterApp.Desktop/WriterApp.Desktop.csproj --no-restore -warnaserror` passed with zero warnings or errors. `dotnet build BlazorApp.csproj --no-restore -warnaserror` passed with zero warnings or errors.
- 37 `LocalProjectTests` and `EditorPanelPresentationTests` passed, including mixed-format project word totals and escaped coach content.

The final breadcrumb label adjustment was verified by a zero-warning isolated desktop build. The regular app was in use when that last build was made, so it will pick up the `Projects` breadcrumb on its next restart.

Evidence: [desktop preview](../artifacts/uat/project-parity/desktop-project.png) and [client](../artifacts/uat/project-parity/client-project.png). The desktop preview image shows isolated synthetic test data. The regular desktop app was reopened on the existing local project.
