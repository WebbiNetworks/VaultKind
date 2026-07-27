using System.Text.Json;

namespace VaultKind_Windows.Services;

internal sealed record AppPreferences(
    bool RememberWindowPlacement = true,
    bool RecordActivityHistory = true,
    string AppearanceMode = "dark",
    bool UseLargerText = false,
    bool SignatureSoundsEnabled = true);

internal static class AppPreferencesStore
{
    internal static AppPreferences Load() => Load(PreferencesPath);

    internal static AppPreferences Load(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return new AppPreferences();
            }

            AppPreferences preferences = JsonSerializer.Deserialize<AppPreferences>(File.ReadAllText(path)) ?? new AppPreferences();
            return Normalize(preferences);
        }
        catch (Exception)
        {
            return new AppPreferences();
        }
    }

    internal static void Save(AppPreferences preferences) => Save(PreferencesPath, preferences);

    internal static void Save(string path, AppPreferences preferences)
    {
        try
        {
            string? directory = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(directory))
            {
                return;
            }

            Directory.CreateDirectory(directory);
            string temporaryPath = path + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(Normalize(preferences)));
            File.Move(temporaryPath, path, true);
        }
        catch (Exception)
        {
            // Preferences are convenience state and must never interrupt vault work.
        }
    }

    private static AppPreferences Normalize(AppPreferences preferences) => preferences with
    {
        AppearanceMode = string.Equals(preferences.AppearanceMode, "light", StringComparison.OrdinalIgnoreCase)
            ? "light"
            : "dark"
    };

    private static string SettingsDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VaultKind");

    private static string PreferencesPath => Path.Combine(SettingsDirectory, "preferences.json");
}
