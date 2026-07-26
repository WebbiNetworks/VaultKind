using System.Globalization;
using System.Text;

namespace VaultKind_Windows.Services;

internal sealed record LanguageOption(string Code, string DisplayName);

internal sealed class LocalizationService
{
    private readonly Dictionary<string, Dictionary<string, string>> resources = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> englishKeysByText = new(StringComparer.Ordinal);
    private string requestedLanguage = "system";

    internal LocalizationService()
    {
        string resourceDirectory = Path.Combine(AppContext.BaseDirectory, "Localization");
        if (!Directory.Exists(resourceDirectory))
        {
            return;
        }

        foreach (string path in Directory.EnumerateFiles(resourceDirectory, "strings*.properties"))
        {
            string fileName = Path.GetFileNameWithoutExtension(path);
            string code = fileName.Equals("strings", StringComparison.OrdinalIgnoreCase)
                ? "en"
                : fileName["strings_".Length..].Replace('_', '-');
            resources[code] = LoadProperties(path);
        }

        // The Java application treats every visible string as a resource key. The
        // native shell can inherit that behaviour even while its XAML is migrated
        // incrementally: resolve an exact English resource value back to its key,
        // then retain that key when the user changes language.
        if (resources.TryGetValue("en", out Dictionary<string, string>? english))
        {
            Dictionary<string, List<string>> keysByText = new(StringComparer.Ordinal);
            foreach ((string key, string value) in english)
            {
                string normalized = NormalizeVisibleText(Rebrand(value));
                if (CanReverseMap(normalized))
                {
                    if (!keysByText.TryGetValue(normalized, out List<string>? keys))
                    {
                        keys = [];
                        keysByText[normalized] = keys;
                    }
                    keys.Add(key);
                }
            }

            // Some old and new keys intentionally share the same English copy.
            // Prefer the established key translated by the greatest number of
            // inherited bundles instead of an English-only VaultKind alias.
            foreach ((string text, List<string> keys) in keysByText)
            {
                englishKeysByText[text] = keys
                    .OrderByDescending(key => resources.Values.Count(language => language.ContainsKey(key)))
                    .ThenBy(key => key, StringComparer.Ordinal)
                    .First();
            }
        }
    }

    internal IReadOnlyList<LanguageOption> GetLanguageOptions()
    {
        string effectiveWindowsLanguage = ResolveWindowsLanguageCode();
        List<LanguageOption> options =
        [
            new("system", $"Follow Windows (Recommended) — {NativeLanguageName(effectiveWindowsLanguage)}")
        ];

        options.AddRange(resources.Keys
            .Select(code => new LanguageOption(code, NativeLanguageName(code)))
            .OrderBy(option => option.DisplayName, StringComparer.CurrentCultureIgnoreCase));
        return options;
    }

    internal void SelectLanguage(string? languageCode)
    {
        requestedLanguage = string.IsNullOrWhiteSpace(languageCode) ? "system" : languageCode;
    }

    internal string Get(string key, string fallback)
    {
        foreach (string code in CandidateLanguageCodes())
        {
            if (resources.TryGetValue(code, out Dictionary<string, string>? language) &&
                language.TryGetValue(key, out string? value) &&
                !string.IsNullOrWhiteSpace(value))
            {
                return Rebrand(value);
            }
        }

        return Rebrand(fallback);
    }

    internal bool TryFindKeyByEnglishText(string? text, out string key)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            key = string.Empty;
            return false;
        }

        return englishKeysByText.TryGetValue(NormalizeVisibleText(text), out key!);
    }

    private IEnumerable<string> CandidateLanguageCodes()
    {
        if (requestedLanguage.Equals("system", StringComparison.OrdinalIgnoreCase))
        {
            foreach (string code in WindowsPreferredLanguageCodes())
            {
                yield return code;
            }
        }
        else
        {
            foreach (string code in ExpandLanguageCode(requestedLanguage))
            {
                yield return code;
            }
        }

        yield return "en";
    }

    private string ResolveWindowsLanguageCode()
    {
        foreach (string code in WindowsPreferredLanguageCodes())
        {
            if (resources.ContainsKey(code))
            {
                return code;
            }
        }

        return "en";
    }

    private static IEnumerable<string> WindowsPreferredLanguageCodes()
    {
        IReadOnlyList<string>? preferredLanguages = null;
        try
        {
            preferredLanguages = Windows.System.UserProfile.GlobalizationPreferences.Languages;
        }
        catch
        {
            // Some unpackaged or test environments cannot query the WinRT user
            // profile. CurrentUICulture is the closest safe Windows fallback.
        }

        IEnumerable<string> languages = preferredLanguages is { Count: > 0 }
            ? preferredLanguages
            : [CultureInfo.CurrentUICulture.Name];

        HashSet<string> emitted = new(StringComparer.OrdinalIgnoreCase);
        foreach (string language in languages)
        {
            foreach (string code in ExpandLanguageCode(language))
            {
                if (emitted.Add(code))
                {
                    yield return code;
                }
            }
        }
    }

    private static IEnumerable<string> ExpandLanguageCode(string languageCode)
    {
        string code = languageCode.Replace('_', '-');
        yield return code;

        int separator = code.IndexOf('-');
        if (separator > 0)
        {
            yield return code[..separator];
        }
    }

    private static string NativeLanguageName(string code)
    {
        try
        {
            CultureInfo culture = CultureInfo.GetCultureInfo(code);
            string nativeName = culture.NativeName;
            if (string.IsNullOrWhiteSpace(nativeName))
            {
                return code;
            }

            nativeName = char.ToUpper(nativeName[0], culture) + nativeName[1..];
            string englishName = culture.EnglishName;
            return string.IsNullOrWhiteSpace(englishName) ||
                   nativeName.Equals(englishName, StringComparison.OrdinalIgnoreCase)
                ? nativeName
                : $"{nativeName} — {englishName}";
        }
        catch (CultureNotFoundException)
        {
            return code;
        }
    }

    private static Dictionary<string, string> LoadProperties(string path)
    {
        Dictionary<string, string> values = new(StringComparer.Ordinal);
        foreach (string rawLine in File.ReadLines(path, Encoding.UTF8))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith('!'))
            {
                continue;
            }

            int separator = FindSeparator(line);
            if (separator <= 0)
            {
                continue;
            }

            string key = Unescape(line[..separator].Trim());
            string value = Unescape(line[(separator + 1)..].TrimStart());
            values[key] = value;
        }
        return values;
    }

    private static string Rebrand(string value) =>
        value.Replace("Cryptomator", "VaultKind", StringComparison.Ordinal);

    private static string NormalizeVisibleText(string value) =>
        value.Replace('\u00A0', ' ').Replace("\r\n", "\n", StringComparison.Ordinal).Trim();

    private static bool CanReverseMap(string value) =>
        value.Length > 0 &&
        !value.Contains("%s", StringComparison.Ordinal) &&
        !value.Contains("%d", StringComparison.Ordinal) &&
        !value.Contains("{0", StringComparison.Ordinal);

    private static int FindSeparator(string line)
    {
        bool escaped = false;
        for (int index = 0; index < line.Length; index++)
        {
            char character = line[index];
            if (!escaped && (character == '=' || character == ':'))
            {
                return index;
            }
            escaped = character == '\\' && !escaped;
            if (character != '\\')
            {
                escaped = false;
            }
        }
        return -1;
    }

    private static string Unescape(string value)
    {
        StringBuilder result = new(value.Length);
        for (int index = 0; index < value.Length; index++)
        {
            if (value[index] != '\\' || index + 1 >= value.Length)
            {
                result.Append(value[index]);
                continue;
            }

            char escaped = value[++index];
            if (escaped == 'u' && index + 4 < value.Length &&
                ushort.TryParse(value.AsSpan(index + 1, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ushort unicode))
            {
                result.Append((char)unicode);
                index += 4;
            }
            else
            {
                result.Append(escaped switch
                {
                    'n' => '\n',
                    'r' => '\r',
                    't' => '\t',
                    _ => escaped
                });
            }
        }
        return result.ToString();
    }
}
