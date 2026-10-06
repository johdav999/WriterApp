# Writing tools redesign — 2026-10-04

Scope: the device/desktop Writing tools drawer, shared writing settings, proposal review layout, and supporting tool groups. Existing AI targets, presets, cancellation, review/apply and history behavior remain connected.

The user's screenshot is the visual baseline. The old parent grid placed the entire writing panel and Summarize button beside each other, stretching the button to the panel height. Local controls had minimal layout styling, and source limits, settings guidance, availability guidance and a recommendation card competed with the actions.

| Issue | Acceptance | Result |
|---|---|---|
| WT-01 — P2: cramped controls and stretched summary action | Target above actions; 40px selects, 42px buttons, 12px action gaps; summary uses its own row; no horizontal panel overflow | Verified in the real device Razor components hosted in Edge at drawer widths 280, 300, 360 and 700px |
| WT-02 — P2: excessive default text | Settings and source help collapsed; custom prompt and translation expandable; one brief target hint; stats reachable through a compact footer action | Verified in the browser, including keyboard settings expansion and scope changes |
| WT-03 — P3: cramped review comparisons | Comparison adapts to drawer width; Apply and Dismiss have separate spacing | Source compiled; existing proposal, Apply, cancellation, stale-source and history checks pass. Native visual review remains open |

Verification lives in `artifacts/writing-tools-redesign/`:

- `results.json`, `verify.mjs`, `panel-*.png`, `viewport-*.png`, `settings-expanded.png`, `section.png`, `continuation.png`, `more-expanded.png`: interactive production device UI with a new isolated local document and signed-out account. Browser checks cover layout, accessible controls, settings values, all three scopes, custom prompt, translation expansion, availability refresh and stats navigation. Drawer sizes are varied through the host grid for verification; this does not claim mobile shell support.
- `tests-final.log`, `results/writing-final.trx`: 88 writing tests pass. The existing compiled test harness used the current production Shared UI and Device assemblies. Initial copy-sensitive assertions were updated to the shorter text and control selectors. Optional-demo availability fixes arriving in the same checkout were retained and pass the final suite.
- `desktop-build-final.log`: normal Release desktop build passes with zero warnings and zero errors. An earlier build ran out of disk space; only this task's generated output was removed, and the normal build was rerun successfully.

The native desktop app was not launched or replaced, and no live provider request was made. Final acceptance in the rebuilt Windows app remains a separate check. Test data and preview hosts do not use the user's ordinary document storage. The task-owned browser host is stopped after verification.
