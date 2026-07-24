using System.Text.Json;
using Microsoft.UI.Windowing;
using Windows.Graphics;

namespace VaultKind_Windows.Services;

internal sealed record WindowPlacement(int X, int Y, int Width, int Height, bool Maximized);

internal static class WindowPlacementStore
{
    private const int MinimumWidth = 960;
    private const int MinimumHeight = 680;

    internal static WindowPlacement? Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return null;
            }

            return JsonSerializer.Deserialize<WindowPlacement>(File.ReadAllText(SettingsPath));
        }
        catch (Exception)
        {
            return null;
        }
    }

    internal static RectInt32 MakeVisible(WindowPlacement placement)
    {
        var requested = new RectInt32(placement.X, placement.Y, placement.Width, placement.Height);
        var displayArea = DisplayArea.GetFromRect(requested, DisplayAreaFallback.Nearest);
        RectInt32 workArea = displayArea.WorkArea;

        int width = Math.Min(Math.Max(placement.Width, MinimumWidth), workArea.Width);
        int height = Math.Min(Math.Max(placement.Height, MinimumHeight), workArea.Height);
        int x = Math.Clamp(placement.X, workArea.X, workArea.X + workArea.Width - width);
        int y = Math.Clamp(placement.Y, workArea.Y, workArea.Y + workArea.Height - height);
        return new RectInt32(x, y, width, height);
    }

    internal static void Save(RectInt32 restoredBounds, bool maximized)
    {
        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            var placement = new WindowPlacement(
                restoredBounds.X,
                restoredBounds.Y,
                restoredBounds.Width,
                restoredBounds.Height,
                maximized);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(placement));
        }
        catch (Exception)
        {
            // Window placement is convenience state. A write failure must never block shutdown.
        }
    }

    private static string SettingsDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VaultKind");

    private static string SettingsPath => Path.Combine(SettingsDirectory, "native-window-placement.json");
}
