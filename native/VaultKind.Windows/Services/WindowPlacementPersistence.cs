using System.Text.Json;

namespace VaultKind_Windows.Services;

internal sealed record WindowPlacement(int X, int Y, int Width, int Height, bool Maximized);

internal static class WindowPlacementPersistence
{
    internal static WindowPlacement? Load() => Load(SettingsPath);

    internal static WindowPlacement? Load(string path)
    {
        try
        {
            return File.Exists(path)
                ? JsonSerializer.Deserialize<WindowPlacement>(File.ReadAllText(path))
                : null;
        }
        catch (Exception)
        {
            // Window placement is convenience state. Preserve unreadable data and use defaults.
            return null;
        }
    }

    internal static void Save(WindowPlacement placement) => Save(SettingsPath, placement);

    internal static void Save(string path, WindowPlacement placement)
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
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(placement));
            File.Move(temporaryPath, path, true);
        }
        catch (Exception)
        {
            // A placement write failure must never block shutdown.
        }
    }

    private static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VaultKind",
        "native-window-placement.json");
}
