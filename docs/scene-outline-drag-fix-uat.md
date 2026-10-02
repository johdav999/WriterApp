# Scene outline native drag fix — 2026-10-01

The desktop project outline and embedded scene navigator passed a Boolean to the HTML `draggable` attribute. Razor rendered enabled rows with a bare `draggable` attribute. This attribute requires the string `true`; the bare attribute leaves ordinary div rows non-draggable, so mouse movement selects text and never starts the outline's native drag handler.

`WriterApp.Device.Shared/Pages/Projects.razor` now supplies explicit `true` and `false` strings. Move resolution and local persistence are unchanged.

Verification:

- A rendered-component regression test reproduced the bare attribute before the fix. Both the full project page and embedded navigator now render every enabled row with `draggable="true"`.
- 27 focused .NET tests passed, covering rendered outline markup, move resolution, manuscript order, writing preservation, and saved project structure. All 6 JavaScript drag-handler tests passed.
- A headless Chrome session used real mouse gestures against the interactive Blazor component with isolated synthetic local data. Reintroducing the bare attribute made the DOM `draggable` property false and prevented writes. With the fix, scene reorder, cross-chapter movement, chapter movement into a part, movement back to the root, and embedded navigator reorder all persisted. Reload retained the final scene order. No browser JavaScript errors occurred.
- Windows desktop Debug build succeeded with zero errors. NuGet vulnerability lookup produced `NU1900` because its service was unavailable; the focused test build also reported an existing nullable warning in `AdvancedDeviceAiTests.cs`.

Browser evidence is in `artifacts/outline-fix-host/browser-result.json` and `artifacts/outline-fix-host/drag-after-reload.png`. The isolated desktop build is in `WriterApp.Desktop/artifacts/outline-fix-desktop/bin/`. The existing running desktop process was left open and still uses its previous assemblies; close it and launch the rebuilt executable to load this fix. Native WebView mouse acceptance remains unverified separately from the browser validation.
