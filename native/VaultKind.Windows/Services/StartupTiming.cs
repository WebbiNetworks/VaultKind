using System.Diagnostics;
using System.Text;

namespace VaultKind_Windows.Services;

internal static class StartupTiming
{
    private const long MaximumLogBytes = 128 * 1024;
    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private static readonly object Sync = new();
    private static readonly List<Entry> Entries = [];
    private static bool reportWritten;

    internal static void Mark(string phase)
    {
        lock (Sync)
        {
            Entries.Add(new Entry(Clock.Elapsed, phase));
        }
    }

    internal static void WriteReport(string outcome)
    {
        lock (Sync)
        {
            if (reportWritten)
            {
                return;
            }

            reportWritten = true;
            Entries.Add(new Entry(Clock.Elapsed, outcome));
            try
            {
                string directory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "VaultKind",
                    "diagnostics");
                Directory.CreateDirectory(directory);

                var report = new StringBuilder();
                report.AppendLine($"VaultKind startup {DateTimeOffset.Now:O}");
                TimeSpan previous = TimeSpan.Zero;
                foreach (Entry entry in Entries)
                {
                    report.AppendLine($"{entry.Elapsed.TotalMilliseconds,9:F0} ms  (+{(entry.Elapsed - previous).TotalMilliseconds,7:F0} ms)  {entry.Phase}");
                    previous = entry.Elapsed;
                }
                report.AppendLine();
                string path = Path.Combine(directory, "startup-timing.log");
                if (File.Exists(path) && new FileInfo(path).Length >= MaximumLogBytes)
                {
                    File.WriteAllText(path, report.ToString());
                }
                else
                {
                    File.AppendAllText(path, report.ToString());
                }
            }
            catch (Exception)
            {
                // Startup measurement must never affect application availability.
            }
        }
    }

    private sealed record Entry(TimeSpan Elapsed, string Phase);
}
