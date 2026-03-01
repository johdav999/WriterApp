# Continuity Fix Apply Strategy

## Root Causes
- Proposal modal used placeholder/meta text for delete operations instead of showing a real deletion diff.
- Apply path failed fast when captured text no longer exactly matched current doc slice.
- Range resolution depended too heavily on a single source (captured doc range or plain range).

## Fixes Implemented
- Continuity proposal modal now renders:
  - `Before` text always
  - For delete: `Remove highlighted text` + exact deletion snippet
  - For replace: `After` text from sanitized fix payload
- Added stronger leak guard for instruction/meta-like text (`highlighted range`, tool/system/model payload patterns).
- Continuity apply now sends extra anchors to TipTap interop:
  - `beforeAnchor`
  - `afterAnchor`
  - `needleText`
- TipTap apply resolution now retries with multi-strategy matching:
  1. captured doc range
  2. expected text nearest match
  3. anchor nearest match
  4. contextual match using before/after anchors + needle
- PageEditor applies one automatic retry for:
  - `doc_expected_text_mismatch`
  - `could_not_resolve_range`
- Modal failure UX now offers:
  - `Show in text`
  - `Recompute range`

## Debugging
- Enable `window.__waQualityDebug = true` to get:
  - expected-text/anchor/contextual match counts and selected ranges
  - final chosen source and doc/plain ranges
- C# logs include:
  - retry attempts
  - stale range detection prior to apply
  - final reason/source from JS interop failures
