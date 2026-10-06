# Custom prompt with annotated writing

2026-10-04, current uncommitted WriterApp checkout. Applies to the shared device writing panel used by Windows desktop and iOS.

The reported error, `Anchored annotations need a targeted selection action or manual revision.`, came from `LocalWriting.Prepare`. With no passage selected, Custom prompt revises every page in the current section. A blanket annotation-count guard rejected that section before any provider call, including sections containing only unanchored tasks or resolved comments. The same guard also rejected next-paragraph continuation.

Annotations are separate planning metadata. The existing document save path calls `LocalPlanning.Reconcile`: it retains annotation identity, text, status and quote, keeps unique matching quotes linked, and detaches missing or ambiguous quotes for relinking through Notes & Tasks. Undo and redo reconcile again against the restored prose. The fix removes the blanket guard and explains retained annotations in the review. Unsupported content and complete page/run mapping checks still apply.

Interrupted writing-save recovery now compares the current document with the reconciled approved snapshot. A content save that completed before its history status was committed can resume without saving twice; later edits still reject recovery.

## Evidence and acceptance

| Issue | Severity | Expected and observed | Verification |
|---|---|---|---|
| CUSTOM-ANNOTATIONS-01 | P1 | A no-selection custom prompt reaches review, retains all section pages, changes writing only after Apply, preserves annotations, and supports undo/redo. | Automated component, service and storage verification passed. |
| CUSTOM-ANNOTATIONS-02 | P2 | Recovery after an interrupted approved save is idempotent with reconciled anchors and rejects later changes. | Automated component and storage verification passed. |

- Before the production fix: three new regression cases failed because the panel never reached review. `artifacts/custom-prompt-annotations/evidence/before-fix.trx`.
- After the fix: 129 writing, planning and annotation tests passed, including custom prompts that preserve or replace an anchor, preview without manuscript mutation, Apply, undo/redo, continuation, and recovery before/after the content commit. `artifacts/custom-prompt-annotations/evidence/after-fix.trx`.
- Six adjacent prompt-library tests passed separately. A prior combined run had a transient `.store.lock` cleanup failure in a prompt-library fixture; its isolated rerun passed. `artifacts/custom-prompt-annotations/evidence/prompt-library.trx`.
- Windows desktop Debug build passed with zero warnings and zero errors. Output: `artifacts/custom-prompt-annotations/desktop-build/Debug/net10.0-windows10.0.19041.0/win-x64/WriterApp.Desktop.exe`.
- The component tests use a synthetic provider and JS preview adapter. They exercise the actual Razor custom action, review, Apply and production file storage. Live-provider and native desktop acceptance were not performed. The running desktop process uses the older default-bin executable and must be relaunched into the rebuilt app to load this fix.

## Recheck

```powershell
dotnet test Tests/WriterApp.Tests/WriterApp.Tests.csproj --no-restore --filter 'FullyQualifiedName~LocalWritingPanelTests|FullyQualifiedName~LocalWritingTests|FullyQualifiedName~LocalPlanningTests|FullyQualifiedName~LocalAnnotationMarkupTests|FullyQualifiedName~DeviceAnnotationWorkflowTests' -p:BaseOutputPath=C:/Users/Johan/source/repos/WriterApp/artifacts/custom-prompt-annotations/build/
dotnet build WriterApp.Desktop/WriterApp.Desktop.csproj --no-restore -p:BaseOutputPath=C:/Users/Johan/source/repos/WriterApp/artifacts/custom-prompt-annotations/desktop-build/
```

Native acceptance: save current writing, close the older Prosa process, launch the rebuilt executable, open an annotated section, leave the passage unselected, enter a custom instruction and preview. Review every changed page before applying. Check retained comments/tasks, relinking for changed quotes, and History undo/redo. No user manuscript was modified by the automated verification.
