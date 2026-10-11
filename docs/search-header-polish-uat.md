# Header search polish — 2026-10-10

Scope: Prosa device/desktop header, author search entry. Baseline: user screenshot showing a clipped input, exposed inline scope label and mismatched controls.

| Issue | Severity | Acceptance | Result |
|---|---|---|---|
| SEARCH-01 | P2 | Readable input, aligned controls, compact scope, clear keyboard focus, no narrow-width overflow | Source-derived Edge layout fixture passes at 859, 560, 390 and 320 px. Native acceptance remains open. |

Implementation: isolated LocalSearch Razor/CSS uses existing design tokens, a search icon, integrated clear button, styled native scope selector and primary submit. Accessible input/scope labels and search landmark are retained. Container query stacks controls when available width falls below 500 px. Search logic is unchanged.

Evidence: artifacts/search-polish/check-layout.cjs extracts the actual form markup and loads production component CSS and design tokens. preview-*.png and layout-checks.json record visual checks. This is a static layout substitute, not a live Blazor interaction or native acceptance test. Reviewed wide and narrow images; shortened the longest scope caption to avoid clipping.

Validation: device shared build passed without warnings. Desktop build passed with three NU1900 warnings because NuGet vulnerability metadata was unreachable. Built in isolated artifacts/search-polish/desktop output to preserve running app files. Running author application was not restarted; rebuild/relaunch is required to load the change. No live search/navigation test was performed.
