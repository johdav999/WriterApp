# Client startup repair — 2026-09-29

Profile: Prosa Blazor WebAssembly client; local Development host; existing development identity; route http://localhost:5387/app/documents. Scope: application startup, library switching, identity badge. Existing manuscript/document data was not edited.

| ID | Severity | Observation | Acceptance / regression | Status |
|---|---|---|---|---|
| BOOT-01 | P1 | Both browser tabs stopped at 98% with failed WriterApp.UI.Shared PDB downloads. Server returned the asset with its matching SHA256. Cached resource failure implicated. | Revalidate boot resources, retry once with a fresh URL, preserve integrity checks; existing localhost tabs load. Four Node tests cover module delegation, integrity, retry, and persistent failure. | Verified |
| AUTH-01 | P2 | Badge requested Azure-only /.auth/me on localhost and displayed Not signed in despite /api/auth/me authenticating the user. | Use cached canonical AuthStateService identity; display Dev User; remove badge's Azure probe. | Verified |

Implementation: manual Blazor startup in index.html; client-startup.js uses the supported loadBootResource callback, no-cache revalidation and one cache-reload retry with integrity preserved. Startup rejection displays actionable feedback instead of the loading ring. UserBadge uses AuthStateService identity claims, shared with AuthGuard.

Validation: server/client build succeeded with no warnings/errors. Four client-startup Node tests passed. Four DeletedAccountClientHandlingTests passed. Both original failing browser tabs were reloaded against the rebuilt local server; project library loaded, Documents view showed its expected empty state, and account badge showed Dev User. Latest load on tab 4 at 2026-09-29 15:45:37 UTC had no new console errors. Older errors remained in the browser's log history. Evidence: artifacts/uat/client-startup/documents-loaded.png.

Server asset manifest must be restarted after rebuild; rebuilding a client beneath a running server exposed new assets as 404 until its manifest reloaded. The identified server was restarted once with final binaries, without adding a second listener. Current local server is managed by the tool session; stop it before starting another server on port 5387.

Reference: https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/startup?view=aspnetcore-10.0#load-client-side-boot-resources

Limits: local development authentication verified; Azure identity providers and deployment were not exercised.
