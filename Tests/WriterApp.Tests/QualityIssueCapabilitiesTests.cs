using System;
using WriterApp.Application.Documents;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class QualityIssueCapabilitiesTests
    {
        [Fact]
        public void IsAutoProposable_SentenceLengthRule_True()
        {
            PageQualityIssueDto issue = new(
                "k1",
                Guid.Empty,
                Guid.Empty,
                "readability.sentence_length",
                "sentence-length",
                "warning",
                "Long sentence",
                null,
                null,
                10,
                40,
                null,
                DateTimeOffset.UtcNow);

            Assert.True(QualityIssueCapabilities.IsAutoProposable(issue));
            Assert.True(QualityIssueCapabilities.IsSentenceLengthIssue(issue));
        }

        [Fact]
        public void IsAutoProposable_SentenceLengthKindWithSuffix_True()
        {
            PageQualityIssueDto issue = new(
                "k2",
                Guid.Empty,
                Guid.Empty,
                "unknown",
                "sentence-length warning",
                "warning",
                "Long sentence",
                null,
                null,
                10,
                40,
                null,
                DateTimeOffset.UtcNow);

            Assert.True(QualityIssueCapabilities.IsAutoProposable(issue));
        }

        [Fact]
        public void IsAutoProposable_RepeatedWordRule_True()
        {
            PageQualityIssueDto issue = new(
                "k3",
                Guid.Empty,
                Guid.Empty,
                "style.repeated_words",
                "repeated-word",
                "info",
                "Repeated word",
                null,
                "att",
                10,
                13,
                null,
                DateTimeOffset.UtcNow);

            Assert.True(QualityIssueCapabilities.IsAutoProposable(issue));
            Assert.True(QualityIssueCapabilities.IsRepeatedWordIssue(issue));
        }

        [Fact]
        public void IsAutoProposable_PassiveVoiceRule_True()
        {
            PageQualityIssueDto issue = new(
                "k4",
                Guid.Empty,
                Guid.Empty,
                "style.passive_voice",
                "passive-voice",
                "info",
                "Possible passive voice detected.",
                "Consider using active voice for stronger clarity.",
                "was tempered",
                10,
                22,
                null,
                DateTimeOffset.UtcNow);

            Assert.True(QualityIssueCapabilities.IsAutoProposable(issue));
            Assert.True(QualityIssueCapabilities.IsPassiveVoiceIssue(issue));
        }
    }
}
