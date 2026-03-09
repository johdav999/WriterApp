# Prosa Feature Summary

## Product overview

Prosa is currently a writing application with two strong cores:

- A manuscript editor for documents, sections, pages, synopsis, export, and AI-assisted revision.
- A project workspace for parts, chapters, scenes, manuscript routing, progress tracking, and scene metadata.

It already includes meaningful subscription, billing, search, admin, and AI infrastructure rather than only UI mockups.

## Top feature categories

- Writing and editing
  - Rich-text editor, section/page management, images, tables, formatting, autosave, version history, annotations, notes
- Planning and structure
  - Projects, navigator tree, parts/chapters/scenes, outline templates, synopsis, scene cards
- AI assistance
  - Rewrite, translation, next-paragraph generation, synopsis coaching, scene suggestions, continuity canon and checks, prompt presets, AI history
- Export and publishing
  - HTML, Markdown, DOCX, EPUB, preview, print, export templates, export presets, project defaults
- Monetization and operations
  - Free/Standard/Professional plans, quota enforcement, Stripe checkout/portal/webhooks, admin overrides, audit, user admin

## Implemented vs partial areas

- Clearly implemented
  - Auth gating and onboarding
  - Documents/projects CRUD
  - Project navigator and manuscript routing
  - Rich editor and save flow
  - Synopsis editing and synopsis AI tools
  - Search
  - Export dialog, preview, templates, presets
  - Stripe-backed billing flows
  - Admin user management and audit

- Partial / flag-gated / backend-first
  - Revise-tool AI families behind flags
  - Prompt Library custom transform execution
  - Continuity fix application behind separate flag
  - Outline board / outline undo-related backend capability
  - Scene annotations / scene quality / scene versions APIs without clear current UI
  - Glossary API without clear current UI
  - EPUB polish and Markdown export fidelity
  - Cover image generation as a productized user feature

## Notable strengths

- The app is not only CRUD: it has strong workflow depth across writing, planning, revision history, and export.
- AI support is broad and backed by quota, history, and entitlement infrastructure.
- Project workflow is more mature than a simple folder tree; it includes goals, sessions, progress, and scene metadata.
- Admin and billing tooling are already operationally meaningful.

## Notable gaps

- The repo still has overlapping old and new UI implementations.
- There is route duplication around `/documents`.
- Product naming is inconsistent between Prosa and WriterApp.
- Some backend capability is ahead of the current visible UI.
- Subscription defaults and seeded entitlements are not fully aligned.

## Immediate documentation next step

Turn the inventory into maintained product documentation by:

1. Choosing the canonical UI implementation and removing legacy ambiguity.
2. Creating a small feature registry with route, flag, maturity, and owner.
3. Splitting public product docs into:
   - End-user features
   - Admin/ops features
   - AI capability matrix
   - Plan/entitlement matrix
