# Editor content compatibility

## AI parity prompt 4 — Glossary-aware quality checks (2026-10-05)

Local quality analysis now passes the current account/document's saved glossary
snapshot into the existing shared rules. Findings keep exact page/selection text
offsets; source is recaptured after asynchronous refresh before results are shown.
Casing changes use the existing targeted preview, approval, save and scoped
History recovery. Near matches remain informational in both fresh and cached
server results. Context loading does not transform manuscript HTML or alter the
editor schema/bundles, authored local format, sync payload or database schema.
The additive versioned cache preserves missing/unknown data instead of inventing
an empty glossary. Existing quality and persistence checks cover these boundaries;
no editor behavior changed, so the shipped-editor harness was not regenerated.

## AI parity prompt 3 — Planning-field approval (2026-10-05)

Scene-card review in both client routes uses shared typed field selection.
Approved planning fields persist in one checked transaction; no manuscript page,
rich-content transformation, editor schema or shipped editor asset changes.
Unselected stored strings/JSON retain their exact values, including inherited
legacy scene planning. Planning history retains the approved subset and committed
receipt; scoped planning Undo/Redo remains prompt 8. See `docs/desktopai-uat.md`
for focused persistence/reopen and shared-control evidence and open acceptance
gates.

## Desktop AI prompt 11 cover assets (2026-10-03)

Cover generation/persistence does not edit or reserialize manuscript prose. Project covers use a separate validated inline PNG field, shared project revision checks and an atomic prior-cover recovery record. `CoverStudioContract` is now the common PNG validator for generated assets and existing local publishing covers: PNG signature/IHDR, encoding/depth, chunk CRCs and completeness, no trailing bytes, bounded zlib pixel decoding/filter checks (including Adam7), 2 MiB maximum, dimensions at most 8000 pixels/32 megapixels. MIME mismatches, SVG/HTML, malformed base64 and remote provider URLs cannot become generated previews or saved desktop assets. Covers render through image elements, never provider markup. Cover-aware sync allows a bounded additional PNG while keeping the original writing/planning limit. Publishing embeds approved bytes offline with its existing image-only data URI and inert export-preview policy. No editor source, content schema or generated editor bundle changed in prompt 11.

## Desktop AI prompt 10 — History inspection and recovery boundaries (2026-10-03)

Cloud AI history is an encoded text comparison, never editor HTML or an automatic mutation. Cloud-only entries cannot use local Undo/Redo or recovery-copy handlers. Reconciled rows retain the local record's exact original/approved snapshots and existing guarded, target-scoped changes; a cloud state cannot override local durability. Later changes to affected writing or fields still refuse undo, and a recovery copy preserves the current document. Account/backend identity guards additionally protect linked local records.

History reporting stores snapshot fingerprints and typed source/target metadata separately from legacy cloud undo snapshots. It never flattens or substitutes writing to satisfy an event request. An Applied/Undone intent is written with terminal local history completion; interrupted Applying/Undoing/Redoing remains local recovery work, without a false cloud completion. No editor source, schema or generated asset changed in prompt 10; existing rich-content and real-editor harness checks are rerun as regression evidence.

## Desktop AI prompt 9 — Preset writing targets (2026-10-03)

Reusable presets use the existing writing revision adapters. Selection execution validates the exact saved page/range and previews inert prose through `previewQualityRevision`; Apply reacquires the same selection and rejects changed source/account/target. Section execution captures every ordered page/text run and previews validated complete results through `previewTranslation`, retaining paragraph/run identities, marks, headings, lists, tables and supported attributes. Custom mapped section output never becomes an HTML fragment or a flat section replacement. Unsupported content/annotations and incomplete or malformed results retain the original with actionable refusal.

Version-3 writing history retains the exact preset definition and original/approved manuscript snapshots before mutation. Reopen, approved-save recovery and target-scoped Undo/Redo use the existing writing safeguards and preserve other pages, planning and later edits. Imported unsupported presets remain lossless library data until explicitly repaired; they cannot bypass these application constraints. No editor source/schema or generated bundle changed in prompt 9. Two added real-editor harness checks exercise custom selection and complete section preview/application/reopen while preserving rich nodes; all 60 shipped-bundle checks passed. Razor tests exercise the production library-to-writing handlers and durable saves; headless browser fixtures establish layout/keyboard behavior, with Windows/provider acceptance separately open in `docs/desktopai-uat.md`.

Contract version 2, updated for desktop editor menu parity (2026-09-30).

`WriterApp.Shared/Editor/content-contract.json` is the shared machine-readable allowlist. The device TypeScript entry point imports it; `EditorContentContract` embeds it in the shared contracts assembly for native validation, transfer and backend upload validation. The web editor retains its richer schema. This contract describes the editable intersection, not the web editor's maximum payload. Storage and sync transport original content without sanitizing it.

## Capability matrix

| Content | Device editing and preservation | Controls / import behavior |
|---|---|---|
| Paragraphs, Unicode text, hard breaks | Editable in both hosts | Paragraph; plain-text import is escaped into paragraphs |
| Headings 1–6 | Editable in both schemas, level preserved | Desktop shared toolbar now offers 1–6. Web keeps its existing visible controls; schema support is broader than the first three toolbar choices |
| Bold, italic | Editable in both | Shared toolbar and existing keyboard commands |
| Underline, strike, inline code | Supported marks preserved by both schemas | More offers strikethrough and inline code; underline retains its TipTap shortcut. Import/export retain these tags |
| Blockquotes, horizontal rules | Supported and preserved | Blockquote toolbar; horizontal rule in More |
| Bulleted/numbered lists, nested list items | Supported and preserved | List toolbar; ordered-list `start` must be an integer in Int32 range and survives import/export |
| Code blocks | Supported plain text inside `pre > code` | Optional `language-*` code class/legacy `language`; no device language picker. Rich marks inside code blocks are not supported |
| Links | Absolute HTTP, HTTPS and mailto only on device | `href`, `target`, `rel`, `class` preserved; unsafe/control-character URLs refused. No opening links directly from the device editor |
| Tables, rows/cells, column widths, merged cells | Editable and preserved | Insert 3x3 table; More offers row/column insertion and deletion, header toggles, merge, split and delete table. Column resizing uses TipTap |
| Images, asset IDs/URLs, width | Editable and preserved | Local PNG/JPEG/GIF/WebP files up to 300 KB embed as data URLs for offline reopening. Image URL accepts HTTP/HTTPS. Existing root-relative web image sources are retained. File import still removes images; HTML export and saved preview preserve them |
| Text alignment and indentation | Editable and preserved | More offers left/center/right alignment and indent decrease/increase (0–8 levels, 2em per level). Only these style declarations are allowed on paragraphs/headings |
| Font size/family, colors, arbitrary styles/classes/data attributes | Original preserved, device read-only | Web schema has richer attributes. Import strips attributes outside the allowlist |
| Scripts, event handlers, frames, forms, active/foreign markup, unsafe links | Invalid/unsafe to render or edit; original still preserved as data | Never executed in saved-source view or backup. Explicit import drops active elements and unwraps unsafe links |
| Unknown nodes, marks, attributes or malformed legacy JSON | Original preserved, device read-only | No automatic text conversion; source backup remains available |
| Legacy plain text | Editable after safe conversion to text nodes | Literal `<` is text, not markup; opening alone does not save a converted version |
| Legacy TipTap JSON using the permitted structure | Editable; original JSON retained until a real edit | Node/mark/attribute checks plus actual TipTap schema validation. First edit saves HTML. Source backup retains the original JSON if no edit has occurred |

The allowlist is conservative. A familiar tag with an unknown attribute does not become editable merely because the device can display some of it. HTML is also checked for unsupported nesting (for example rich content inside code blocks). The browser checks the actual legacy JSON against its schema. An additional browser rejection leaves the source intact even if the native preflight accepted it.

## Boundaries and no-op behavior

1. Download/storage preserves each page's `Content` string and explicit `ContentFormat`. Sync identities, versions, account isolation, conflict handling and recovery remain unchanged.
2. `DeviceTextEditor` checks the native contract before mounting. The browser independently validates content before creating TipTap. Unsupported pages show escaped original source and an explanation; no partially parsed editor is offered.
3. `LocalEditorSession.Edit` rejects replacement of an unsupported original and rejects unsupported replacement HTML. This also guards AI/editor callbacks. Import append **and replace** use the same check, so import cannot overwrite a protected page.
4. Opening initializes editor version zero and emits no writing callback. Flushing an unchanged editor does not create an edit, change the content format, advance a local revision, or queue a writing upload. Tests exercise this through both real editor bundles and the real sync engine with an isolated fake transport.
5. Compatible pages can still be edited in documents containing a protected page. Unchanged pages are copied verbatim into sync payloads. No conversion of the whole document is performed.

Backend upload validation now accepts the same common tags/attributes (including headings 4–6, underline and code language). It permits an existing rich/legacy page to pass through unchanged only when page ID, exact content and format match the current owned document inside the expected-version transaction. New or modified content must pass the normal supported-HTML validation. Copying another document's IDs/content does not grant this exception. Ownership, paid entitlement, size/complexity limits, receipts and version checks remain in force. A device must never construct a lossy replacement and rely on the server to detect its intent; that is why the device editing/import guards are also required.

Editable content is preserved semantically, not promised byte-identical after an actual edit: TipTap normalizes equivalent tags, HTML escaping and default link attributes. Unsupported content retains the exact original string and format through storage, sync and backup. A backup decodes to that exact content; the surrounding JSON uses normal JSON escaping.

## Explicit import versus export

The file-picker import notice explains that unsupported formatting, images and active content are removed. This is an explicit UTF-8 TXT/HTML conversion, up to 5 MB; rich clipboard paste similarly falls back to plain text with a notice. Neither path sanitizes saved/cloud source on open.

HTML and text document export now refuse a document containing unsupported pages instead of silently flattening or stripping it. HTML export retains allowed headings, marks, ordered-list start, code language and link attributes. Text export intentionally keeps text and order, not formatting. Legacy JSON must be edited/saved into supported HTML for these reading formats, or backed up directly.

**Export original source backup** is available on a protected page; **Import and export → Lossless source backup** is available for all documents. It writes an inert `.source.json` version-1 local document envelope, including all pages, their original content/format and identity/sync metadata. It neither changes the document nor schedules an upload. The native picker labels it Source backup. Keep it for recovery or inspection in a text editor; it is not accepted by the ordinary TXT/HTML import UI. Restore a backup only through a deliberate recovery procedure with identity/account checks, not by importing it as prose.

## Extending the contract

Change the manifest, both validation implementations and fixtures together. Do not add a tag merely to make validation pass: first prove the device schema, commands and serialization retain its attributes and children. Rebuild both tracked bundles with `npm run build`. Test original-source backup, no-op opens, imports and supported web → device → web round trips. Richer font/color styles and a backup-restore UI remain future work.

## Desktop menu parity (2026-09-30)

The desktop toolbar includes the screenshot's formatting, table, local image, image URL, zoom and More controls. Undo/redo, feedback, documentation and document actions sit below it. Document actions provide writing width and an A4 paper view, save, import/export and publishing. The paper view is a writing layout; use Publishing for export pagination and PDF. Zoom changes display size without saving content.

Images must fit within the existing 500,000-character page sync limit; oversized insertions are rejected before changing writing. Embedded images reopen offline; remote images require access to their source. Deploy the updated shared content contract on the backend before syncing newly added rich formatting to an older server. Unknown formatting and unsafe sources remain protected.

Validation: desktop build, both editor bundles, 155 focused .NET tests, and browser editor checks cover formatting, tables, images, save locks, reopen, exports, preview and sync. Native Windows picker and live authenticated sync remain manual acceptance checks.

See [parity UAT evidence](desktop-client-parity-uat.md) for fresh checks and the distinction between real-editor checks, sync-engine tests and live authenticated sync acceptance.

## Targeted quality revisions (2026-10-03)

Prompt 1 parity repair (2026-10-05): targeted device previews and client
quality/consistency Apply now share `targeted-revision.ts`. Unchanged mixed marks
in sentence context survive; newly changed wording must have one unambiguous
source mark set. Minimal changes expand to whole words and Unicode graphemes.
Pure deletion may remove differently marked text without reassigning marks.
Embedded content, incompatible changed spans, block crossings, stale checked
ranges and partial words/graphemes refuse before dispatch. Complete result text
is checked. Client paragraph splits support one uniform paragraph with retained
attributes; client editor-appended unrelated structure is rejected. No content
schema or import/sync policy changes. Existing general selection and device
consistency preflight remain stricter; this does not widen every AI action to
mixed marks. Fresh evidence is in `artifacts/ai-parity-p01/` and the prompt-1 entry
in `desktopai-uat.md`.

The quality adapter captures exact ProseMirror text with double newlines between blocks and a single newline for hard breaks. Its selection, issue, highlight, Jump and preview ranges use this same UTF-16 mapping. General AI/consistency capture remains unchanged; its offsets cannot be reused for quality. Decorations do not alter stored HTML, editor version or selection, and are discarded when typing changes the document.

`validateQualityRange` checks source/range identity and complete words/Unicode before requesting generated text. Targeted quality replacement supports one text block with uniform inline marks. Mixed inline styles, cross-block spans and embedded content remain intact with an actionable manual-edit explanation. A supported phrase keeps its source marks, and a local paragraph split keeps block attributes/marks. Surrounding writing, tables/links, other pages and planning are preserved. Proposals are inserted as schema text rather than trusted provider HTML, and mutation requires explicit approval with durable recovery evidence.

See [desktop AI prompt 4 evidence](desktopai-uat.md) for real-editor fixtures, restart/Undo/Redo and remaining native/provider acceptance.

After a panel/history operation restores legacy text or JSON, workspace refresh passes its explicit format to `DeviceTextEditor.SetContentAsync`. The editor prepares that format through the same protected conversion as initial open, so legacy text containing tag-like characters remains inert. Refresh does not emit an authored edit; a format-only change also triggers the refresh.

## Canon-aware consistency ranges (2026-10-03)

`consistencyPlainText` prepares saved HTML, legacy text or legacy JSON with the device's actual schema and returns its exact single-newline text mapping. Section analysis joins the resulting pages with two newlines. This replaces regex-based anchor resolution in the desktop consistency workflow; quality's double-newline block mapping stays separate. The backend/provider now honor the same section text override so report offsets refer to the analyzed text.

Jump checks document/page identity, full current editor text and a verified range before selecting/scrolling/focusing. It never changes saved HTML or editor version. Verified original offsets may disambiguate identical passages; after a prior approved fix, remaining evidence must match uniquely again. Stale/missing/ambiguous evidence fails closed.

Before generation or direct review, the consistency preflight checks whole-word/Unicode boundaries, one text block and uniform inline marks. Cross-block, mixed-mark, embedded-content and multi-paragraph revisions require manual editing. Safe previews insert inert prose into the original schema, retaining the target's marks and other headings, attributes, links, tables and pages. Apply requires explicit approval and durable pre-save recovery; existing original-source backup and quality protections remain intact. The general older preview helper remains compatible; the current device panel uses the stricter `previewSafeConsistencyRevision` path.

## Page-mapped translation (2026-10-03)

`captureTranslation` prepares every saved HTML, legacy text or legacy JSON page through the device's real schema. Ordered text-node path markers capture the entire intended source, including empty pages. `previewTranslation` changes only those text nodes and serializes the retained schema tree; provider text never supplies markup or structural nodes. Headings, paragraphs, inline marks/links, lists, tables and hard breaks remain host-owned. Whole-scope translation rejects images/captions, inline/block code, HTML comments, unsupported structure/attributes, implicit text-node line breaks and anchored scene annotations before sending an AI request.

Shared version-1 translation contracts validate exact section/page/run ordering, complete output, target/language metadata, safe Unicode and boundary whitespace. The local Before snapshot retains the original format; legacy source is converted to supported HTML only for the approved translated result. Recovery/Undo restores the original explicit format through the existing editor refresh adapter. The selection translation adapter remains independently range-bound. Prompt 14 supplies checked web broader Apply through aggregate server persistence; old plain-text web proposals remain blocked.

See the prompt-5 entry in `docs/desktopai-uat.md` for current multi-page, rich-content, restart/Undo/Redo and both-bundle editor checks. These checks do not establish native or live provider language quality.

Prompt 14 exports the shared mapping functions from the web bundle and regenerates both tracked editor bundles. HTML capture now rejects images/code from the inert source template before schema parsing, including block images nested in paragraphs that the parser could otherwise discard. Web approved text is substituted into server-owned DOM text nodes and escaped by serialization; submitted/provider HTML never replaces page content. Exact original text-node boundaries must match captured runs. Unsupported/noncanonical boundaries refuse before provider execution with normalization/selection guidance. Empty pages remain in the graph. Per-page Original/Proposed review is text-only. Replacement preserves structural IDs and marks; duplication assigns new graph IDs once at commit and replays its receipt. Durable originals and terminal aggregate history remain separate from page-HTML Undo. Current evidence is appended under prompt 14 in `desktopai-uat.md`.

## Writing scopes and continuation (2026-10-03)

Writing selection capture uses the quality mapping: double newlines between blocks, one newline for hard breaks, and UTF-16 offsets. `qualityPlainText` prepares saved HTML/legacy JSON/legacy text through the same schema and must exactly match the active editor before requesting a revision. `validateQualityRange` and `previewQualityRevision` retain the supported uniform text-block range and insert provider output as inert text. Moving the reviewed selection or changing its source prevents Apply.

Complete section revision uses the same ordered page/text-node mapping as translation with its own strict `WritingStructure` contract and existing expand/tighten/change-tone/show-don't-tell endpoints. It changes text only inside retained schema nodes, includes empty pages and preserves all intended pages/formatting. Missing/reordered/duplicate markers, invalid Unicode, changed boundary whitespace, new text-node line breaks, wrong identities, unknown JSON fields and invalid prose are rejected. Whole-section structural edits, images/captions, code, comments and anchored annotations need a smaller supported scope or manual editing.

Continuation prepares the final saved page through the schema, retains every original node, and appends a new plain paragraph via `previewContinuation`. It supports retained rich blocks, tables/images and both legacy formats, including an empty final page. The new paragraph cannot supply HTML/structural nodes or exceed 20,000 characters. Shared normalization/echo/meta checks occur before preview. Durable version-3 aggregate recovery and scoped Undo/Redo restore the original explicit format and retain unrelated pages/planning. See prompt-6 evidence in `docs/desktopai-uat.md`; shipped-editor and synthetic controller/component checks do not establish native/provider prose quality.

## Scene coaching preservation (desktop AI prompt 7, 2026-10-03)

Field-scoped scene Apply owns only approved planning values. Complete saved section context is read through the shipped editor schema, including supported legacy JSON pages; Apply never rewrites those pages or converts their format. Existing unsupported-content preflight remains authoritative. Scene status, tags and entity references use typed validation and explicit field approval; omitted, empty or malformed values preserve authored data. New planning links use mapped cloud identities and exact current canon IDs, retaining case/underscores instead of applying prose normalization to identifiers.

Version-4 scene history records complete original/approved aggregate snapshots before mutation. Atomic save/reopen and target-scoped Undo/Redo preserve rich manuscript content, annotations, other scene fields and notes. Source/planning changes invalidate a pending proposal; metadata-only document acknowledgement remains tolerated. Interrupted approval can be finished without regeneration, and later changes require original-copy recovery. No editor source or tracked bundle changed in prompt 7; the existing 58 real editor checks were rerun successfully. Native and live-provider interaction remains a separate acceptance gate.

## Desktop AI prompt 8 — Synopsis-only application (2026-10-03)

Synopsis evaluation and guiding questions are readable, escaped feedback with no writing/planning Apply target. Field improvement uses the dedicated revision-checked synopsis endpoint and a typed, strictly separated field-text/commentary response. Its captured source includes all ten synopsis fields; scenes, editor selections and manuscript pages are not prerequisites or replacement targets. Unsupported/legacy manuscript content is retained byte-for-byte because this flow does not extract or mutate it.

Explicit Apply updates the reviewed field through shared synopsis planning, retaining rich manuscript content and other planning. Version-5 durable history stores immutable complete original/approved snapshots before saving, supports guarded offline completion of an interrupted approved save, and performs field-scoped Undo/Redo. Malformed, stale, wrong-field, wrong-account or expired results cannot become local changes. Existing writing, scene, consistency, quality and translation history behavior remains intact. No editor source/bundle regeneration was necessary; the shipped editor harness passed all 58 existing checks. Actual Windows events and provider prose quality remain open acceptance gates, documented in `docs/desktopai-uat.md`.
## Desktop AI onboarding preservation (2026-10-03)

The desktop guide never initializes writing in an existing editor. It creates a separate labelled practice project only after **Open practice project**, reserves its stable identity first, and opens it through the ordinary document/editor route. Creation retries validate its provenance marker and retain the complete existing snapshot, including deliberately empty writing. Skip, resume, restart, completion, account switching and onboarding-state migration do not change source content. Trashed/missing samples are retained or reported for recovery, never reseeded. Existing standalone/project documents remain available without signing in or connecting.

The guide points to the existing selection/preset writing controls and complete structured section revision, comparison, Apply/Dismiss and History Undo/Redo. No selection/formatting/anchor/undo algorithm or generated editor asset changed for prompt 12. The `onboarding_demo` request flag is added only to deliberate practice section tightening; it neither bypasses desktop gates nor supplies server scene eligibility. Shared guide text and provider output remain escaped/inert. Automated evidence includes reopening rich, Unicode and empty practice writing, state migration and creation-interruption recovery, actual Razor controls, production writing/history actions and the stored sample through shipped editor assets. Native/deployed acceptance is still open.

## Integrated AI preservation verification (2026-10-03, prompt 13)

The final unfiltered suite passed 1,434 tests; the actual shipped device/client editor harness passed all 60 checks. An equivalent stored practice/rich Unicode fixture in both editors at 1280×720 and 1920×1080 retained inert review, explicit Apply, supported marks/structure/run IDs and reopened content. No editor source/generated asset was changed. Actual native process relaunch and authenticated provider acceptance remain open; see [desktopai-gap-analysis.md](desktopai-gap-analysis.md).

Legacy web section/document translation Apply could flatten writing and write only first pages without a recoverable aggregate result. It is now blocked before mutation, including duplication and direct handler entry, with guidance to review/copy or use supported selection/desktop translation. This is a safety containment, not a complete web translation repair. The broader web adapter must consume the existing typed complete page/run contract and acquire checked-source, aggregate failure/recovery, reload and history semantics before Apply can be enabled. Current desktop structured translation and existing selection translation remain intact.

Normal non-demo web HTTP persistence was verified separately through production authorization/controllers/repositories and file-backed SQLite: restart retains rich Unicode, page/section IDs, untouched pages and authored scene metadata. This uses synthetic authentication in an isolated TestServer and does not establish deployed web browser or native persistence acceptance.

## Owned onboarding demo preservation (2026-10-04, prompt 15)

Local/guest practice remains separate from the explicit server-owned demo. Titles, an empty manuscript or `desktopAiPracticeGuide` metadata cannot adopt writing or grant an AI entitlement. Bootstrap retries preserve the reserved graph, including edited/empty/missing pages, and retain missing/trashed identities for recovery. A second device imports the same owned cloud graph with its own stable local ID; guest samples and unrelated rich manuscripts remain unchanged.

Run Demo uses the existing complete section shortening contract, save/source/sync/version checks, inert Original/Proposed review, explicit Apply/Dismiss and durable History Undo/Redo. It cannot apply a stale proposal or replace unsupported rich content. No editor algorithm or generated asset changes were required. Fresh verification passed 62 shipped-editor checks, 98 actual Razor fixture renders at both desktop sizes and four equivalent device/client Apply/reopen checks. Production SQLite and device tests cover stable bootstrap/progress/sync replay, ownership and completion isolation. Native/authenticated provider acceptance remains separate; the exact evidence and migration prerequisites are in the prompt 15 UAT and development documentation.

## Owned remote cover preservation (2026-10-04, prompt 16)

Materialization reads only the current owned saved cover reference or server provider result and stores validated static PNG bytes/provenance separately. Fetch, cache, preview, concept selection, cancellation and failed/expired/malformed responses do not rewrite manuscript pages, rich formats, structural IDs, scenes/synopsis or project cover metadata. Source/account/backend checks span network and persistence boundaries. A project cache supports sibling manuscripts without copying or retargeting authored content. Unknown/corrupt scoped asset files remain preserved with guidance.

Only the existing explicit **Save as project cover** updates cover metadata with the reviewed exact PNG, metadata CAS, prior remote-cover recovery and ordinary project sync. Original remote metadata remains recoverable, including when its signed reference expires; cached immutable bytes can still supply its preview. Offline publishing uses the matching account/project/reference/hash cache without a download or metadata update. HTML, DOCX image parts and EPUB image entries were checked against the exact validated bytes. The actual Publishing page also refuses a private snapshot after sign-out; physical native saving/PDF execution is a separate gate.

No editor algorithm or tracked generated bundle changed. Fresh integrated verification passed all 1,579 tests, 62 shipped-editor checks, 116 actual Razor fixture renders and four equivalent device/client Apply/reopen checks. Previous structured translation, onboarding, rich Unicode preservation and history/recovery checks remain in that unfiltered suite. Remote JPEG/WebP/GIF/SVG conversion is unsupported and leaves prior content intact. See the dated prompt-16 UAT for the synthetic network/provider/authentication seams and remaining native/live gates.

## Prompt 17 — saved outline preservation (2026-10-04)

Canonical saved outline extraction carries stable IDs/order and escaped Unicode titles independently of authored rich prose and planning notes. It does not mutate sections, pages or nodes. Source changes invalidate approval before Apply; ordinary writing history, recovery, presets and continuation limits remain in place. Both shipped editor bundles are unchanged. Fresh evidence: 1,615 Release tests passed, 62 real editor checks, 116 Razor renders and four equivalent Apply/reopen comparisons. Native and live provider meaning remain separate gates.

## Prompt 18 — cover edits preserve manuscript and project source (2026-10-04)

Cover edits use an owned immutable static PNG and bounded operation/brief data. They do not send manuscript prose or modify writing, section/page/run IDs, planning or saved cover during generation/review. Shared Original/Proposed comparison and concept selection remain inert until explicit Save. Both studios retain metadata CAS, prior-cover recovery and account/backend/source guards. Web approved-save receipts reconcile after restart/lost acknowledgement and cannot substitute a different operation/result; device offline previews use exact validated cached bytes and local Save remains distinct from cloud sync confirmation. Both shipped editor bundles remain unchanged. Provider visual quality, native interaction and physical publishing acceptance remain separate checks.

## Prompt 19 — checked web writing and planning preservation (2026-10-04)

Web generation now starts after confirmed current editor/card/synopsis saves and carries the owned saved writing/planning/canon fingerprint. Source changes without sync metadata changes invalidate review/Apply; unchanged-content sync acknowledgements do not. Account/backend/target, response identity, date and cancellation checks span generation and review. Provider markup remains inert readable text. Normal writing and editor Undo remain available independently of AI connectivity.

Complete section expand/tighten/change-tone/show-don't-tell and reusable section prompts use shared typed writing structure and the real shipped web editor's saved page/run mapping. Every page is preflighted before any write; supported heading/strong/emphasis, whitespace, blank runs and Unicode survive preview, explicit Apply and reopen. Unsupported later-page images/nodes refuse the entire proposal without modifying earlier pages. Multi-page Apply reuses prompt-14 atomic durable approved intent/receipts and original-copy recovery, with `IsWriting` preserving language metadata. No whole-manuscript flattening or scene aggregate fallback was added. Section summaries without a safe mapping and section tools without a typed reusable prompt explain the limitation.

Selection/continuation, translation, quality and individual consistency revisions use scoped rich replacement and checked Save acknowledgement before marking Applied. A failed/missing checked acknowledgement retains the local draft and proposal with reload/retry guidance. Legacy quality reports can be read but cannot authorize a new Apply. Scene quality needs a linked manuscript page. Consistency anchors retain the existing web current-editor-page scope. AI history page Undo/Redo adds expected saved HTML/source checks; ordinary editor Undo remains intact. Scene cloud AI-history replay remains explicitly unavailable.

Scene coaching applies only reviewed narrative fields. Expected normalized card fingerprint and owned source CAS protect both normal and AI card saves; authored tags/status/entity links and other fields are retained. Synopsis modes preserve all ten fields except the explicitly reviewed field, with expected saved snapshot/source checks. The storyboard inspector retains place/time/tags/references on ordinary Save and does not write unrelated title/notes/metadata as part of AI field Apply. All four storyboard actions retain explicit scene creation and proposal-specific source/time checks.

Fresh integrated evidence: **1,773 unfiltered Release passes, 64 real shipped-editor checks, 142 actual Razor renders at both requested desktop sizes and four equivalent device/client Apply/reopen comparisons**. The two new editor cases cover complete rich mapping and unsupported aggregate preflight. No editor source algorithm or generated bundle changed in prompt 19; the harness uses both existing shipped bundles. These checks establish tested mapping/persistence and inert review, not native interaction, full signed-in shell or live provider meaning. See the dated prompt-19 UAT and per-flow gap table; durable general web applied-event delivery remains prompt 20.
## Prompt 20 — persistence-derived history and rich recovery (2026-10-04)

Web AI Apply now retains immutable rich original/proposed evidence before persistence in an account/backend-scoped durable browser outbox. Normal checked Page/SceneContent saves atomically commit exact approved HTML and a typed operation receipt. Lost acknowledgement/reload inspects that receipt before reporting; reporting rejection cannot undo successfully saved writing. Uncommitted Apply intent remains inspectable without claiming Applied or automatically reapplying it.

Cloud Undo/Redo uses only Page/SceneContent receipts and checks current owned source and exact expected saved HTML before reversing a transition. A later authored edit refuses replay, including duplicate PUTs, while historical receipt delivery can still reconcile the earlier valid save. Existing editor Undo, formatting, Unicode, structural/run identities and ordinary save paths remain intact.

Scene/section-card receipts retain normalized planning and the approved typed request/actual typed response; they never authorize page HTML replay. Aggregate writing/translation retains prompt-14 operation references and explicit original-copy recovery. Unrelated planning fields and other pages remain protected by the existing controllers and aggregate contracts. Provider content in history details is rendered as inert text. The new outbox JavaScript is an unbundled static module; no rich-editor source algorithm or generated editor bundle changed.

## Prompt 21 — iOS identity preserves writing and approval boundaries (2026-10-04)

iOS native authentication now uses the shared device services through a platform adapter. System sign-in, native Keychain credentials and scoped selected-account state remain outside the manuscript WebView/local document schema. Authentication never replaces authored HTML, IDs, planning or recovery copies. Guest/offline writing remains usable when public auth settings are absent or invalid; changing account does not delete documents.

Sign-out/revocation immediately invalidate account generation and cancel pending acquisitions. An ignored cancellation or late callback cannot restore an old identity; existing AI Apply guards reject proposals captured before account switch, expiry, revocation or host destruction. Silent renewal for the same account leaves generation unchanged. Native resume does not duplicate an interactive browser acquisition, and normal backgrounding during system authentication is retained. Backend/environment-scoped secure selection prevents guessing a different cached account. Existing canon/history/cover source/account isolation, rich Apply, autosave and local recovery remain intact.

The native adapter and lifecycle are tested through native-platform seams and compiled for the available iOS managed target. Actual Keychain/signing, callback presentation, native Apply/reopen and device acceptance require an authorized Mac/device setup; see prompt-21 UAT and development prerequisites. No editor algorithm or generated editor asset changed.

## Prompts 22–23 — page identity and lifecycle acceptance (2026-10-04)

The production manuscript editor now keeps every real page ID and opens one page at a time. A keyboard-accessible page picker saves the active editor before switching; failed/offline saves retain that page and its draft with readable guidance. Preview alone joins all section pages. Version restoration updates its matching cached page. Opening a multi-page section no longer concatenates its entire writing under the first page ID.

Save lifecycle calls defer until after the current Blazor render, coalesce repeated events and cancel on editor replacement. Only actual window blur requests a lifecycle save. Identical saved content settles a dirty save coordinator before guarded navigation or AI actions. Destroying a web editor disables pagination interop, clears queued timers and cancels resize/reflow callbacks before they can access a destroyed TipTap view. The TypeScript source was changed and both tracked editor bundles regenerated using `npm run build`.

Fresh checks: 1,863 unfiltered Release passes, 71 shipped-editor checks (including queued resize/scroll followed by destroy), 156 actual Razor renders and four device/client Apply/reopen comparisons. The complete LocalDev web shell additionally passes 16 navigation/focus, rewrite review/Dismiss/Apply, IndexedDB report interruption/reload/retry, Undo/Redo, offline-draft recovery and disk-download/source-metadata checks. ZIP/XML inspection confirms both DOCX and EPUB retain all sections, Unicode and exact cover bytes. This is synthetic-provider/local-authentication evidence; independent reader rendering, native dialogs, close/relaunch and iOS touch/accessibility remain open.

Production local-store integration checks exercise document envelope versions 1–3 plus version-1 guide migration, interrupted write/retry, exact backups, unchanged rich writing/IDs, queued presets/history, cached canon/covers and immutable approved recovery. They do not establish Windows package upgrade or iOS container migration acceptance. See the dated UAT and final release checklist for those gates.

## AI parity prompt 2 — Request cancellation and save safety (2026-10-05)

Client writing, targeted quality and consistency requests now own a token through
checked preparation, generation and bounded retry. Cancellation rejects late
proposal/preview retention and cannot turn a cancelled result into an applicable
change. Supported editor reads stop their continuation on cancellation; request
tokens do not reach the authored save boundary. A save already started before
generation completes independently, preserving its exact rich draft. Explicit
quality/consistency Apply uses the reviewed candidate and keeps existing checked
source, formatting-safe transaction, confirmed save and recovery rules.

There is no editor schema, content envelope, rich-replacement algorithm or
generated JS asset change. The shipped-editor probe still passes all 109 cases;
the owned save/cancellation/late-result boundaries have separate production-handler
tests. Structured translation keeps its existing cancellation and atomic approval
flow. See dated prompt-2 UAT for available evidence and native/provider gates.

## AI parity prompt 5 — Consistency page identity (2026-10-05)

Section checks join the saved pages in order with two newlines, preserving page IDs,
rich HTML and checked source revisions. Limits remain 1–100,000 plain characters,
1–1,000 unique pages and two million HTML characters; oversized writing is refused
without truncation. Full quoted evidence must occur exactly once within one
captured page. An anchor cannot authorize a duplicate or span multiple pages;
word, surrogate and combining-character cuts are refused or expanded to the full
verified evidence. Highlights and Jump use mapped page-local offsets.

Review and Apply validate account/backend, document/section, page membership/order,
source fingerprint and exact active-editor content. Approved replacement uses the
existing formatting-safe range helper and a checked save for the actual target
page. Unsupported rich spans are refused without replacement. Source drift just
before persistence is rejected by the backend; a failed save retains the reviewed
rich draft through the existing page recovery coordinator. Other pages are not
rewritten. Intentional decisions and cross-scene evidence retain their existing
storage/navigation policy. No editor schema, rich-content envelope or generated
editor asset changes were necessary; both shipped editor bundles pass 109 checks.

## AI parity prompt 6 — Atomic explained style edits (2026-10-05)

The client captures exact normalized editor HTML plus current page/selection
plain text, page-local offsets and checked saved source. Its style projection
matches saved plain-text separators around empty paragraphs and inline breaks,
while the actual rich document retains those structures. A collapsed selection
cannot use cached text. The backend verifies the exact owned saved range and
supported style goal before generation; Apply also revalidates saved outline,
account/backend, page identity, source revision and exact editor HTML.

All explained edits must be unique, bounded and non-overlapping. Each uses the
existing minimal-change formatting-safe transaction helper on the captured
document; descending order preserves offsets. Only after every selected edit
and the complete resulting document validate does a single transaction dispatch.
Untouched marks and blocks survive. Unsupported changed spans, embedded content,
paragraph-changing replacements and unsafe Unicode boundaries refuse without
partial mutation. Provider HTML remains inert text. Normalized rich HTML that
cannot be mapped to the saved text contract is refused rather than approximated.

One approved batch produces one checked rich save with durable history intent.
Refusal clears uncommitted AI metadata when content is unchanged; uncertain/failed
saves retain intent and the exact approved recovery draft. Existing page-history
Undo restores the saved original. Cancellation applies to generation/preparation
and preview, while approved persistence keeps its durable boundary. No content
schema/envelope or device editor behavior changes were introduced.

The rebuilt web bundle passes **23 new style + 109 existing shipped-editor
checks**, including empty paragraphs, inline breaks, rich partial/all approval,
desktop preview reference and single editor Undo. Separate production-handler
tests prove checked persistence/restart/history/recovery. Evidence and remaining
signed-in/native/provider gates are in the dated prompt-6 UAT entry.

## AI parity prompt 7 — Recommendation output targets (2026-10-05)

Both hosts use the actual intent catalog templates with checked complete section
run mappings. Section revisions preserve ordered page/run identity, boundary
whitespace, block structure and rich marks. Improve Hook may change only the
first root paragraph of the first page; later writing remains exact. Unsupported
heading/embedded/empty openings refuse before generation. Add Structure uses
signpost wording within existing runs, rather than creating new blocks.

Continue Scene and Expand Idea return one new paragraph appended at the verified
section end. Desktop preserves other pages/content formats; client requires the
last page active and uses its existing checked rich append/save/page-history
path. Leading source echoes and repeated-only output are refused. Generate
Headlines and Summarize Clearly produce strict plain-text items for selection
and copy. Neither can replace manuscript content, invoke generic replacement
operations or create a local manuscript recovery entry. Provider-like markup in
rendered candidates and appended prose remains inert text.

Actual saved HTML is verified against every recommendation's run map on the
backend. Hosts recheck source/account/outline before approval or copy; malformed,
stale or cancelled output cannot change authored content. Existing rich section
atomic saves/original-copy recovery and local scoped snapshots/Undo/Redo remain
in use. The optional immutable local history recommendation identity distinguishes
these section actions from reusable presets; existing records remain readable.
Client aggregate section Undo/Redo remains prompt 9.

No schema/envelope, editor algorithm or generated editor asset changes were
needed. Both shipped bundles pass **109 existing + 8 recommendation checks**;
separate production-handler tests cover checked persistence and recovery.
Evidence and remaining signed-in/native/provider gates are in the dated prompt-7
UAT entry and `artifacts/ai-parity-p07/verification.ps1`.

## AI parity prompts 8–9 — exact scoped recovery (2026-10-06)

New client planning Apply captures raw persisted field values, not provider text
or normalized UI defaults. Undo/Redo restores only fields actually changed by the
approved action, including affected linked card mirrors. Unrelated later planning
survives; changed affected fields or deleted/moved targets refuse the whole restore.

New supported checked aggregate replace actions retain exact before/after page
HTML, scoped identity/order and actual language/mirror changes on the same owned
backend. The mapping does not add/remove/reorder pages, blocks or runs. Recovery
checks scoped page membership/order and every affected value before mutation,
then commits all content and its history outcome together. Mid-save/restore failure
cannot leave a partially committed manuscript. Unchanged pages/fields are not
rewritten; edits to an affected content mirror refuse rather than overwriting it.
Created mirrors are restored only after whole-record checks. Separate translated
copies are retained without destructive Undo, including later edits; original-copy
recovery remains idempotent and available. Legacy comparisons never acquire
fabricated rich snapshots or device-local IDs.

The reachable history controls display raw comparisons as encoded text, save drafts
under an editing lock and reload current page HTML after success. The ordinary
browser outbox still handles report delivery after the atomic backend outcome.
No editor schema, envelope, algorithm or shipped asset changed. Both shipped
bundles pass 109 existing + 8 recommendation checks. New scoped persistence,
compiled callback, browser and migration evidence is in the dated prompts 8–9 UAT
and `artifacts/ai-parity-p09/verification.ps1`; full signed-in/native/live-provider
acceptance remains independent.

## AI parity prompt 10 — Source-bound quality dismissal (2026-10-06)

Dismissal and Restore write a separate decision journal and existing server dismissal
rows. They never write, flatten, sanitize or reserialize manuscript HTML, change rich
editor marks/schema, or create an applied AI-history entry. Source identity uses the
existing quality plain-text mapping, exact occurrence offsets, shared catalog revision
and ordered glossary. The actual panel verifies saved source and recaptures current
editor HTML/text before Dismiss/Restore. Changed text/context is eligible again;
selection identities stay local and cannot be treated as full-page server offsets.

The shared review renders excerpts as encoded text, including HTML-shaped passages.
Production editor code and both shipped bundles remain untouched. Scoped synthetic
tests assert byte-preserved authored documents through dismissal, retry, Restore,
account changes and synchronization failures. No editor algorithm changed, so this
prompt does not regenerate or rerun unrelated editor bundles. Native/full-shell and
deployed acceptance remain separate gates in the dated prompt-10 UAT entry.

## AI parity prompt 11 — Read-only targeted preflight (2026-10-06)

Client targeted generation/retry now checks the exact quality-finding anchor,
checked page/source and resolved rich document range before generation and between
attempts. `validateTargetedRevisionRange` extracts the existing Apply range guards
from `targeted-revision.ts`: one text block, exact expected text, complete Unicode
and word boundaries, no embedded leaves or line-break crossings. The client patch
exposes a boolean preflight without constructing or dispatching a transaction.
Apply still rejects unchanged output and retains the shared minimal-change,
mixed-changed-mark and complete-result guards. Device preflight retains its
existing targeted-range validator; schema-specific guards remain in their hosts.

Both shipped bundles were regenerated from source. The real-browser harness checks
plain and marked passages, stale anchors, block images, block crossings and hard
breaks in both hosts, asserting unchanged HTML after every preflight. All 109
existing editor checks and 12 added preflight checks pass. A first browser probe
caught that Apply intentionally rejects a no-op transaction; separating its range
guards corrected that integration before final verification. An inline-image test
fixture was corrected to use the shipped schema's block image representation.

An invalid first/second result or cancelled/source-changed request leaves authored
content untouched and offers no invalid Apply. Only a validated result proceeds
through explicit review, the existing checked save and recovery. This is synthetic
shipped-editor evidence, not signed-in native or live-provider acceptance.

## AI parity prompt 12 — Preset continuity presentation (2026-10-06)

This prompt changes preset-library help, storage-origin labels and transfer-status
presentation. Reading help, refreshing presets and transferring/importing them never
change manuscript HTML, editor selection, schema or Apply/history contracts. The
synthetic acceptance journey compares complete page DTOs before/after local edits,
explicit cloud updates, actual client Refresh and a separate desktop Refresh/Import;
the manuscript remains identical. Existing preset Run/approval checks also pass.

Origin and transfer status are optional presentation fields, not new persistence
metadata or a reason to disable supported presets. Existing templates, parameters
and conflict comparisons continue rendering as encoded text. No editor source or
shipped bundle changed, so unrelated editor asset rebuilds were not repeated.
Both bundles remain byte-identical to the prompt-12 baseline. The manual workflow,
available checks and remaining full-shell/native gates are in the dated prompt-12
UAT entry. No provider call is needed to establish preset transfer continuity.

## AI parity prompt 13 — Tone menu/request contract (2026-10-06)

Shared supported tone descriptors now include Executive and supply the client preset
catalog and shared desktop selectors. The desktop Executive rewrite sends the same
tone, length, preserve-terms and instruction parameters as the existing client
Executive preset. Existing preset labels/wire values and stored custom tone text
remain compatible. Changing a setting and requesting a preview still leave authored
content unchanged until explicit approval through the existing checked writing flow.

Actual client handler checks cover all six tones with a synthetic provider and retain
unchanged manuscript pages; the desktop panel check exercises its setting callback,
request construction and review. Existing writing suites cover stale/cancelled
requests, checked Apply, interrupted persistence and Undo/Redo after restart. Both
shipped editor bundles remain byte-identical to the prompt-13 baseline. Editor
algorithms, schemas and assets were unchanged, so unrelated editor asset builds were
not repeated. Browser snapshots establish menu layout/keyboard behavior only; full
authenticated client UI, native loading and live prose quality remain separate gates.

### AI parity prompt 14 integrated editor acceptance — 2026-10-06

Fresh runs of `verify-targeted-revisions.mjs`, `style-quality-editor.mjs`,
`recommended-writing-editor.mjs` and `targeted-retry-editor.mjs` exercised both
shipped bundles: **109 common regressions plus 44 additional focused checks**, all
passing. The common suite ran in each harness and is counted once. Evidence is in
`artifacts/ai-parity-p14/editor`; no editor source/schema/asset changed in prompt 14.
The complete sentence clock-to-chime replacement preserves the unchanged bold word;
style subset, recommendation/list insertion and retry/read-only/stale/embedded-content
checks retain their existing formatting and refusal contracts.

Actual Development WASM selection rewrite also preserved rich formatting through
checked preview, explicit Apply, interrupted history delivery, retry, Undo, Redo and
reopen, with unrelated saved pages compared exactly. Scene recovery testing found
and fixed a legacy-purpose projection problem in the C# save path, independent of
the editor algorithms. Unchanged scene preflush now skips saves, and unchanged
role/intent retain raw legacy purpose during unrelated manual saves.

**Remaining P14-N02:** merely visiting a saved page ending in a list can append
`<p></p>` through StarterKit trailing-node insertion and autosave. Reproduce by
opening the synthetic second page, navigating away without typing and comparing
saved HTML. Authored text, marks and list items remained intact in this case;
byte-identical navigation preservation is not established. Initial and fresh
navigation evidence are retained separately from exact pre-request AI snapshots.
Follow-up must distinguish initialization/normalization from writer edits and test
list/table-ended pages, autosave and reopen in both hosts. Native loading, external
provider quality and deployed/package acceptance remain pending.
