# PROMPT 1 — DOCX EXPORT (PHASE 1)

You are working in WriterApp (Blazor + server API). Add DOCX export as a first-class export format with a “good enough” mapping, designed to be expanded later.

Business goal
- DOCX export is a major upgrade trigger. Ship an MVP quickly but safely.

Scope (Phase 1)
- Map these constructs:
  1. Headings: H1 / H2 / H3
  2. Paragraphs
  3. Inline marks: bold / italic / underline
  4. Lists: bullet + ordered (nested lists supported at least 2 levels if feasible)
  5. Page breaks for chapter rules:
     - “Start each H1 on new page”
     - “Start each section on new page”
- Ignore images for now (explicitly stub with TODOs + safe fallback)

Constraints
- Do not break existing HTML/Markdown/PDF exports.
- Add behind a feature flag: `Exports:DocxEnabled` (default false).
- Keep implementation server-side (preferred) so output is consistent and doesn’t rely on browser quirks.
- “Good enough” fidelity: correct structure and basic styling, not perfect typography.

Architecture requirements
1) Add `docx` as a supported export format in the existing export flow:
- UI: export format dropdown includes DOCX only if feature flag enabled.
- API: accept `format=docx` and return `application/vnd.openxmlformats-officedocument.wordprocessingml.document`.
2) Implement a new exporter module (server):
- `DocxExportService` or similar, invoked from existing export orchestrator.
- Use an established .NET library (prefer Open XML SDK). Avoid heavy/unmaintained dependencies.
3) Content pipeline:
- Convert internal page/section content to an intermediate “export AST” if one exists; otherwise implement a minimal adapter.
- From the intermediate structure, build a Word document:
  - Paragraph = Word paragraph
  - Heading = paragraph with Heading1/2/3 style
  - Bold/italic/underline = Run properties
  - Lists = numbering definitions (bulleted + ordered)
  - Page break = paragraph with break
4) Chapter rules:
- Apply page breaks BEFORE each chapter boundary based on the user’s export settings.
- Ensure breaks do not appear at the very start of the document (avoid blank first page).

Deliverables
- Files added/changed with minimal diffs:
  - Server export handler updated to support docx.
  - New docx exporter class(es).
  - UI: add DOCX option gated by flag.
  - Update export preset/template model only if needed; prefer reusing existing settings.
- Tests:
  - Unit tests for “mapping” (given a small document structure, docx has expected paragraphs/styles/runs).
  - A lightweight integration test that calls export endpoint and validates it returns a non-empty .docx with correct content markers.
- Logging:
  - Add export trace log line: format, scope, sections count, bytes length.
- Documentation:
  - Add a short note in the user guide or release notes: DOCX supports headings/paragraphs/basic formatting/lists/page breaks; images later.

Acceptance criteria
- Export produces a valid DOCX that opens in Word/LibreOffice.
- H1/H2/H3 map correctly to heading styles.
- Bold/italic/underline appear correctly.
- Lists render correctly for basic nesting.
- Chapter rules insert page breaks correctly.

Implement now. Provide code changes with paths and brief rationale per file.
