using System;
using System.Collections.Generic;
using WriterApp.Domain.Documents;

namespace WriterApp.Application.Commands
{
    public sealed class SetCoverImageCommand : DocumentEditCommand, IAiEditCommand
    {
        private readonly AiEditGroup _group;
        private readonly DocumentArtifact _artifact;
        private readonly string? _reason;
        private Guid? _previousCoverId;
        private List<DocumentArtifact>? _previousArtifacts;
        private bool _hasExecuted;

        public SetCoverImageCommand(Guid sectionId, DocumentArtifact artifact, AiEditGroup group, string? reason = null)
            : base(sectionId, EditOrigin.AI)
        {
            _artifact = artifact ?? throw new ArgumentNullException(nameof(artifact));
            _group = group ?? throw new ArgumentNullException(nameof(group));
            if (_group.SectionId != sectionId)
            {
                throw new ArgumentException("AI edit group section does not match command section.", nameof(group));
            }

            _reason = reason;
        }

        public override string Name => "SetCoverImage";

        public Guid AiEditGroupId => _group.GroupId;

        public string? AiEditGroupReason => _group.Reason ?? _reason;

        public override void Execute(Document document)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            _previousCoverId = document.CoverImageId;
            _previousArtifacts = new List<DocumentArtifact>(document.Artifacts);

            if (!document.Artifacts.Exists(existing => existing.ArtifactId == _artifact.ArtifactId))
            {
                document.Artifacts.Add(_artifact);
            }

            document.CoverImageId = _artifact.ArtifactId;
            MarkAppliedUtc();
            _hasExecuted = true;
        }

        public override void Undo(Document document)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (!_hasExecuted)
            {
                throw new InvalidOperationException("Command has not been executed.");
            }

            document.CoverImageId = _previousCoverId;
            document.Artifacts.Clear();
            if (_previousArtifacts is not null)
            {
                document.Artifacts.AddRange(_previousArtifacts);
            }
        }
    }
}
