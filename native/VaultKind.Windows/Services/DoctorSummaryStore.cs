using System.Text.Json;

namespace VaultKind_Windows.Services;

internal sealed record DoctorRunSummary(
    int Healthy,
    int Attention,
    int Information,
    DateTimeOffset CompletedAt);

internal static class DoctorSummaryStore
{
    internal static DoctorRunSummary? Load()
    {
        try
        {
            if (!File.Exists(SummaryPath))
            {
                return null;
            }

            DoctorRunSummary? summary = JsonSerializer.Deserialize<DoctorRunSummary>(File.ReadAllText(SummaryPath));
            return summary is not null
                && summary.Healthy >= 0
                && summary.Attention >= 0
                && summary.Information >= 0
                && summary.CompletedAt <= DateTimeOffset.Now.AddMinutes(5)
                    ? summary
                    : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    internal static void Save(DoctorRunSummary summary)
    {
        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            string temporaryPath = SummaryPath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(summary));
            File.Move(temporaryPath, SummaryPath, true);
        }
        catch (Exception)
        {
            // A cached summary is convenience state and must never interrupt vault work.
        }
    }

    private static string SettingsDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VaultKind");

    private static string SummaryPath => Path.Combine(SettingsDirectory, "doctor-summary.json");
}
