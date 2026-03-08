using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;

namespace WriterApp.Shared.Localization
{
    public sealed record TranslationLanguageOption(
        string Code,
        string DisplayName,
        string? NativeName = null,
        bool IsPopular = false)
    {
        public string SearchLabel =>
            string.IsNullOrWhiteSpace(NativeName)
                ? DisplayName
                : $"{DisplayName} ({NativeName})";
    }

    public static class TranslationLanguages
    {
        private static readonly Regex LanguageTagPattern = new(
            "^[A-Za-z]{2,3}(?:-[A-Za-z0-9]{2,8})*$",
            RegexOptions.Compiled);

        private static readonly ReadOnlyCollection<TranslationLanguageOption> Catalog = new[]
        {
            new TranslationLanguageOption("ar", "Arabic", "العربية", true),
            new TranslationLanguageOption("hy", "Armenian", "Հայերեն"),
            new TranslationLanguageOption("az", "Azerbaijani", "Azərbaycan dili"),
            new TranslationLanguageOption("eu", "Basque", "Euskara"),
            new TranslationLanguageOption("bn", "Bengali", "বাংলা", true),
            new TranslationLanguageOption("bs", "Bosnian", "Bosanski"),
            new TranslationLanguageOption("bg", "Bulgarian", "Български"),
            new TranslationLanguageOption("ca", "Catalan", "Català"),
            new TranslationLanguageOption("zh-Hans", "Chinese (Simplified)", "简体中文", true),
            new TranslationLanguageOption("zh-Hant", "Chinese (Traditional)", "繁體中文", true),
            new TranslationLanguageOption("hr", "Croatian", "Hrvatski"),
            new TranslationLanguageOption("cs", "Czech", "Čeština"),
            new TranslationLanguageOption("da", "Danish", "Dansk", true),
            new TranslationLanguageOption("nl", "Dutch", "Nederlands", true),
            new TranslationLanguageOption("en", "English", "English", true),
            new TranslationLanguageOption("et", "Estonian", "Eesti"),
            new TranslationLanguageOption("fi", "Finnish", "Suomi"),
            new TranslationLanguageOption("fr", "French", "Français", true),
            new TranslationLanguageOption("gl", "Galician", "Galego"),
            new TranslationLanguageOption("ka", "Georgian", "ქართული"),
            new TranslationLanguageOption("de", "German", "Deutsch", true),
            new TranslationLanguageOption("el", "Greek", "Ελληνικά"),
            new TranslationLanguageOption("gu", "Gujarati", "ગુજરાતી"),
            new TranslationLanguageOption("he", "Hebrew", "עברית"),
            new TranslationLanguageOption("hi", "Hindi", "हिन्दी", true),
            new TranslationLanguageOption("hu", "Hungarian", "Magyar"),
            new TranslationLanguageOption("is", "Icelandic", "Íslenska"),
            new TranslationLanguageOption("id", "Indonesian", "Bahasa Indonesia", true),
            new TranslationLanguageOption("ga", "Irish", "Gaeilge"),
            new TranslationLanguageOption("it", "Italian", "Italiano", true),
            new TranslationLanguageOption("ja", "Japanese", "日本語", true),
            new TranslationLanguageOption("kn", "Kannada", "ಕನ್ನಡ"),
            new TranslationLanguageOption("kk", "Kazakh", "Қазақша"),
            new TranslationLanguageOption("ko", "Korean", "한국어", true),
            new TranslationLanguageOption("lv", "Latvian", "Latviešu"),
            new TranslationLanguageOption("lt", "Lithuanian", "Lietuvių"),
            new TranslationLanguageOption("mk", "Macedonian", "Македонски"),
            new TranslationLanguageOption("ms", "Malay", "Bahasa Melayu"),
            new TranslationLanguageOption("ml", "Malayalam", "മലയാളം"),
            new TranslationLanguageOption("mt", "Maltese", "Malti"),
            new TranslationLanguageOption("mr", "Marathi", "मराठी"),
            new TranslationLanguageOption("mn", "Mongolian", "Монгол"),
            new TranslationLanguageOption("ne", "Nepali", "नेपाली"),
            new TranslationLanguageOption("no", "Norwegian", "Norsk", true),
            new TranslationLanguageOption("fa", "Persian", "فارسی"),
            new TranslationLanguageOption("pl", "Polish", "Polski", true),
            new TranslationLanguageOption("pt", "Portuguese", "Português", true),
            new TranslationLanguageOption("pt-BR", "Portuguese (Brazil)", "Português (Brasil)", true),
            new TranslationLanguageOption("pt-PT", "Portuguese (Portugal)", "Português (Portugal)"),
            new TranslationLanguageOption("pa", "Punjabi", "ਪੰਜਾਬੀ"),
            new TranslationLanguageOption("ro", "Romanian", "Română"),
            new TranslationLanguageOption("ru", "Russian", "Русский", true),
            new TranslationLanguageOption("sr", "Serbian", "Српски"),
            new TranslationLanguageOption("sk", "Slovak", "Slovenčina"),
            new TranslationLanguageOption("sl", "Slovenian", "Slovenščina"),
            new TranslationLanguageOption("so", "Somali", "Soomaali"),
            new TranslationLanguageOption("es", "Spanish", "Español", true),
            new TranslationLanguageOption("sw", "Swahili", "Kiswahili"),
            new TranslationLanguageOption("sv", "Swedish", "Svenska", true),
            new TranslationLanguageOption("tl", "Tagalog", "Tagalog"),
            new TranslationLanguageOption("ta", "Tamil", "தமிழ்"),
            new TranslationLanguageOption("te", "Telugu", "తెలుగు"),
            new TranslationLanguageOption("th", "Thai", "ไทย", true),
            new TranslationLanguageOption("tr", "Turkish", "Türkçe", true),
            new TranslationLanguageOption("uk", "Ukrainian", "Українська"),
            new TranslationLanguageOption("ur", "Urdu", "اردو"),
            new TranslationLanguageOption("uz", "Uzbek", "Oʻzbek"),
            new TranslationLanguageOption("vi", "Vietnamese", "Tiếng Việt", true),
            new TranslationLanguageOption("cy", "Welsh", "Cymraeg"),
            new TranslationLanguageOption("xh", "Xhosa", "isiXhosa"),
            new TranslationLanguageOption("yo", "Yoruba", "Yorùbá"),
            new TranslationLanguageOption("zu", "Zulu", "isiZulu")
        }
        .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
        .ToList()
        .AsReadOnly();

        private static readonly Dictionary<string, TranslationLanguageOption> ByCode = Catalog
            .ToDictionary(item => NormalizeTag(item.Code), StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, string> Aliases = BuildAliases();

        public static IReadOnlyList<TranslationLanguageOption> All => Catalog;

        public static IReadOnlyList<TranslationLanguageOption> Popular =>
            Catalog.Where(item => item.IsPopular).OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();

        public static string? NormalizeRequestedLanguage(string? value, bool allowAuto = false)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            string trimmed = value.Trim();
            if (allowAuto && string.Equals(trimmed, "auto", StringComparison.OrdinalIgnoreCase))
            {
                return "auto";
            }

            string normalized = NormalizeTag(trimmed);
            if (ByCode.TryGetValue(normalized, out TranslationLanguageOption? option))
            {
                return option.Code;
            }

            if (Aliases.TryGetValue(trimmed.Trim().ToLowerInvariant(), out string? aliasCode)
                && ByCode.TryGetValue(aliasCode, out option))
            {
                return option.Code;
            }

            return LanguageTagPattern.IsMatch(trimmed) ? normalized : trimmed;
        }

        public static TranslationLanguageOption? Find(string? value, bool allowAuto = false)
        {
            string? normalized = NormalizeRequestedLanguage(value, allowAuto);
            if (string.IsNullOrWhiteSpace(normalized) || string.Equals(normalized, "auto", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return ByCode.TryGetValue(NormalizeTag(normalized), out TranslationLanguageOption? option)
                ? option
                : null;
        }

        public static string GetDisplayNameOrValue(string? value, bool allowAuto = false)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            if (allowAuto && string.Equals(value.Trim(), "auto", StringComparison.OrdinalIgnoreCase))
            {
                return "Auto-detect";
            }

            TranslationLanguageOption? option = Find(value, allowAuto);
            return option?.DisplayName ?? value.Trim();
        }

        public static string GetDisplayLabel(string? value, bool allowAuto = false)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Unknown";
            }

            TranslationLanguageOption? option = Find(value, allowAuto);
            if (option is null)
            {
                return allowAuto && string.Equals(value.Trim(), "auto", StringComparison.OrdinalIgnoreCase)
                    ? "Auto-detect"
                    : value.Trim();
            }

            return string.IsNullOrWhiteSpace(option.NativeName)
                ? option.DisplayName
                : $"{option.DisplayName} ({option.NativeName})";
        }

        private static Dictionary<string, string> BuildAliases()
        {
            Dictionary<string, string> aliases = new(StringComparer.OrdinalIgnoreCase);
            foreach (TranslationLanguageOption option in Catalog)
            {
                aliases[option.Code.ToLowerInvariant()] = NormalizeTag(option.Code);
                aliases[option.DisplayName.ToLowerInvariant()] = NormalizeTag(option.Code);
                if (!string.IsNullOrWhiteSpace(option.NativeName))
                {
                    aliases[option.NativeName.Trim().ToLowerInvariant()] = NormalizeTag(option.Code);
                }
            }

            aliases["norwegian bokmal"] = "no";
            aliases["bokmal"] = "no";
            aliases["chinese simplified"] = "zh-Hans";
            aliases["chinese traditional"] = "zh-Hant";
            aliases["portuguese brazil"] = "pt-BR";
            aliases["brazilian portuguese"] = "pt-BR";
            aliases["portuguese portugal"] = "pt-PT";
            return aliases;
        }

        private static string NormalizeTag(string value)
        {
            string[] parts = value.Trim().Split('-', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return value.Trim();
            }

            parts[0] = parts[0].ToLowerInvariant();
            for (int index = 1; index < parts.Length; index++)
            {
                parts[index] = parts[index].Length == 2
                    ? parts[index].ToUpperInvariant()
                    : char.ToUpperInvariant(parts[index][0]) + parts[index][1..].ToLowerInvariant();
            }

            return string.Join("-", parts);
        }
    }
}
