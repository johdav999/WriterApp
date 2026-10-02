# Synopsis UI repair — 2026-09-29

Profile: Prosa Blazor web client, local Development server, signed-out development document. Launch: `dotnet run --project BlazorApp.csproj --launch-profile http`. Revision: be57525 plus existing working changes and this repair. Scope: synopsis rendering, labels, keyboard focus, responsive form and coaching controls. No manuscript data edited.

| ID | Severity | Finding | Acceptance | Status |
|---|---|---|---|---|
| SYN-01 | P2 | Screenshot showed unstyled shared fields following component extraction; fresh baseline loaded scoped CSS. Stale assets are implicated, not proven as the sole original cause. | Shared field CSS loads, fields align and remain readable. | Desktop verified |
| SYN-02 | P2 | Parent isolated CSS did not style PlanGatedButton child buttons; coaching labels lacked associations. | Scoped deep button styling, explicit label associations, visible keyboard focus. | Desktop verified |
| SYN-03 | P2 | Fixed navigation sidebar squeezed phone synopsis fields to 136px. | Horizontal navigation on synopsis at <=700px, full-width single-column form without page overflow. | Verified at 390px: 309px fields, horizontal navigation, no page overflow |

Changes: Shared field guidance and semantic labels; responsive field grid; story form card; coaching control styles; live save status; synopsis-only phone navigation adjustment. Shared presentation remains separate from persistence and AI orchestration.

Validation: Client build and server build succeeded without warnings/errors. Existing SynopsisTests: 3 passed. Fresh-cache browser verified final form at 1280px. Rebuilding during a running server caused stale fingerprinted resource failures; restart server and refresh cached browser assets after local builds. Native host and authenticated save/reload/live AI were not exercised.

Final evidence: `artifacts/uat/synopsis/desktop.png` (1440x1000) and `artifacts/uat/synopsis/mobile.png` (390x844). Desktop has 10 labeled fields, two-column workspace, no horizontal page overflow. Mobile has a single-column form and independently scrolling navigation. Final visual verification used the same localhost server over the IPv6 loopback origin to avoid the embedded browser's stale cache. Temporary viewport override reset.
