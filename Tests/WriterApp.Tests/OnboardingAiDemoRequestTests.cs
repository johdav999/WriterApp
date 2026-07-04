using System.Collections.Generic;
using System.Text.Json;
using WriterApp.Shared;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class OnboardingAiDemoRequestTests
    {
        [Fact]
        public void ShouldBypassClientGates_ReturnsTrue_ForTaggedOnboardingDemo()
        {
            Dictionary<string, object?> parameters = new()
            {
                [OnboardingAiDemoRequest.ParameterKey] = true
            };

            bool result = OnboardingAiDemoRequest.ShouldBypassClientGates(
                allowOnboardingDemoBypass: true,
                OnboardingAiDemoRequest.ActionKey,
                parameters);

            Assert.True(result);
        }

        [Fact]
        public void ShouldBypassClientGates_ReturnsFalse_WhenBypassNotAllowed()
        {
            Dictionary<string, object?> parameters = new()
            {
                [OnboardingAiDemoRequest.ParameterKey] = true
            };

            bool result = OnboardingAiDemoRequest.ShouldBypassClientGates(
                allowOnboardingDemoBypass: false,
                OnboardingAiDemoRequest.ActionKey,
                parameters);

            Assert.False(result);
        }

        [Fact]
        public void ShouldBypassClientGates_ReturnsFalse_ForDifferentAction()
        {
            Dictionary<string, object?> parameters = new()
            {
                [OnboardingAiDemoRequest.ParameterKey] = true
            };

            bool result = OnboardingAiDemoRequest.ShouldBypassClientGates(
                allowOnboardingDemoBypass: true,
                "expand.section",
                parameters);

            Assert.False(result);
        }

        [Fact]
        public void IsRequested_ReturnsTrue_ForJsonBooleanParameter()
        {
            Dictionary<string, object?> parameters = new()
            {
                [OnboardingAiDemoRequest.ParameterKey] = JsonDocument.Parse("true").RootElement.Clone()
            };

            bool result = OnboardingAiDemoRequest.IsRequested(
                OnboardingAiDemoRequest.ActionKey,
                parameters);

            Assert.True(result);
        }

        [Fact]
        public void IsRequested_ReturnsTrue_ForJsonStringParameter()
        {
            Dictionary<string, object?> parameters = new()
            {
                [OnboardingAiDemoRequest.ParameterKey] = JsonDocument.Parse("\"true\"").RootElement.Clone()
            };

            bool result = OnboardingAiDemoRequest.IsRequested(
                OnboardingAiDemoRequest.ActionKey,
                parameters);

            Assert.True(result);
        }
    }
}
