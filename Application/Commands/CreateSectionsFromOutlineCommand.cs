using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using WriterApp.Application.Synopsis;
using WriterApp.Domain.Documents;

namespace WriterApp.Application.Commands
{
    public sealed class CreateSectionsFromOutlineCommand : DocumentEditCommand
    {
        private readonly IReadOnlyList<OutlineItemDraft> _items;
        private List<Section>? _previousSections;
        private Guid _chapterId;
        private bool _hasExecuted;

        public CreateSectionsFromOutlineCommand(Guid provenanceSectionId, IReadOnlyList<OutlineItemDraft> items)
            : base(provenanceSectionId, EditOrigin.User)
        {
            _items = items ?? throw new ArgumentNullException(nameof(items));
        }

        public override string Name => "CreateSectionsFromOutline";

        public override void Execute(Document document)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (_items.Count == 0)
            {
                return;
            }

            Chapter chapter = ResolveChapter(document);
            _chapterId = chapter.ChapterId;
            _previousSections = new List<Section>(chapter.Sections);

            int nextOrder = chapter.Sections.Count == 0 ? 0 : chapter.Sections.Max(section => section.Order) + 1;
            List<Section> updated = new(chapter.Sections);
            foreach (OutlineItemDraft item in _items)
            {
                string title = string.IsNullOrWhiteSpace(item.Title)
                    ? $"Outline Item {item.Index}"
                    : item.Title.Trim();

                updated.Add(new Section
                {
                    SectionId = Guid.NewGuid(),
                    Order = nextOrder++,
                    Title = title,
                    Kind = SectionKind.Section,
                    Content = new SectionContent
                    {
                        Format = "html",
                        Value = BuildSectionHtml(item)
                    },
                    Notes = item.Notes ?? string.Empty,
                    CreatedUtc = DateTime.UtcNow,
                    ModifiedUtc = DateTime.UtcNow
                });
            }

            chapter.Sections.Clear();
            chapter.Sections.AddRange(updated);
            _hasExecuted = true;
        }

        public override void Undo(Document document)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (!_hasExecuted || _previousSections is null)
            {
                throw new InvalidOperationException("Command has not been executed.");
            }

            Chapter chapter = document.Chapters.FirstOrDefault(item => item.ChapterId == _chapterId)
                ?? ResolveChapter(document);
            chapter.Sections.Clear();
            chapter.Sections.AddRange(_previousSections);
        }

        private static Chapter ResolveChapter(Document document)
        {
            if (document.Chapters.Count > 0)
            {
                return document.Chapters[0];
            }

            Chapter chapter = new()
            {
                ChapterId = Guid.NewGuid(),
                Order = 0,
                Title = "Draft",
                Sections = new List<Section>()
            };
            document.Chapters.Add(chapter);
            return chapter;
        }

        private static string BuildSectionHtml(OutlineItemDraft item)
        {
            StringBuilder builder = new();
            if (!string.IsNullOrWhiteSpace(item.Summary))
            {
                builder.Append("<p>");
                builder.Append(WebUtility.HtmlEncode(item.Summary.Trim()));
                builder.Append("</p>");
            }

            if (item.Beats is not null && item.Beats.Count > 0)
            {
                builder.Append("<ul>");
                foreach (string beat in item.Beats.Where(entry => !string.IsNullOrWhiteSpace(entry)))
                {
                    builder.Append("<li>");
                    builder.Append(WebUtility.HtmlEncode(beat.Trim()));
                    builder.Append("</li>");
                }

                builder.Append("</ul>");
            }

            if (!string.IsNullOrWhiteSpace(item.Pov))
            {
                builder.Append("<p><strong>POV:</strong> ");
                builder.Append(WebUtility.HtmlEncode(item.Pov.Trim()));
                builder.Append("</p>");
            }

            return builder.Length == 0 ? "<p></p>" : builder.ToString();
        }
    }
}
