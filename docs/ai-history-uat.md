# AI history GUI and undo/redo verification — 2026-10-01

Scope: the shared AI history panel and local desktop AI actions. Author role; Windows Debug build; synthetic local-only project named `AI history acceptance`. Native acceptance uses an isolated validation data directory under ignored `artifacts/uat/ai-history/native-data`. No live AI request or cloud-enabled sample mutation was needed.

| Issue | Impact | Acceptance | Evidence / result |
|---|---|---|---|
| AIH-001 | P2: dense internal labels and JSON make history hard to review | Readable cards, status/time metadata, expanded scene fields, responsive previews, clear empty/busy states | Shared component render tests and native 360px context panel inspection passed. Scene suggestions render field comparisons without raw JSON. |
| AIH-002 | P1: local history offers recovery copies but no actual undo/redo | Writing, scene and synopsis changes can be reversed/reapplied and saved; reviewed analyses cannot be undone | Native writing undo restored `The harbor was quiet.`; redo restored `The harbor lay quiet beneath the evening sky.`. Editor word count changed 8 → 4 → 8. Native synopsis redo/undo changed history state and persisted the corresponding field. |
| AIH-003 | P1: snapshot replacement could discard later work | Compare affected values; preserve unrelated fields, notes, links, status and current sync metadata; explain blocked changes | Service tests cover affected-writing conflicts, unrelated synopsis/scene edits, restart persistence, immutable evidence, and completing interrupted undo/redo. |

Native steps: launch the validation build, open the synthetic project, choose History, undo and redo its writing card, redo and undo its synopsis card, expand the reviewed scene suggestion. All save results were checked against visible editor/history state and the synthetic project's local history files. Capture: `artifacts/uat/ai-history/native-history.png` (local ignored evidence).

Automated validation: 87 focused tests passed covering LocalAiHistoryActions, AdvancedDeviceAi, EditorPanelPresentation, SceneCardProposalPresentation, ConsistencyReportPresentation, AiActionsController and LocalEditorSession. Server/web and shared assemblies compile through the test project. Windows desktop Debug builds pass, including the normal output. NuGet vulnerability metadata lookup emits NU1900 because the feed is unavailable; compilation and tests succeed.

Limits: native scene-field undo has service coverage; this pass exercises a reviewed scene preview, not a newly generated and applied live scene proposal. Authenticated web undo/redo was wired to existing handlers and covered by controller tests, but was not exercised in a live web session. iOS and signed installer acceptance were not run. Native sample verification does not certify cloud round-trip behavior or every older history record; ambiguous older targets expose recovery instead of guessing.
