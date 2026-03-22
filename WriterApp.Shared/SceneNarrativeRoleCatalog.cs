using System;
using System.Collections.Generic;
using System.Linq;

namespace WriterApp.Application.Documents
{
    public static class SceneNarrativeRoleCatalog
    {
        public static readonly IReadOnlyList<string> Values =
        [
            "Setup",
            "Inciting Incident",
            "Rising Action",
            "Complication",
            "Revelation",
            "Relationship Beat",
            "Reversal",
            "Decision",
            "Climax",
            "Aftermath"
        ];

        public static bool TryNormalize(string? value, out string? normalizedRole)
        {
            normalizedRole = null;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string trimmed = value.Trim();
            normalizedRole = Values.FirstOrDefault(role =>
                string.Equals(role, trimmed, StringComparison.OrdinalIgnoreCase));
            return normalizedRole is not null;
        }

        public static (string? NarrativeRole, string? NarrativeIntent) SplitLegacyPurpose(string? value)
        {
            if (TryNormalize(value, out string? role))
            {
                return (role, null);
            }

            return (null, NormalizeOptional(value));
        }

        public static string? NormalizeOptional(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        public static string? ToLegacyPurpose(string? narrativeRole, string? narrativeIntent)
        {
            return NormalizeOptional(narrativeRole) ?? NormalizeOptional(narrativeIntent);
        }
    }
}
