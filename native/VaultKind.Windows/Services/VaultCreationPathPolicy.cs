namespace VaultKind_Windows.Services;

internal static class VaultCreationPathPolicy
{
    internal static VaultCreationTarget Resolve(string selectedFolderPath, string vaultName)
    {
        string selectedFolder = Path.GetFullPath(selectedFolderPath);
        string normalizedName = vaultName.Trim();
        string selectedFolderName = Path.GetFileName(Path.TrimEndingDirectorySeparator(selectedFolder));
        bool selectedFolderMatchesName = selectedFolderName.Equals(normalizedName, StringComparison.OrdinalIgnoreCase);
        string targetPath = selectedFolderMatchesName
            ? selectedFolder
            : Path.Combine(selectedFolder, normalizedName);

        try
        {
            if (File.Exists(targetPath))
            {
                return VaultCreationTarget.Unavailable(targetPath);
            }

            if (!Directory.Exists(targetPath))
            {
                return VaultCreationTarget.Available(targetPath, usesSelectedFolder: false);
            }

            if (selectedFolderMatchesName && !Directory.EnumerateFileSystemEntries(targetPath).Any())
            {
                return VaultCreationTarget.Available(targetPath, usesSelectedFolder: true);
            }

            return VaultCreationTarget.Unavailable(targetPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return VaultCreationTarget.Unavailable(targetPath);
        }
    }
}

internal sealed record VaultCreationTarget(string Path, bool IsSuitable, bool UsesSelectedFolder)
{
    internal static VaultCreationTarget Available(string path, bool usesSelectedFolder) => new(path, true, usesSelectedFolder);

    internal static VaultCreationTarget Unavailable(string path) => new(path, false, false);
}
