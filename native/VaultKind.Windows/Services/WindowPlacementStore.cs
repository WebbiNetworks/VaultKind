using Microsoft.UI.Windowing;
using Windows.Graphics;

namespace VaultKind_Windows.Services;

internal static class WindowPlacementStore
{
    private const int MinimumWidth = 960;
    private const int MinimumHeight = 680;

    internal static WindowPlacement? Load() => WindowPlacementPersistence.Load();

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
        var placement = new WindowPlacement(
            restoredBounds.X,
            restoredBounds.Y,
            restoredBounds.Width,
            restoredBounds.Height,
            maximized);
        WindowPlacementPersistence.Save(placement);
    }
}
