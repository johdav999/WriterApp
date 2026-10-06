# Storyboard native drag fix — 2026-10-02

The original markup fix addressed one defect: Razor rendered the enumerated `draggable` attribute with an empty value. Explicit `"true"` / `"false"` values fixed browser drags. The running Windows app already included that fix, but native dragging still failed.

The remaining cause is the desktop transport: MAUI's Windows BlazorWebView uses WinUI WebView2, where native in-page HTML5 drag/drop remains blocked. The upstream report explicitly distinguishes external-file `AllowDrop` support from this limitation: https://github.com/dotnet/maui/issues/37903 . Browser acceptance did not establish native acceptance.

`WriterApp.Device.Shared/Pages/Storyboard.razor` now selects `UsePointerDragging=true`. Desktop scene slots and handles disable native HTML dragging. `WriterApp.UI.Shared/wwwroot/storyboard.js` tracks primary pointer movement, captures after a six-pixel threshold, highlights the destination, and resolves gaps / card halves / empty chapters. Horizontal edge scrolling supports offscreen chapters. Escape, pointer cancellation, lost capture, and window deactivation cancel the gesture. Ordinary clicks select scenes, and buttons / inputs retain their editing behavior.

The JS callback validates the scene, chapter, and insertion anchor, then uses the existing reorder / atomic cross-chapter move path. Local saving, manuscript order updates, optimistic rollback, and error display remain authoritative. Both JS and the board block concurrent moves. Component disposal removes listeners. The web client retains native HTML5 dragging.

Verification:

- Native baseline: built the same app with explicit draggable markup and pointer mode disabled. A real mouse drag of synthetic Scene C into an empty chapter did not change the three-scene / zero-scene placement.
- Native fixed build: real Windows mouse drags reordered Scene C to first position, moved Scene B into the empty chapter, inserted Scene A before Scene B from the card edge, and appended Scene A after Scene B. Final order: Chapter 1: Scene C; Empty chapter: Scene B, Scene A.
- Closed and relaunched the isolated native app. The storyboard retained that order. A normal click selected Scene A after dragging; title editing opened and Escape canceled it without a text change.
- After the final listener changes, rebuilt and relaunched the isolated native app again. Moving Scene B back before Scene C succeeded: Chapter 1 showed Scene B, Scene C; the other chapter retained Scene A.
- Compared synthetic section IDs and complete page objects before / after. Page IDs and writing, including Japanese text, were unchanged. Snapshots: `artifacts/storyboard-pointer-native/before.json` and `after.json`.
- All 26 focused .NET tests passed across `LocalStoryboardTests`, `LocalProjectDropTests`, and `ProjectOutlinePresentationTests`. The rendered regression covers both transports and both slot/handle attributes.
- All 11 Node tests passed: `node --test Tests/storyboard-pointer-drag.test.mjs Tests/outline-drag.test.mjs`. New tests cover insertion, threshold, cancellation, outside releases, self drops, editing controls, save gating, and listener teardown.
- A real-browser pointer harness passed 10 groups with zero JavaScript errors: `artifacts/storyboard-pointer-native/check-pointer.cjs`, result `pointer-result.json`. It checks mouse gestures, clicks after dragging, card halves, empty chapter placement, Escape / pointer cancellation, invalid drops, busy / pending gating, and idempotent attachment.
- Windows desktop Debug build succeeded with zero errors. NuGet vulnerability lookup emitted existing `NU1900` warnings because its service was unavailable. The test build also reported an existing xUnit analyzer warning in `LocalStoryboardTests`.

The earlier HTML5 browser evidence is in `artifacts/storyboard-drag-host/`. It established the markup defect, but did not prove native desktop support. Current native acceptance uses synthetic data under `artifacts/storyboard-pointer-native/data/`.

The normal app was gracefully closed through its save-flushing close handler, rebuilt in `WriterApp.Desktop/bin/Debug/net10.0-windows10.0.19041.0/win-x64/`, and relaunched against the existing library. Automatic approval review rejected the proposed drag of a live user scene because it could persistently change user data; synthetic data was used instead. Fixture cloud sync was not enabled, and the fixed validation build uses an unreachable API URL to prevent cloud operations. The normal build uses the existing configured backend.

Packaged Release acceptance and other operating systems were not exercised. Native Debug acceptance is established for this Windows installation. The user's live scenes were not moved for testing.

## Follow-up: revision conflicts blocking drag and creation

The subsequent screenshot showed `Document ... has changed. Reload it before saving.` Dragging, adding a scene, and adding a chapter share `LocalStoryboardData.CommitAsync`. Unlike the local editor, this adapter kept its loaded revision after background sync acknowledged the document. `DeviceSyncEngine.UpdateMetadataAsync` advances the stored local revision even when writing and planning are unchanged, so the next storyboard save failed its compare-and-swap and subsequent actions reused the same stale snapshot.

`CommitAsync` now reads the latest document under the storyboard mutation gate. It adopts a newer revision only when the existing sync fingerprint confirms unchanged content, planning, structure, and trash state. It applies the mutation to that latest snapshot to retain synchronization metadata, and retries up to two times if an acknowledgment lands between the read and save. Real content conflicts still fail without overwriting the manuscript or discarding planning drafts.

Regression evidence: two new tests failed with the exact reported conflict before this change. They now cover moving a scene after a sync acknowledgment, creating a scene and chapter afterward, reload persistence, writing preservation, and retaining a planning draft when sync acknowledges during the commit. A third regression confirms that newer writing arriving during a commit is preserved and the stale mutation is rejected.

Follow-up validation: 96 focused .NET tests passed across storyboard, project drops, outline presentation, editor sessions, and device sync; all 11 pointer/outline JavaScript tests passed. The normal Windows desktop Debug output rebuilt successfully with zero warnings or errors. Test restore metadata emitted the existing NU1900 vulnerability-service warning and existing xUnit2031 analyzer warning. Test builds used an isolated BaseOutputPath because the running web host locks its assemblies. This follow-up did not perform a new native mouse acceptance run or live cloud sync run; prior pointer-drag acceptance above predates this persistence fix. Restart the rebuilt desktop app to load the updated adapter.
