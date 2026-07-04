using System;
using System.Collections.Generic;
using System.Text.Json;

namespace WriterApp.Shared
{
    public static class OnboardingAiDemoRequest
    {
        public const string ParameterKey = "onboarding_demo";
        public const string ActionKey = "tighten.section";

        public static bool IsRequested(string? actionKey, IReadOnlyDictionary<string, object?>? parameters)
        {
            if (!string.Equals(actionKey, ActionKey, StringComparison.Ordinal)
                || parameters is null
                || !parameters.TryGetValue(ParameterKey, out object? value)
                || value is null)
            {
                return false;
            }

            return value switch
            {
                bool boolean => boolean,
                string text => bool.TryParse(text, out bool parsed) && parsed,
                JsonElement json when json.ValueKind is JsonValueKind.True or JsonValueKind.False => json.GetBoolean(),
                JsonElement json when json.ValueKind == JsonValueKind.String
                    => bool.TryParse(json.GetString(), out bool parsed) && parsed,
                _ => false
            };
        }

        public static bool ShouldBypassClientGates(
            bool allowOnboardingDemoBypass,
            string? actionKey,
            IReadOnlyDictionary<string, object?>? parameters)
        {
            return allowOnboardingDemoBypass && IsRequested(actionKey, parameters);
        }
    }
}
