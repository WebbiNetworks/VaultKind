using System.Text.Json;
using System.Text.RegularExpressions;

namespace VaultKind_Windows.Services;

internal static partial class VaultKindDataPaths
{
    private const string PackageProfileFileName = "VaultKind.PackageProfile.json";
    private static readonly Lazy<string> LocalApplicationDataRootValue = new(ResolveConfiguredLocalApplicationDataRoot);

    internal static string LocalApplicationDataRoot => LocalApplicationDataRootValue.Value;

    internal static string SocketPath => Path.Combine(
        LocalApplicationDataRoot,
        "VaultKind",
        "bridge",
        "native-bridge-v1.sock");

    internal static string SettingsPath => JavaVaultEngineHost.ResolvePersistentSettingsPath(LocalApplicationDataRoot);

    internal static string ResolveLocalApplicationDataRoot(string localApplicationData, string? isolatedProfileId)
    {
        string resolvedLocalApplicationData = Path.GetFullPath(localApplicationData);
        if (string.IsNullOrWhiteSpace(isolatedProfileId))
        {
            return resolvedLocalApplicationData;
        }
        if (!IsolatedProfileIdPattern().IsMatch(isolatedProfileId))
        {
            throw new InvalidDataException("The packaged VaultKind profile identifier is invalid.");
        }

        string isolatedRoot = Path.GetFullPath(Path.Combine(resolvedLocalApplicationData, "VaultKind", "PackageProfiles", isolatedProfileId));
        string allowedPrefix = Path.Combine(resolvedLocalApplicationData, "VaultKind", "PackageProfiles")
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!isolatedRoot.StartsWith(allowedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The packaged VaultKind profile escaped its local application-data boundary.");
        }
        return isolatedRoot;
    }

    private static string ResolveConfiguredLocalApplicationDataRoot()
    {
        string localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string markerPath = Path.Combine(AppContext.BaseDirectory, PackageProfileFileName);
        if (!File.Exists(markerPath))
        {
            return ResolveLocalApplicationDataRoot(localApplicationData, null);
        }

        PackageProfileMarker marker;
        try
        {
            marker = JsonSerializer.Deserialize<PackageProfileMarker>(File.ReadAllBytes(markerPath), JsonOptions)
                ?? throw new InvalidDataException("The packaged VaultKind profile marker is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The packaged VaultKind profile marker is invalid JSON.", exception);
        }

        if (!marker.DevelopmentOnly
            || string.IsNullOrWhiteSpace(marker.PackageName)
            || !marker.PackageName.EndsWith(".Development", StringComparison.Ordinal))
        {
            throw new InvalidDataException("The packaged VaultKind profile marker is not a development-package marker.");
        }
        return ResolveLocalApplicationDataRoot(localApplicationData, marker.ProfileId);
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private sealed record PackageProfileMarker(string ProfileId, string PackageName, bool DevelopmentOnly);

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9.-]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex IsolatedProfileIdPattern();
}
