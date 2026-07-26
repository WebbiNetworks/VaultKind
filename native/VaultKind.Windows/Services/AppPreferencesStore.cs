using System.Text.Json;

namespace VaultKind_Windows.Services;

internal sealed record AppPreferences(
    bool RememberWindowPlacement = true,
    bool RecordActivityHistory = true,
    string AppearanceMode = "dark",
    bool UseLargerText = false,
    string LanguageCode = "system",
    bool SignatureSoundsEnabled = true);

internal static class AppPreferencesStore
{
    internal static AppPreferences Load()
    {
        try
        {
            if (!File.Exists(PreferencesPath))
            {
                return new AppPreferences();
            }

            return JsonSerializer.Deserialize<AppPreferences>(File.ReadAllText(PreferencesPath)) ?? new AppPreferences();
        }
        catch (Exception)
        {
            return new AppPreferences();
        }
    }

    internal static void Save(AppPreferences preferences)
    {
        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            string temporaryPath = PreferencesPath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(preferences));
            File.Move(temporaryPath, PreferencesPath, true);
        }
        catch (Exception)
        {
            // Preferences are convenience state and must never interrupt vault work.
        }
    }

    private static string SettingsDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VaultKind");

    private static string PreferencesPath => Path.Combine(SettingsDirectory, "preferences.json");
}
