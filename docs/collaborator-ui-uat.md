# Collaborator UI polish — 2026-10-19

Product: Prosa normal web editor and shared web/device editor binding. Environment: existing local Staging instance on `http://localhost:5398`, launched with `scripts/start-local-collaboration.ps1`; isolated Kestrel/SQLite/Node browser fixtures for automated acceptance. Roles: existing signed-in owner in Edge and Editor in Chrome, manuscript `2921410a`; both have display name Johan. Evidence: `artifacts/collaborator-ui/`. Changes remain in the existing dirty checkout.

Scope: discover people on the active page, identify colored cursors, use participant controls with a keyboard, handle connection/access changes, and inspect narrow/print layouts. Presence remains ephemeral and separate from saved writing.

| ID | Severity | Finding | Acceptance | Status |
| --- | --- | --- | --- | --- |
| COL-01 | P2 | Raw participant text appears at the bottom of manuscript paper. | Compact shell bar above editing tools; manuscript contains no participant UI. | Verified in normal editor and both real accounts |
| COL-02 | P2 | Duplicate names and repeated connections are difficult to identify. | Per-account avatars/colors, one row per account, self marked You; matching cursor color. | Verified in real same-name accounts and automated duplicate-connection check |
| COL-03 | P2 | Participant controls and connection feedback are missing. | Keyboard-operable list; Escape/outside dismissal; disconnected/revoked presence cleared; live status distinct from save status. | Verified in real UI and automated keyboard/disconnect/reconnect/revocation checks |
| COL-04 | P3 | Unbounded labels and layout lack polish. | Rounded/truncated cursor badges; narrow list fits; long names wrap; no presence in print. | Verified 360px layouts, first-line badge visibility and print checks; labels bounded in CSS |

Baseline: user screenshot shows `Online: Johan, Johan` at the bottom of the page, with identical blue cursors. Name resolution was already verified; this pass changes presentation and lifecycle feedback.

## Verification packet

Build: web bundle version 11, both generated web/device bundles rebuilt, TypeScript check and `dotnet build BlazorApp.csproj --no-restore` passed. Logs: `artifacts/collaborator-ui-build.log` and `collaborator-ui-app-build.log`.

The real Kestrel/Hocuspocus browser probe passed 12 journeys: display-name mapping, matching avatar/cursor colors, keyboard/Escape/focus behavior without changing text or selection, three connections representing two accounts, narrow/print layouts, disconnect/reconnect, comments/anchors, undo/redo, and revocation. The normal Prosa probe also passed with the bar outside paper, one self marker, mouse-opened participant region, and both accounts' writing/concurrent changes/saves/reloads. `PROBE_SYNC_ONLY=1` keeps this run focused on sharing, presence and writing. Logs: `artifacts/collaborator-ui-final-tests.log`; per-journey results: `artifacts/realtime-p9-interactions/browser-results.json`. Screenshots: `web-narrow.png`, `device-narrow.png`, `web-participants.png`, `device-participants.png` in the evidence directory.

Real-account flow: reload the existing owner and Editor on the same document, wait for Live, open People, inspect You and separate purple/green identities for the two Johan accounts, move a cursor to the first line, and verify its name badge stays visible. Existing writing (`… Yes it is. Test`, 15 words) remained identical and saved. Screenshot and DOM evidence: `real-editor-final.png`, `real-accounts.json`. A local restart initially encountered the existing lease/recovery fence; reopening after lease expiry restored both sessions. The normal onboarding overlay was dismissed before mouse verification.

Boundary: verified on local Staging and desktop Edge/Chrome browser surfaces, with web/device bundles at narrow widths. Packaged Android/desktop execution and hosted deployment are not established by this pass. Existing broader section-navigation/AI workflows remain outside this presence UI scope.
