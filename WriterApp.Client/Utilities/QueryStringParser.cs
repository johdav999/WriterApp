using System;
using System.Collections.Generic;

namespace WriterApp.Client.Utilities
{
    internal static class QueryStringParser
    {
        public static Dictionary<string, string> Parse(string? queryString)
        {
            Dictionary<string, string> result = new(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(queryString))
            {
                return result;
            }

            string query = queryString[0] == '?' ? queryString[1..] : queryString;
            if (query.Length == 0)
            {
                return result;
            }

            string[] parts = query.Split('&', StringSplitOptions.RemoveEmptyEntries);
            foreach (string part in parts)
            {
                int equalsIndex = part.IndexOf('=');
                string rawKey = equalsIndex >= 0 ? part[..equalsIndex] : part;
                string rawValue = equalsIndex >= 0 ? part[(equalsIndex + 1)..] : string.Empty;
                if (rawKey.Length == 0)
                {
                    continue;
                }

                string key = Decode(rawKey);
                string value = Decode(rawValue);
                if (!result.ContainsKey(key))
                {
                    result[key] = value;
                }
            }

            return result;
        }

        private static string Decode(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return Uri.UnescapeDataString(value.Replace('+', ' '));
        }
    }
}
