using System;

namespace WriterApp.Data
{
    public static class IdNorm
    {
        public static string Norm(Guid id)
            => id.ToString("D").ToLowerInvariant();

        public static string Norm(string id)
            => id?.Trim().ToLowerInvariant();

        public static bool TryNormGuidString(string id, out string normalized)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                normalized = id;
                return false;
            }

            if (Guid.TryParse(id, out Guid parsed))
            {
                normalized = Norm(parsed);
                return true;
            }

            normalized = id.Trim().ToLowerInvariant();
            return false;
        }
    }
}
