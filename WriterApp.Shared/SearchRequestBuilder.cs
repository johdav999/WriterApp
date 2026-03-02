using System;

namespace WriterApp.Application.Documents
{
    public static class SearchRequestBuilder
    {
        public static string BuildUrl(string query, Guid projectId, bool includeMeta, int limit)
        {
            if (projectId == Guid.Empty)
            {
                throw new ArgumentException("projectId is required.", nameof(projectId));
            }

            string safeQuery = Uri.EscapeDataString(query ?? string.Empty);
            string includeMetaValue = includeMeta ? "true" : "false";
            int safeLimit = Math.Clamp(limit, 1, 200);
            return $"api/search?q={safeQuery}&projectId={projectId:D}&includeMeta={includeMetaValue}&limit={safeLimit}";
        }
    }
}
