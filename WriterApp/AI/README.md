# WriterApp AI notes

## Request context fields
- AiRequestContext includes selection metadata (SelectionText, SelectionStart, SelectionLength) plus LanguageHint.
- RewriteSelectionAction fills ContainingParagraph, SurroundingBefore, and SurroundingAfter from the active section.
- DocumentTitle comes from document metadata when available.

## Rewrite inputs
Rewrite variants pass these inputs via AiRequest.Inputs:
- tone: Neutral | Formal | Casual | Executive | Friendly | Technical
- length: Shorter | Same | Longer
- preserve_terms: true | false

## Cover persistence
- Cover images are persisted in the document model.
- Document.CoverImageId references a stored DocumentArtifact in Document.Artifacts.
- SetCoverImageCommand stores the artifact and sets CoverImageId with undo/redo support.

## OpenAI provider
- Configure `WriterApp:AI:Providers:OpenAI` in `appsettings.Development.json`.
- Set `DefaultTextProviderId` / `DefaultImageProviderId` to `openai` to route to the OpenAI provider.
