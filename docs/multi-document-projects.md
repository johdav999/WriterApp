# Multi-document projects

A project contains several documents and identifies a primary manuscript. Each manuscript has its own parts, chapters, scenes, scene cards, notes, annotations, synopsis and storyboard. Supporting documents (research notes, outline documents, synopsis documents and other material) remain ordinary editable documents without a manuscript tree. Project title, author, language, genre, cover and export defaults are shared metadata.

## Desktop and web workflow

Open a project and use **Add document** to add a manuscript or supporting document. The document selector changes the active manuscript; selecting supporting material opens its editor. **Make primary** chooses the manuscript used by default project navigation and project progress. Storyboard and synopsis selectors switch between manuscripts. Structure and storyboard links retain `documentId`, so an alternate draft does not reopen the primary draft accidentally.

Desktop library roots group every member under one project. The Documents view lists each member separately. Trashing or restoring a project root applies to its members; an individual document action applies to that document. Deleting a server document removes only its owned tree and dependents. Project deletion retains its existing whole-project behavior.

## Ownership and persistence

- Server `Documents.ProjectId` remains the existing project relationship. The former unique manuscript-per-project index is replaced with a normal index.
- `Projects.PrimaryDocumentId` supplies the default manuscript. `Projects.MetadataRevision` protects concurrent shared metadata updates.
- `ProjectNodes.DocumentId` owns the manuscript tree. Project tree, node, linking, AI and export operations select one manuscript. Nodes cannot be moved or attached across manuscript boundaries.
- Web requests capture manuscript identity in the URL, and the structure cache is keyed by project and document. Cold scene links derive the document from the scene.
- Desktop project metadata is stored independently in `projects/{projectId}.json` inside the document store. Existing inline metadata is a hydrated projection; manuscript nodes and synopsis remain in their document file. Stale writing saves preserve authoritative project metadata.

## Upgrade and synchronization

Apply `20261002061051_MultiDocumentProjects` for SQLite or `20261002061110_MultiDocumentProjectsSqlServer` for SQL Server through the normal server migration process. The upgrade selects the original manuscript and backfills legacy tree ownership without changing document, section, page or node IDs. Empty/support-only projects acquire a primary manuscript when one is created.

Desktop document envelopes use schema 4 and project projections use version 3. Older envelopes remain readable and their exact original bytes are backed up before migration. Earlier desktop versions reject schema 4 rather than editing an incomplete representation.

Desktop synchronization uses `/api/sync/v4/documents`. Project payload version 3 includes primary manuscript identity and metadata revision. Versions 1–3 of the HTTP API remain available for compatible single-document projects; they reject multi-document project mutations/downloads with HTTP 426. The desktop does not silently fall back to an older protocol.

Manuscript edits invalidate only their owning document's snapshot. Shared metadata changes invalidate all members. Sync acknowledgments retain the shared metadata revision durably. Sibling metadata updates that already match the local writing do not create false conflicts. Divergent metadata retains independent local and cloud copies; choosing either copy applies that choice explicitly.

The migration deliberately has no destructive downgrade. Rollback requires a pre-upgrade database backup because multiple manuscript trees cannot fit safely into the former model. Deploy the server migration and v4 endpoint before distributing the new desktop build.

## Verification and acceptance

Automated coverage includes two independent desktop storyboards, supporting documents, shared metadata and stale saves, library grouping and project trash/restore, two desktop stores and restart, primary changes and shared renaming, conflicting metadata with both resolution choices, server request ownership, client request scope/cache separation, sibling-preserving deletion, legacy migration and older-client rejection.

Verification completed on 2026-10-02: all 885 .NET tests passed; the test build compiled the server, web client and shared libraries; the Windows desktop build passed. Both database providers report no pending model changes. SQLite migration execution and legacy node identity preservation are tested. The SQL Server upgrade script was generated successfully; execution against a live SQL Server database remains unverified. Builds reported NU1900 warnings because the NuGet vulnerability feed was unavailable.

Build/test output is retained in `.codex-build/multi-document-*.log`, and the generated SQL Server upgrade script is `.codex-build/multi-document-sqlserver-upgrade.sql`.

Before release, verify these flows in the rebuilt desktop and web browser:

1. Create a project, add an alternative manuscript and research notes, and verify the library lists each member once.
2. Give both manuscripts distinct scenes/cards and synopsis text. Switch between them, reload and restart; the planning data must remain separate.
3. Type a scene-card change and switch manuscripts. A failed save or active scene action must retain the current view. Deep links and back/forward navigation must keep the correct manuscript.
4. Make the alternative manuscript primary. Project default navigation and progress must follow it; explicit links to the original must still open the original.
5. Sync from two devices, rename the shared project, change primary, and edit distinct manuscripts. Verify grouping and absence of false sibling conflicts.
6. Make divergent shared metadata edits. Choose keep local, then repeat with keep cloud. Verify the selected metadata and inspect both retained backup copies.
7. Trash/restore an individual member and the project root. Permanently delete a trashed alternate manuscript and verify the original tree and research survive.

Native mouse/pixel acceptance, live AI/provider execution, live tenant sync and live SQL Server migration execution require separate runtime verification. The implementation changes do not deploy a server or restart an already running desktop app.
