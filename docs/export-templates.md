# Export Templates

Export templates define page size, margins, typography, header/footer text, page numbering, and TOC behavior for exports. Templates are stored per-user and seeded the first time a user loads the export templates API.

## Presets
The following presets are seeded for each user (stored as user-owned rows):

- Manuscript (`manuscript`): 216x279mm, double-spaced, wider margins.
- Paperback 6x9 (`paperback_6x9`): 152x229mm, tighter margins, smaller font.
- A4 (`a4`): 210x297mm, normal margins.

## Header/Footer Tokens
Header and footer text supports simple token substitution. Current tokens:

- `{DocumentTitle}`
- `{SectionTitle}`
- `{Date}` (yyyy-MM-dd)
- `{PageNumber}`
- `{TotalPages}` (HTML export uses `?` placeholder today)

Tokens are optional; leave the field blank to suppress that segment.

## Page Numbering and TOC
- `PageNumbersEnabled` and `PageNumberStart` control numbering independently of header/footer text.
- `TocEnabled` and `TocDepth` define table-of-contents inclusion and depth.
