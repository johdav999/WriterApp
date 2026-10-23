# Android Navigator touch dragging — 2026-10-12

The device project outline used only HTML dragstart/dragover/drop events. Android touch input did not start that HTML drag sequence, so the existing DropOutlineAsync move and persistence callback was never reached. The explicit draggable="true" fix for mouse dragging did not supply a touch gesture implementation.

Each project outline row now has a grip with touch-action: none. Pointer capture tracks dragging from that grip, coordinate hit testing chooses the destination, and existing before/after/inside indicators explain placement. Ordinary row swipes remain available for scrolling. Edge scrolling, cancellation, capture loss, Escape, blur, disabled state and single-flight saving are handled. Touch and mouse grip moves call the same existing DropOutlineAsync validation and repository save; native mouse dragging from the row remains available. Both the project workspace and embedded editor Navigator use this component.

Verification completed:

- Nine JavaScript tests passed, including native drag regression, captured touch reorder/reparent, cancellation, disabled state, duplicate-save prevention and click suppression.
- Ten focused .NET tests passed, including project and embedded row markup, project drop resolution and structure behavior.
- Debug android-x64 build succeeded with zero warnings/errors using isolated outputs in artifacts/android-navigator-build. The initial normal-output build encountered locked packaging files; the isolated build avoided that collision.
- Installed the rebuilt APK over the existing emulator app with adb install -r. Existing document hashes matched before and after installation and acceptance.
- Real Input.dispatchTouchEvent gestures in the installed Android WebView reordered scenes, moved a scene into another chapter, and moved a scene through the embedded Navigator. Saved structure survived reopening the project route.
- Returned to the original manuscript and opened Navigator. Visually inspected the grips and row layout. Synthetic local test projects were used for mutations; existing writing was not rearranged or changed.

The repeatable native check is Tests/android-navigator-touch.mjs: run baseline before installing the build, then run without an argument after installation. It creates a new blank local test project and retains it for inspection. Earlier development retries also retained their blank local projects.

Evidence: artifacts/android-navigator-touch/results.json; artifacts/android-uat/navigator-touch-project.png; artifacts/android-uat/navigator-touch-embedded.png; artifacts/android-uat/navigator-touch-original.png. Build and focused test logs are artifacts/android-navigator-build.log and artifacts/android-navigator-tests.log. Physical phone acceptance remains unverified.
