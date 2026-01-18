using System;
using WriterApp.Application.AI.StoryCoach;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class StoryCoachValidationTests
    {
        [Fact]
        public void Validator_AllowsContextualReasoningWithoutLabels()
        {
            bool ok = StoryCoachOutputValidator.TryValidate(
                "The protagonist faces a moral dilemma that threatens the mission.",
                "central_conflict",
                "Old conflict",
                out string reason);

            Assert.True(ok);
            Assert.True(string.IsNullOrWhiteSpace(reason));
        }

        [Fact]
        public void Validator_RejectsFieldLabels()
        {
            bool ok = StoryCoachOutputValidator.TryValidate(
                "Protagonist: A brave pilot.",
                "central_conflict",
                "Old conflict",
                out string reason);

            Assert.False(ok);
            Assert.Equal("field_label_detected", reason);
        }

        [Fact]
        public void Validator_RejectsHeadings()
        {
            bool ok = StoryCoachOutputValidator.TryValidate(
                "# Central Conflict\nA rift opens in the crew.",
                "central_conflict",
                "Old conflict",
                out string reason);

            Assert.False(ok);
            Assert.Equal("heading_detected", reason);
        }

        [Fact]
        public void Validator_RejectsEmptyOrUnchanged()
        {
            bool emptyOk = StoryCoachOutputValidator.TryValidate(
                "  ",
                "premise",
                "Old premise",
                out string emptyReason);

            Assert.False(emptyOk);
            Assert.Equal("empty_output", emptyReason);

            bool sameOk = StoryCoachOutputValidator.TryValidate(
                "Same value",
                "premise",
                "Same value",
                out string sameReason);

            Assert.False(sameOk);
            Assert.Equal("no_change", sameReason);
        }
    }
}
