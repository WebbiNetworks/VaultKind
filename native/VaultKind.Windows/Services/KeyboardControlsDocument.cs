using System.Reflection;
using System.Text;

namespace VaultKind_Windows.Services;

public sealed record KeyboardControlsGuide(
    string Introduction,
    IReadOnlyList<KeyboardControlsSection> Sections,
    string Tip);

public sealed record KeyboardControlsSection(string Title, string Body);

public static class KeyboardControlsDocument
{
    public const string ResourceName = "VaultKind.KeyboardControls.md";
    private const string StartMarker = "<!-- learning-center:start -->";
    private const string EndMarker = "<!-- learning-center:end -->";
    private const string SummaryPrefix = "<!-- learning-center-summary:";
    private const string TipPrefix = "<!-- learning-center-tip:";

    public static KeyboardControlsGuide Load(Assembly assembly)
    {
        using Stream stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidDataException($"Embedded keyboard controls document is missing: {ResourceName}");
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return Parse(reader.ReadToEnd());
    }

    public static KeyboardControlsGuide Parse(string markdown)
    {
        string normalized = markdown.Replace("\r\n", "\n", StringComparison.Ordinal);
        string introduction = ReadMetadata(normalized, SummaryPrefix);
        string tip = ReadMetadata(normalized, TipPrefix);
        int start = normalized.IndexOf(StartMarker, StringComparison.Ordinal);
        int end = normalized.IndexOf(EndMarker, StringComparison.Ordinal);
        if (start < 0 || end <= start)
        {
            throw new InvalidDataException("Keyboard controls document is missing its Learning Center markers.");
        }

        string[] lines = normalized[(start + StartMarker.Length)..end].Split('\n');
        var sections = new List<KeyboardControlsSection>();
        string? title = null;
        var body = new StringBuilder();

        void FinishSection()
        {
            if (title is null)
            {
                return;
            }

            sections.Add(new KeyboardControlsSection(title, body.ToString().Trim()));
            body.Clear();
        }

        for (int index = 0; index < lines.Length; index++)
        {
            string line = lines[index].TrimEnd();
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                FinishSection();
                title = NormalizeInline(line[3..]);
                continue;
            }
            if (title is null || line.StartsWith("<!--", StringComparison.Ordinal))
            {
                continue;
            }
            if (line.StartsWith('|'))
            {
                string[] cells = TableCells(line);
                if (IsTableSeparator(cells)
                    || (index + 1 < lines.Length && IsTableSeparator(TableCells(lines[index + 1]))))
                {
                    continue;
                }

                AppendBodyLine(body, cells.Length switch
                {
                    2 => $"{cells[0]}: {cells[1]}",
                    3 => $"{cells[0]} — {cells[1]}: {cells[2]}",
                    _ => string.Join(" — ", cells)
                });
                continue;
            }

            string rendered = NormalizeInline(line.TrimStart());
            if (rendered.StartsWith("- ", StringComparison.Ordinal))
            {
                rendered = "• " + rendered[2..];
            }
            AppendBodyLine(body, rendered);
        }

        FinishSection();
        if (sections.Count == 0 || string.IsNullOrWhiteSpace(introduction) || string.IsNullOrWhiteSpace(tip))
        {
            throw new InvalidDataException("Keyboard controls document does not contain complete Learning Center content.");
        }
        return new KeyboardControlsGuide(introduction, sections, tip);
    }

    private static string ReadMetadata(string markdown, string prefix)
    {
        string? line = markdown.Split('\n')
            .Select(candidate => candidate.Trim())
            .FirstOrDefault(candidate => candidate.StartsWith(prefix, StringComparison.Ordinal));
        return line is null
            ? string.Empty
            : line[prefix.Length..].TrimEnd('-', '>', ' ');
    }

    private static string[] TableCells(string line) => line.Trim().Trim('|').Split('|')
        .Select(cell => NormalizeInline(cell.Trim()))
        .ToArray();

    private static bool IsTableSeparator(string[] cells) =>
        cells.Length > 0 && cells.All(cell => cell.Length >= 3 && cell.Trim('-', ':', ' ').Length == 0);

    private static string NormalizeInline(string value) => value
        .Replace("`", string.Empty, StringComparison.Ordinal)
        .Replace("**", string.Empty, StringComparison.Ordinal);

    private static void AppendBodyLine(StringBuilder body, string line)
    {
        if (line.Length == 0)
        {
            if (body.Length > 0 && !body.ToString().EndsWith("\n\n", StringComparison.Ordinal))
            {
                body.AppendLine();
            }
            return;
        }

        body.AppendLine(line);
    }
}
