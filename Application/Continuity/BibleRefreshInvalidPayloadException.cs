using System;

namespace WriterApp.Application.Continuity
{
    public sealed class BibleRefreshInvalidPayloadException : InvalidOperationException
    {
        public BibleRefreshInvalidPayloadException(
            BibleType bibleType,
            Guid documentId,
            string actionId,
            string failureReason,
            string responsePreview)
            : base($"{bibleType} bible refresh returned an invalid JSON payload.")
        {
            BibleType = bibleType;
            DocumentId = documentId;
            ActionId = actionId ?? string.Empty;
            FailureReason = failureReason ?? string.Empty;
            ResponsePreview = responsePreview ?? string.Empty;
        }

        public BibleType BibleType { get; }

        public Guid DocumentId { get; }

        public string ActionId { get; }

        public string FailureReason { get; }

        public string ResponsePreview { get; }
    }
}
