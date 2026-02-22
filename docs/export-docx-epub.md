# DOCX/EPUB Export Notes

DOCX and EPUB exports are available behind feature flags:
- `Exports:DocxEnabled`
- `Exports:EpubEnabled`

Current support (MVP):
- Headings (H1/H2/H3)
- Paragraphs
- Bold / italic / underline
- Bullet + ordered lists (basic nesting)
- Chapter breaks based on export settings

Not yet supported:
- Images (exporters insert a safe placeholder or skip images)

These exporters are intended as a safe baseline and can be expanded over time.
