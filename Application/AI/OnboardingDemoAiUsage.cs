using System;
using System.Collections.Generic;

namespace WriterApp.Application.AI
{
    public static class OnboardingDemoAiUsage
    {
        public const string RequestParameterKey = WriterApp.Shared.OnboardingAiDemoRequest.ParameterKey;
        public const string InstructionParameterKey = "instruction";
        public const string HttpContextValidatedKey = "__onboarding_demo_validated";
        public const string DemoActionKey = WriterApp.Shared.OnboardingAiDemoRequest.ActionKey;
        public static readonly TimeSpan GrantLifetime = TimeSpan.FromDays(7);

        private static readonly HashSet<string> AllowedActionKeys = new(StringComparer.Ordinal)
        {
            DemoActionKey
        };

        public static bool IsAllowedAction(string? actionKey)
            => !string.IsNullOrWhiteSpace(actionKey) && AllowedActionKeys.Contains(actionKey);
    }
}
