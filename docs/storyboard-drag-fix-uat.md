# Storyboard native drag fix — 2026-10-02

The shared storyboard scene slot and its drag handle passed Boolean values to HTML's enumerated `draggable` attribute. Razor rendered enabled elements as bare `draggable`, giving them an empty attribute value. Ordinary div/span elements remain non-draggable with that value, so mouse movement never starts `OnSceneDragStart` and the existing move/persistence path receives no scene.

`WriterApp.UI.Shared/Projects/StoryboardBoard.razor` and `SceneCard.razor` now render explicit `true`/`false` strings. The board also disables the card's drag handle while a move is in flight, matching the slot. Both the web client and desktop use these shared components.

Verification:

- The rendered-board regression test failed before the change: both scene slots had an empty `draggable` value. It passes after the change and verifies both slots and handles; disabled cards omit the handle.
- All 25 focused .NET tests passed across `LocalStoryboardTests`, `LocalProjectDropTests`, and `ProjectOutlinePresentationTests`, covering local persistence, atomic cross-chapter movement, manuscript order, writing preservation, and native drag markup.
- Headless Chrome used real mouse drags against the actual desktop storyboard page hosted as an interactive Blazor component with isolated synthetic data. Restoring the bare attributes reproduced the failure without a write. With the fix, dragging by the handle reordered to the first gap, moved into an empty chapter, and reordered to the last gap. Dragging by the card body inserted into an occupied chapter. Reload retained the final order and page identities/content were unchanged. No browser JavaScript errors occurred.
- Windows desktop Debug build succeeded with zero errors. NuGet vulnerability lookup emitted existing `NU1900` warnings because its service was unavailable. The test build also reported an existing xUnit analyzer warning in `LocalStoryboardTests`.

Browser evidence and its repeatable fixture are in `artifacts/storyboard-drag-host/`, including `browser-result.json`, `check-drag.cjs`, and `drag-after-reload.png`. The browser fixture uses its own `data/` directory and does not touch the user's library. Its persisted fixture must be fresh to rerun the exact sequence.

Both the normal desktop output in `WriterApp.Desktop/bin/Debug/net10.0-windows10.0.19041.0/win-x64/` and the isolated output in `WriterApp.Desktop/artifacts/storyboard-drag-fix/bin/Debug/net10.0-windows10.0.19041.0/win-x64/` were rebuilt successfully. A running desktop app must be relaunched to load rebuilt assemblies. Native Windows WebView mouse acceptance remains unverified separately from the browser checks.
