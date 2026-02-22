using System;
using System.Collections.Generic;

namespace WriterApp.Application.Importing
{
    public enum SectionImportMode
    {
        Replace,
        Append
    }

    public sealed record SectionImportOptions(
        bool NormalizeWhitespace,
        bool PreserveTxtLineBreaks);

    public sealed record SectionImportStats(
        int Paragraphs,
        int Headings,
        int Lists,
        int Characters);

    public sealed record SectionImportResult(
        string Html,
        SectionImportStats Stats,
        IReadOnlyList<string> Warnings,
        string Format);

    public sealed record SectionImportRequest(
        Guid TargetSectionId,
        SectionImportMode Mode,
        SectionImportOptions Options);
}
