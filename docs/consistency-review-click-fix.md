# Consistency suggestion click repair — 2026-10-04

The saved report matching the user's screenshot supplied `plainTextStart: 61` and `plainTextLength: 102`. Those offsets selected `etting, the platform bathed in a soft, fading glow. Elin stepped off, gripping her suitcase a little t`: the range begins inside **setting** and ends inside **tighter**. The resolver previously accepted any anchored substring contained in the evidence quote. The shipped editor then rejected this range with “Select a complete passage or whole words before reviewing this fix.” The panel rendered the rejection below the entire report, outside the visible finding.

`LocalConsistencyRevisions.Resolve` now accepts an anchor only if both endpoints preserve whole words and UTF-16 characters. Invalid offsets fall back to the exact evidence quote, subject to the existing unique-match check. Valid phrase anchors still work; repeated or missing quotes still block review. The resulting comparison explicitly shows the passage that Apply will replace. This does not infer a different intended passage or establish the semantic quality of the AI's suggested prose.

`LocalAiPanel` shows progress and review/apply errors within the clicked finding. The operation retains its finding index even when preparation or the workspace save callback fails; unrelated operations clear that association. Review, Apply, Discard, persistence, and history remain connected.

## Acceptance packet

Profile: Prosa Windows desktop shared Razor UI; author role; synthetic account/provider/sync and isolated document storage; real production components and shipped editor module in headless Edge. Fixture: `artifacts/consistency-review-fix/Host/`. Launch with `dotnet run --project artifacts/consistency-review-fix/Host/Host.csproj --configuration Release --no-launch-profile --urls http://127.0.0.1:5423/`, then run `node artifacts/consistency-review-fix/verify.mjs`. Use a fresh fixture data directory for each run; the driver applies its synthetic suggestion.

| ID | Severity | Reproduction and acceptance | Evidence | Result |
|---|---|---|---|---|
| CR-01 | P1 | Check the report with offsets 61/102, click Review & apply suggestion, compare the entire exact evidence, Discard, Review again, Apply, reload. Only the reviewed quote changes; unrelated suffix remains. Undo restores the original. | 46 focused .NET tests; `results/consistency-review.trx`; `browser-results.json`; `review-390.png`; `review-710.png` | Verified in production-component fixture; native interaction unverified |
| CR-02 | P2 | Click a finding whose quote is missing. Its error appears within that finding, writing remains unchanged, and the valid finding can still be reviewed. | Real panel regression test; `inline-error.png`; browser driver | Verified in production-component fixture |

Artifacts are under `artifacts/consistency-review-fix/`. Browser checks pass with zero page errors; screenshots were visually inspected. The Windows Debug desktop build succeeds with zero warnings/errors. The test build has one unrelated pre-existing xUnit warning in `LocalStoryboardTests`.

Rebuilt executable: `artifacts/consistency-review-fix/desktop/Debug/net10.0-windows10.0.19041.0/win-x64/WriterApp.Desktop.exe`. The user's existing desktop process and authored data were preserved. Close it after saving, launch this executable, and run a fresh consistency check. Native interaction and live-provider suggestion quality were not verified; no backend deployment was performed.
