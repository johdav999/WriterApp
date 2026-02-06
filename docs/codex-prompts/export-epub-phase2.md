# PROMPT 2 — EPUB EXPORT (PHASE 2)

You are working in WriterApp (Blazor + server API). Add EPUB export as a second-phase export format, building on the DOCX/HTML export pipeline with a controlled, incremental implementation.

Business goal
- EPUB export unlocks wide ebook distribution. Ship a compatible, standards-compliant EPUB 3 with a stable core feature set.

Scope (Phase 2)
- Map these constructs:
  1. Headings: H1 / H2 / H3
  2. Paragraphs
  3. Inline marks: bold / italic / underline
  4. Lists: bullet + ordered (nested lists supported at least 2 levels if feasible)
  5. Chapter/section breaks as separate XHTML files
  6. Table of contents (nav.xhtml + NCX for compatibility)
- Images: if present, include as resources; if not supported yet, stub with TODOs + safe fallback.

Constraints
- Do not break existing HTML/Markdown/PDF/DOCX exports.
- Add behind a feature flag: `Exports:EpubEnabled` (default false).
- Keep implementation server-side so output is deterministic.
- “Good enough” fidelity: correct structure, consistent styling, readable in common readers (Apple Books, Calibre, Kindle via conversion).

Architecture requirements
1) Add `epub` as a supported export format in the existing export flow:
- UI: export format dropdown includes EPUB only if feature flag enabled.
- API: accept `format=epub` and return `application/epub+zip`.
2) Implement a new exporter module (server):
- `EpubExportService` or similar, invoked from existing export orchestrator.
- Use a maintained .NET library for EPUB creation if available; if not, implement a minimal EPUB 3 writer (zip packaging, OPF, nav.xhtml, NCX, content XHTML) with clear TODOs.
3) Content pipeline:
- Reuse the export AST if available; otherwise implement a minimal adapter from sections to “chapters”.
- Generate one XHTML file per section (or per chapter rule) with stable ids.
- Apply basic CSS (reuse existing export CSS where feasible, or include a minimal stylesheet scoped to EPUB).
4) Chapter rules:
- Honor existing export settings for “Start each H1 on new page” and “Start each section on new page” by splitting chapters accordingly.
- Avoid empty first chapter or duplicate breaks.

Deliverables
- Files added/changed with minimal diffs:
  - Server export handler updated to support epub.
  - New epub exporter class(es).
  - UI: add EPUB option gated by flag.
  - Update export preset/template model only if needed; prefer reusing existing settings.
- Tests:
  - Unit tests for packaging (mimetype file first and uncompressed, OPF contains manifest + spine).
  - Unit tests for mapping (headings/paragraphs/marks/lists appear in chapter XHTML).
  - Integration test that calls export endpoint and validates the .epub can be opened (at minimum: zip structure + required files present).
- Logging:
  - Add export trace log line: format, scope, sections count, bytes length.
- Documentation:
  - Add a short note in the user guide or release notes: EPUB supports headings/paragraphs/basic formatting/lists/TOC; images limited or deferred.

Acceptance criteria
- Export produces a valid EPUB 3 file that opens in Apple Books or Calibre.
- H1/H2/H3 map correctly to heading elements and TOC entries.
- Bold/italic/underline appear correctly.
- Lists render correctly for basic nesting.
- Chapter rules split content into appropriate XHTML files.
- The `.epub` file passes a basic structural validation (mimetype stored uncompressed, required files present).

Implement now. Provide code changes with paths and brief rationale per file.
