# Editor content compatibility

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
