# Sharing workspace polish — 2026-10-19

## Profile and scope

Prosa web / shared Blazor UI; working checkout based on `4ed39d5` with existing concurrent changes preserved. Routes: `/app/collaboration` and `/app/documents/{id}/sharing`. Roles: owner, invited viewer, editor. The browser fixture uses synthetic manuscripts and API responses with the actual compiled client and shared component. No real invitations or manuscripts are changed.

Design follows the existing shared navy, warm neutral surfaces, Segoe UI typography, radius and focus tokens. The shared panel is used by the web and device hosts; device Projects navigation retains `/projects`.

## Issue ledger

| ID | Severity | Finding | Acceptance | Result |
| --- | --- | --- | --- | --- |
| SHARE-01 | P2 | Plain lists obscure invitations, ownership and access. | Distinct invitation section, manuscript cards, role badges, owner controls and immediate pending-invitation feedback. | Verified in actual Blazor UI with fixture data. |
| SHARE-02 | P2 | Initial request renders an unavailable message before discovery finishes. | Loading, unavailable, populated, empty and failed-refresh states are distinct; retry works. | Verified with delayed and failed fixture responses. |
| SHARE-03 | P2 | Fixed navigation squeezes phone content; nested body scrolling clips full-page content. | Responsive page, full-width mobile content, reachable footer, long-title wrapping at 320/390/768px. | Verified in Edge; shared shell rules are scoped to sharing. |
| SHARE-04 | P3 | Shared libraries are difficult to scan as they grow. | Search both owned and incoming manuscript lists; explain zero matches; clear restores results. | Verified. |

## Verification

- `dotnet build WriterApp.Client/WriterApp.Client.csproj --no-restore -v quiet`: success, no warnings or errors.
- `dotnet test Tests/WriterApp.Tests/WriterApp.Tests.csproj --no-restore -p:UseAppHost=false --filter FullyQualifiedName~CollaborationSharingPanelTests -v quiet`: 2 passed. Covers account invalidation and uncertain invitation replay. `UseAppHost=false` avoids replacing the running local host executable. Existing dependency/analyzer warnings remain in the log.
- `node Tests/sharing-workspace.mjs` with `PLAYWRIGHT_MODULE` set to an installed Playwright module and current static client at `PROBE_CLIENT_URL` (defaults to `http://localhost:5398`): 11 checks passed; no uncaught browser errors. Covers populated/empty states, search, decline, accept navigation, Enter-key form submission, immediate outgoing invitation row, refresh failure/retry, disabled availability, initial loading, mobile reachability and long titles.
- Visually inspected desktop, mobile and owner-access screenshots. Screenshot fixtures contain illustrative titles and accounts.

Evidence: `artifacts/sharing-polish/{desktop,mobile,owner-desktop,owner-mobile,empty}.png`, `artifacts/sharing-polish/results.json`, `artifacts/sharing-polish-build.log`, `artifacts/sharing-polish-tests.log`.

The API-backed security tests retain their existing account/race coverage. The new browser fixture verifies presentation and request/navigation behavior, not real-provider identity, backend invitation persistence or native device acceptance. No deployment was performed. Packaged native host and real multi-account acceptance remain separate checks.
