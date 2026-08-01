using System.Text.Json;

namespace VaultKind_Windows.Services;

internal static class LearningProgressStore
{
    internal static IReadOnlyList<string> Load(IEnumerable<string> validTopics) =>
        Load(ProgressPath, validTopics);

    internal static IReadOnlyList<string> Load(string path, IEnumerable<string> validTopics)
    {
        try
        {
            if (!File.Exists(path))
            {
                return [];
            }

            HashSet<string> valid = new(validTopics, StringComparer.Ordinal);
            List<string>? stored = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(path));
            return stored?
                .Where(valid.Contains)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray()
                ?? [];
        }
        catch (Exception)
        {
            // Learning progress is optional. Preserve unreadable data and start empty.
            return [];
        }
    }

    internal static void Save(IEnumerable<string> viewedTopics) => Save(ProgressPath, viewedTopics);

    internal static void Save(string path, IEnumerable<string> viewedTopics)
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
            string[] canonicalTopics = viewedTopics
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(canonicalTopics));
            File.Move(temporaryPath, path, true);
        }
        catch (Exception)
        {
            // A progress write failure must not interrupt Learning Center navigation.
        }
    }

    private static string ProgressPath => Path.Combine(
        VaultKindDataPaths.LocalApplicationDataRoot,
        "VaultKind",
        "learning-progress.json");
}
