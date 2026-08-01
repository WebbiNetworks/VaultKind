using System.Text.Json;

namespace VaultKind_Windows.Services;

internal sealed record SessionActivity(DateTime Timestamp, string Title, string Detail, string Category);

internal static class ActivityHistoryStore
{
    private const int MaximumEntries = 500;

    internal static IReadOnlyList<SessionActivity> Load() => Load(ActivityHistoryPath);

    internal static IReadOnlyList<SessionActivity> Load(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return [];
            }

            List<SessionActivity>? stored = JsonSerializer.Deserialize<List<SessionActivity>>(File.ReadAllText(path));
            return stored?
                .Where(IsValid)
                .TakeLast(MaximumEntries)
                .ToArray()
                ?? [];
        }
        catch (Exception)
        {
            // Activity is optional. Preserve unreadable data and start with an empty history.
            return [];
        }
    }

    internal static void Save(IEnumerable<SessionActivity> activity) => Save(ActivityHistoryPath, activity);

    internal static void Save(string path, IEnumerable<SessionActivity> activity)
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
            SessionActivity[] retained = activity.Where(IsValid).TakeLast(MaximumEntries).ToArray();
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(retained));
            File.Move(temporaryPath, path, true);
        }
        catch (Exception)
        {
            // A history write failure must never interrupt a vault operation.
        }
    }

    private static bool IsValid(SessionActivity activity) =>
        !string.IsNullOrWhiteSpace(activity.Title)
        && !string.IsNullOrWhiteSpace(activity.Category);

    private static string ActivityHistoryPath => Path.Combine(
        VaultKindDataPaths.LocalApplicationDataRoot,
        "VaultKind",
        "activity.json");
}
