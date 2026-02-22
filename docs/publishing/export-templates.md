# Export Templates

Export templates define page size, margins, typography, header/footer text, page numbering, and TOC behavior used when exporting documents. Templates are stored per user and are applied to HTML and PDF (browser print) exports.

## What templates are
Templates capture print-ready layout settings for export. They are applied to the HTML export renderer, so preview, export, and print use the same output.

## Presets
WriterApp ships with the following presets (available in the UI and API):

- Manuscript (default)
  - 216x279mm, wide margins, double spaced
  - Header shows `{DocumentTitle}` and `{SectionTitle}`
- Paperback 6x9
  - 152x229mm, tighter margins, smaller font
  - Header centers `{DocumentTitle}`
- A4
  - 210x297mm, standard margins
  - Footer shows `{PageNumber}`

Presets are defined in code and used consistently by API seeding and the UI create-from-preset flow.

## Token syntax
Header and footer text supports simple token substitution. Supported tokens:

- `{DocumentTitle}`
- `{SectionTitle}`
- `{Date}` (yyyy-MM-dd)
- `{PageNumber}`
- `{TotalPages}` (placeholder only)

## Current limitations
- `{TotalPages}` is not known for HTML-only rendering and currently renders as `?`.
- PDF export is browser print of HTML. There is no server-side PDF generation.

## Future evolution
Later, this will evolve into server-side export jobs (HTML-to-PDF or other formats). The same templates and tokens will be used, but `TotalPages` can be computed reliably during a server render step.
