using System.Text.Json;
using System.Text.RegularExpressions;

namespace VaultKind_Windows.Services;

internal static partial class VaultKindDataPaths
{
    private const string PackageProfileFileName = "VaultKind.PackageProfile.json";
    private const int MaximumUnixDomainSocketPathLength = 108;
    private static readonly Lazy<ResolvedPaths> ConfiguredPaths = new(ResolveConfiguredPaths);

    internal static string LocalApplicationDataRoot => ConfiguredPaths.Value.LocalApplicationDataRoot;

    internal static string SocketPath => ConfiguredPaths.Value.SocketPath;

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

        string isolatedRoot = Path.GetFullPath(Path.Combine(resolvedLocalApplicationData, "VKP", isolatedProfileId));
        string allowedPrefix = Path.Combine(resolvedLocalApplicationData, "VKP")
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!isolatedRoot.StartsWith(allowedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The packaged VaultKind profile escaped its local application-data boundary.");
        }
        _ = ResolveSocketPath(isolatedRoot);
        return isolatedRoot;
    }

    internal static string ResolveSocketPath(string localApplicationDataRoot, string? isolatedProfileId = null, string? userProfile = null)
    {
        string socketPath;
        if (string.IsNullOrWhiteSpace(isolatedProfileId))
        {
            socketPath = Path.Combine(
                Path.GetFullPath(localApplicationDataRoot),
                "VaultKind",
                "bridge",
                "native-bridge-v1.sock");
        }
        else
        {
            if (!IsolatedProfileIdPattern().IsMatch(isolatedProfileId)
                || string.IsNullOrWhiteSpace(userProfile))
            {
                throw new InvalidDataException("The packaged VaultKind bridge identity is invalid.");
            }

            string resolvedUserProfile = Path.GetFullPath(userProfile);
            string bridgeRoot = Path.GetFullPath(Path.Combine(resolvedUserProfile, ".vaultkind-runtime"));
            socketPath = Path.GetFullPath(Path.Combine(bridgeRoot, isolatedProfileId, "native-bridge-v1.sock"));
            string allowedPrefix = bridgeRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!socketPath.StartsWith(allowedPrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("The packaged VaultKind bridge escaped its user-profile boundary.");
            }
        }
        if (socketPath.Length > MaximumUnixDomainSocketPathLength)
        {
            throw new InvalidDataException($"The VaultKind local socket path is {socketPath.Length} characters; Windows permits at most {MaximumUnixDomainSocketPathLength}.");
        }
        return socketPath;
    }

    private static ResolvedPaths ResolveConfiguredPaths()
    {
        string localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string markerPath = Path.Combine(AppContext.BaseDirectory, PackageProfileFileName);
        if (!File.Exists(markerPath))
        {
            string permanentRoot = ResolveLocalApplicationDataRoot(localApplicationData, null);
            return new ResolvedPaths(permanentRoot, ResolveSocketPath(permanentRoot));
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
        string isolatedRoot = ResolveLocalApplicationDataRoot(localApplicationData, marker.ProfileId);
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return new ResolvedPaths(isolatedRoot, ResolveSocketPath(isolatedRoot, marker.ProfileId, userProfile));
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private sealed record ResolvedPaths(string LocalApplicationDataRoot, string SocketPath);
    private sealed record PackageProfileMarker(string ProfileId, string PackageName, bool DevelopmentOnly);

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9.-]{0,15}$", RegexOptions.CultureInvariant)]
    private static partial Regex IsolatedProfileIdPattern();
}
