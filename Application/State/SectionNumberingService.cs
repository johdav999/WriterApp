using System;
using System.Collections.Generic;
using System.Linq;
using WriterApp.Domain.Documents;

namespace WriterApp.Application.State
{
    public sealed class SectionNumberingService
    {
        public IReadOnlyDictionary<Guid, SectionNumberingInfo> BuildIndex(Document document)
        {
            Dictionary<Guid, SectionNumberingInfo> results = new();
            if (document is null)
            {
                return results;
            }

            int chapterNumber = 0;
            IEnumerable<Section> ordered = document.Chapters
                .OrderBy(chapter => chapter.Order)
                .SelectMany(chapter => chapter.Sections.OrderBy(section => section.Order));

            foreach (Section section in ordered)
            {
                if (section.Kind == SectionKind.Chapter
                    && section.IncludeInNumbering
                    && section.NumberingStyle != SectionNumberingStyle.None)
                {
                    chapterNumber += 1;
                    string number = FormatNumber(chapterNumber, section.NumberingStyle);
                    results[section.SectionId] = new SectionNumberingInfo(number);
                    continue;
                }

                results[section.SectionId] = SectionNumberingInfo.Unnumbered;
            }

            return results;
        }

        public string BuildHeading(Section section, string title, SectionNumberingInfo? info)
        {
            if (section is null)
            {
                return title ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                title = "Untitled section";
            }

            if (section.Kind == SectionKind.Chapter && info?.IsNumbered == true)
            {
                return $"Chapter {info.Number} - {title}";
            }

            return title;
        }

        private static string FormatNumber(int value, SectionNumberingStyle style)
        {
            return style switch
            {
                SectionNumberingStyle.Roman => ToRoman(value),
                _ => value.ToString()
            };
        }

        private static string ToRoman(int value)
        {
            if (value <= 0)
            {
                return value.ToString();
            }

            (int Value, string Numeral)[] map =
            {
                (1000, "M"),
                (900, "CM"),
                (500, "D"),
                (400, "CD"),
                (100, "C"),
                (90, "XC"),
                (50, "L"),
                (40, "XL"),
                (10, "X"),
                (9, "IX"),
                (5, "V"),
                (4, "IV"),
                (1, "I")
            };

            int remaining = value;
            System.Text.StringBuilder builder = new();
            for (int i = 0; i < map.Length && remaining > 0; i++)
            {
                while (remaining >= map[i].Value)
                {
                    builder.Append(map[i].Numeral);
                    remaining -= map[i].Value;
                }
            }

            return builder.ToString();
        }
    }

    public sealed record SectionNumberingInfo(string? Number)
    {
        public static SectionNumberingInfo Unnumbered { get; } = new(null);

        public bool IsNumbered => !string.IsNullOrWhiteSpace(Number);
    }
}
