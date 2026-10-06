# Writing tools action flow

Implemented 2026-10-04 in the shared device panel used by desktop and iOS.

Choose the writing target, select an action, adjust applicable settings, and use the single preview button. Choosing an action or saved preset does not generate writing. The existing review, explicit Apply, history, and undo flow handles the proposal.

- Rewrite: tone, length, and preserve terms; selected passage only, matching the supported backend contract.
- Expand, Shorten, Show, don't tell: dedicated actions without unrelated settings.
- Change tone: tone only.
- Current section: four supported actions; switching from passage Rewrite selects Expand.
- Next paragraph: retains its existing continuation controls and preview flow.

The built-in Rewrite preset dropdown and misleading Fix grammar entry are removed. Save as preset stores the selected action, target, and relevant settings in the existing local prompt store. Saved combinations populate the same controls and preview flow, retaining the exact preset definition in reviewed history. Imported presets respect the current account/backend and project. Custom templates and presets with free-form tones remain available through Prompt Library; this selector only exposes presets whose settings it can represent.

## Verification

- 100 tests passed across LocalWritingPanelTests, LocalWritingTests, and LocalPromptPanelTests, including selection without generation, dedicated preview routing, persistence, apply/undo, account boundaries, and settings visibility.
- Desktop Debug build succeeded with zero warnings or errors using an isolated BaseOutputPath.
- Tests/writing-tools-layout.mjs passed 24 layout checks at 320, 420, and 680 pixels using actual Razor markup and production scoped CSS in headless Edge.
- Rendered markup, screenshots, and layout-results.json are under artifacts/writing-tools/evidence. These use a synthetic provider. Native desktop relaunch and live-provider acceptance were not performed.

To rerun the checks from the repository root in PowerShell:

```powershell
New-Item -ItemType Directory -Force artifacts/writing-tools/evidence | Out-Null
$env:WRITERAPP_WRITING_TOOLS_EVIDENCE = Join-Path (Get-Location) 'artifacts/writing-tools/evidence'
$writingToolsOutput = Join-Path (Get-Location) 'artifacts/writing-tools'
dotnet test Tests/WriterApp.Tests/WriterApp.Tests.csproj --filter 'FullyQualifiedName~LocalWritingPanelTests|FullyQualifiedName~LocalWritingTests|FullyQualifiedName~LocalPromptPanelTests' "-p:BaseOutputPath=$writingToolsOutput/build/"
# Set PLAYWRIGHT_MODULE to an installed Playwright module if it is not locally resolvable.
node Tests/writing-tools-layout.mjs
dotnet build WriterApp.Desktop/WriterApp.Desktop.csproj "-p:BaseOutputPath=$writingToolsOutput/desktop-build/" --no-restore
```

All changes remain uncommitted in this checkout alongside the existing work.
