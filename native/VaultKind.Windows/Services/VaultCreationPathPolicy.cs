namespace VaultKind_Windows.Services;

internal static class VaultCreationPathPolicy
{
    internal static VaultCreationTarget Resolve(string selectedFolderPath, string vaultName)
    {
        string selectedFolder = Path.GetFullPath(selectedFolderPath);
        string normalizedName = vaultName.Trim();
        string selectedFolderName = Path.GetFileName(Path.TrimEndingDirectorySeparator(selectedFolder));
        bool selectedFolderMatchesName = selectedFolderName.Equals(normalizedName, StringComparison.OrdinalIgnoreCase);

        try
        {
            if (File.Exists(selectedFolder) || !Directory.Exists(selectedFolder))
            {
                return VaultCreationTarget.Unavailable(selectedFolder);
            }

            if (!Directory.EnumerateFileSystemEntries(selectedFolder).Any())
            {
                return VaultCreationTarget.Available(selectedFolder, usesSelectedFolder: true);
            }

            if (selectedFolderMatchesName)
            {
                return VaultCreationTarget.Unavailable(selectedFolder);
            }

            string targetPath = Path.Combine(selectedFolder, normalizedName);
            return File.Exists(targetPath) || Directory.Exists(targetPath)
                ? VaultCreationTarget.Unavailable(targetPath)
                : VaultCreationTarget.Available(targetPath, usesSelectedFolder: false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return VaultCreationTarget.Unavailable(selectedFolder);
        }
    }
}

internal sealed record VaultCreationTarget(string Path, bool IsSuitable, bool UsesSelectedFolder)
{
    internal static VaultCreationTarget Available(string path, bool usesSelectedFolder) => new(path, true, usesSelectedFolder);

    internal static VaultCreationTarget Unavailable(string path) => new(path, false, false);
}
