using System.Text.Json;

namespace VaultKind_Windows.Services;

internal sealed record DoctorRunSummary(
    int Healthy,
    int Attention,
    int Information,
    DateTimeOffset CompletedAt);

internal static class DoctorSummaryStore
{
    internal static DoctorRunSummary? Load() => Load(SummaryPath, DateTimeOffset.Now);

    internal static DoctorRunSummary? Load(string path, DateTimeOffset now)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            DoctorRunSummary? summary = JsonSerializer.Deserialize<DoctorRunSummary>(File.ReadAllText(path));
            return summary is not null
                && summary.Healthy >= 0
                && summary.Attention >= 0
                && summary.Information >= 0
                && summary.CompletedAt <= now.AddMinutes(5)
                    ? summary
                    : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    internal static void Save(DoctorRunSummary summary) => Save(SummaryPath, summary);

    internal static void Save(string path, DoctorRunSummary summary)
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
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(summary));
            File.Move(temporaryPath, path, true);
        }
        catch (Exception)
        {
            // A cached summary is convenience state and must never interrupt vault work.
        }
    }

    private static string SettingsDirectory => Path.Combine(
        VaultKindDataPaths.LocalApplicationDataRoot,
        "VaultKind");

    private static string SummaryPath => Path.Combine(SettingsDirectory, "doctor-summary.json");
}
