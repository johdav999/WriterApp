# Android writing header — 2026-10-14

## Scope and cause

Prosa Android, local author workspace, existing project manuscript. Baseline revision
`4ed39d5` with existing workspace changes. The project context in
`WriterApp.Device.Shared/Pages/DocumentWorkspace.razor` was a single unstyled inline
navigation element. It repeated equal project/document titles, appended the scene
title directly to Storyboard, and rendered Cover studio as a separate bare link.
There were no layout rules for `.local-project-context`.

The context now has a breadcrumb and a separate Project tools navigation group.
Equal project/document names appear once; a distinct document name remains visible.
The scene title stays in the existing editor heading. Links retain their project,
document and scene identifiers. Scoped styles use shared design tokens, SVG icons,
44px minimum link heights, keyboard focus outlines and ellipsis for long titles.
At 800px and below the breadcrumb and tools occupy separate rows. Wider layouts
place them on one row. The existing context wrapper is retained so annotation
mode continues to hide the whole context area.

## Issue ledger

| ID | Severity | Flow | Expected / acceptance | Result |
|---|---|---|---|---|
| HEADER-001 | P2 | Open project manuscript | Clear hierarchy; no repeated manuscript/project label; distinct tools; no concatenated links; no overflow at 320px; working touch navigation | Verified in installed Debug Android WebView |

## Verification

- Built Android Debug x64 with embedded assemblies, installed with `adb install
  --no-incremental -r`, and relaunched the app without clearing its data.
- Native viewport: 412 CSS pixels. Visually inspected the final screenshot.
- `node Tests/android-writing-header.mjs`: **11 checks passed**. Six layout cases
  (native and viewport overrides at 320, 360, 412, 800 and 1280px), four actual
  touch navigation/return paths (Storyboard, Cover studio, project and Projects),
  and preservation of all **22** pre-existing local document files by SHA-256.
- Long-title stress checks temporarily change DOM text only. They verify ellipsis,
  containment, minimum touch heights, label alignment and a header height below
  120px. These checks do not rename any persisted project.
- `node --check Tests/android-writing-header.mjs` and scoped `git diff --check`
  passed. Final Android build: zero errors; 10 NU1900 warnings because the NuGet
  vulnerability feed was unavailable.

Evidence: `artifacts/android-writing-header/before.png`, `after.png`, `results.json`,
`document-hashes-before.json`, `artifacts/android-uat/writing-header-after.png` and
`artifacts/android-writing-header-build.log`.

Build command:

```powershell
dotnet build WriterApp.Android/WriterApp.Android.csproj -c Debug `
  -p:RuntimeIdentifier=android-x64 -p:EmbedAssembliesIntoApk=true `
  -p:ArtifactsPath=C:/Users/Johan/source/repos/WriterApp/artifacts/android-writing-header-build `
  -p:UseArtifactsOutput=true --no-restore
```

The focused acceptance script expects an open project document in the installed
Debug app and a preflight document-hash snapshot at the evidence path. It restores
the original viewport and returns to the writing route after following each link.
Physical-phone, iOS and native desktop visual checks were not performed.
