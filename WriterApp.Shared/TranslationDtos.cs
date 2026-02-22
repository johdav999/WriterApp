using System;
using System.Collections.Generic;

namespace WriterApp.Application.Documents
{
    public sealed record DocumentTranslationLinkDto(
        Guid DocumentId,
        string Title,
        string? LanguageCode,
        Guid TranslationGroupId);

    public sealed record SectionTranslationLinkDto(
        Guid SectionId,
        Guid DocumentId,
        string Title,
        string? LanguageCode,
        Guid TranslationGroupId);

    public sealed record TranslationDuplicateSectionRequest(
        string Content,
        string? TargetLanguage,
        string? SourceLanguage,
        string? Title = null);

    public sealed record TranslationDuplicateSectionResponse(
        SectionDto Section,
        Guid PageId);

    public sealed record TranslatedSectionPayload(
        Guid SectionId,
        string Content,
        string? Title = null);

    public sealed record TranslationDuplicateDocumentRequest(
        string? Title,
        string? TargetLanguage,
        string? SourceLanguage,
        IReadOnlyList<TranslatedSectionPayload> Sections);

    public sealed record TranslationDuplicateDocumentResponse(
        DocumentDetailDto Document,
        Guid? DefaultSectionId,
        Guid? DefaultPageId);
}
