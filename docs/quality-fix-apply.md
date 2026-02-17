# Quality Fix Apply Notes

## Root Causes

1. Proposal preview used raw `Fix.Text` for all fix kinds, including delete fixes.  
   Result: metadata/prompt-like payloads could appear as "Proposed" text.
2. Quality apply in TipTap used strict expected-text matching for captured ranges and failed early on mismatch.  
   Result: false `doc_expected_text_mismatch` when unrelated edits shifted text.
3. Import append mode in the client used `SetContentAsync(result.Html)` for active page refresh.  
   Result: imported fragment replaced editor content instead of appending.

## Fixes

1. Proposal sanitization:
   - Delete fixes now show `"(removed)"`.
   - Replacement proposal text is filtered through meta-leak detection (`OpenAI/tool/system/model/input` signatures).
   - Suspicious proposal text is suppressed and logged once per issue key.

2. Apply mismatch recovery (TipTap patch):
   - On expected-text mismatch, retry resolution using:
     - recomputed fix-range resolution,
     - nearest match of `expectedText`,
     - anchor-based nearest match for delete fixes.
   - Only returns `doc_expected_text_mismatch` after recovery attempts fail.

3. Import append behavior:
   - Added `appendImportedHtml` command in TipTap commands module.
   - Append mode now inserts imported content at document end with paragraph boundary.
   - Client in-memory page state mirrors append behavior.

4. Style & Quality action:
   - Run button label now switches between `Run check` and `Re-run check` based on run state.

## Debug Tips

- Enable quality debug:
  - `window.__waQualityDebug = true`
- Look for logs:
  - `[quality] apply target resolved`
  - `[quality] apply fix resolved`
  - `doc_expected_text_mismatch` (should be rarer after fallback recovery)
