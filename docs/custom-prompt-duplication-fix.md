# Custom prompt section duplication

Fixed 2026-10-04 in the shared device workspace used by desktop and iOS.

The custom prompt button previously called `DeviceAiRequests.Build`. With no selection, this sent the entire section to `custom_transform`, whose response is complete revised text, but assigned `ApplyMode = "append"`. Apply then inserted that whole response after the active page's original text. This was a request/apply contract mismatch.

The workspace now routes custom prompts to `LocalWritingPanel.RunCustomAsync`. It captures the current selection, chooses passage replacement or complete section revision, and retains the instruction as an unsaved prompt definition. Section requests capture every page using the existing page/run mapping; reviewed results replace those pages through the existing atomic save and history flow. Selected passages use the existing validated range replacement. The old request builder refuses unmapped custom section requests, and the legacy apply guard refuses custom append proposals. The equivalent web section path already uses checked replacement and needed no change.

Verification: 146 focused tests passed, including the actual workspace handler, complete source plus added prose without duplication, selection/section targeting, unaffected writing, undo/redo after reopening history, incomplete provider results, unsupported backend capability, stale proposals, prompt library, and web/server structured custom contracts. The desktop Debug build succeeded with zero warnings or errors.

Outputs are isolated under `artifacts/custom-prompt-fix/build` and `artifacts/custom-prompt-fix/desktop-build`. The initial test invocation hit running backend DLL locks; the isolated invocation passed. The running backend and desktop were left open. Native relaunch and live-provider acceptance have not been performed.

```powershell
dotnet test Tests/WriterApp.Tests/WriterApp.Tests.csproj --no-restore '-p:BaseOutputPath=C:\Users\Johan\source\repos\WriterApp\artifacts\custom-prompt-fix\build\' --filter 'FullyQualifiedName~LocalWritingPanelTests|FullyQualifiedName~LocalWritingTests|FullyQualifiedName~DeviceAiActionTests|FullyQualifiedName~LocalPromptPanelTests|FullyQualifiedName~ActualWebCheckedSectionWritingCommitsAllMappedPagesAndReopensRecovery|FullyQualifiedName~ReusablePresetExecutionRequiresDeclaredCompleteOwnedTargetAndCurrentRevision'
dotnet build WriterApp.Desktop/WriterApp.Desktop.csproj --no-restore '-p:BaseOutputPath=C:\Users\Johan\source\repos\WriterApp\artifacts\custom-prompt-fix\desktop-build\'
```

Changes remain uncommitted alongside the existing work in this checkout. For native acceptance, open the rebuilt desktop app, run a custom prompt without selecting text in a section with multiple pages, review every page, apply, and confirm the original prose appears once with the requested addition. Repeat with a selected passage, then undo and redo in History.
