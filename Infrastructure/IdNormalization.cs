using System;

namespace WriterApp.Infrastructure
{
    public static class IdNormalization
    {
        public static string Norm(Guid id)
            => id.ToString("D").ToLowerInvariant();

        public static string Norm(string id)
            => string.IsNullOrWhiteSpace(id)
                ? id ?? string.Empty
                : id.Trim().ToLowerInvariant();

        public static bool TryNormGuidString(string id, out string normalized)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                normalized = id ?? string.Empty;
                return false;
            }

            if (Guid.TryParse(id, out Guid parsed))
            {
                normalized = Norm(parsed);
                return true;
            }

            normalized = id;
            return false;
        }
    }
}
