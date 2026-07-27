using System.Diagnostics;
using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text.Json;

namespace VaultKind_Windows.Services;

/// <summary>
/// Starts the existing Java vault engine for the native Windows shell. Release builds use
/// the deterministic Engine directory beside the app; source-tree discovery remains as a
/// development fallback only.
/// </summary>
internal sealed class JavaVaultEngineHost : IDisposable
{
    private static readonly string[] RequiredCapabilities = ["vault.list", "vault.unlock", "vault.lock", "vault.reveal", "vault.remove", "vault.rename", "vault.stats", "vault.locate_encrypted", "vault.decrypt_filename", "vault.create", "vault.connect", "vault.reset_password", "vault.change_password", "vault.show_recovery_key", "settings.mount.list", "settings.mount.select", "backend.shutdown"];
    private Process? ownedProcess;

    internal bool StartIfNeeded()
    {
        BundledEngineLayout? bundledEngine = FindBundledEngine();
        string? repositoryRoot = bundledEngine is null ? FindRepositoryRoot() : null;
        string settingsPath = ResolveExpectedSettingsPath(bundledEngine, repositoryRoot);
        string profileRoot = Path.GetDirectoryName(settingsPath)!;

        if (IsCompatibleBackendListening(settingsPath, out bool backendListening))
        {
            return true;
        }

        if (backendListening)
        {
            if (!TryRequestGracefulShutdown())
            {
                return false;
            }
            if (!WaitForBackendToStop())
            {
                return false;
            }
        }

        string? javaExecutable = bundledEngine?.JavaExecutable ?? FindJavaExecutable(repositoryRoot);
        if (javaExecutable is null || (bundledEngine is null && repositoryRoot is null))
        {
            return false;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = javaExecutable,
            WorkingDirectory = bundledEngine?.EngineRoot ?? repositoryRoot!,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        string classesDirectory = bundledEngine?.ClassesDirectory ?? Path.Combine(repositoryRoot!, "target", "classes");
        startInfo.ArgumentList.Add($"-Dlogback.configurationFile={Path.Combine(classesDirectory, "logback-native.xml")}");
        startInfo.ArgumentList.Add($"-Dcryptomator.settingsPath={settingsPath}");
        startInfo.ArgumentList.Add($"-Dcryptomator.pluginDir={Path.Combine(profileRoot, "plugins")}");
        startInfo.ArgumentList.Add($"-Dcryptomator.logDir={Path.Combine(profileRoot, "logs")}");
        startInfo.ArgumentList.Add($"-Dcryptomator.mountPointsDir={Path.Combine(profileRoot, "mnt")}");
        startInfo.ArgumentList.Add("-Dcryptomator.disableUpdateCheck=true");
        startInfo.ArgumentList.Add("-cp");
        startInfo.ArgumentList.Add(bundledEngine?.BuildClasspath() ?? BuildDevelopmentClasspath(repositoryRoot!));
        startInfo.ArgumentList.Add("org.cryptomator.launcher.Cryptomator");
        startInfo.ArgumentList.Add("--native-backend");

        ownedProcess = Process.Start(startInfo);
        return ownedProcess is not null;
    }

    public void Dispose()
    {
        if (ownedProcess is { HasExited: false })
        {
            if (TryRequestGracefulShutdown())
            {
                ownedProcess.WaitForExit(5000);
            }
        }

        ownedProcess?.Dispose();
        ownedProcess = null;
    }

    private static bool TryRequestGracefulShutdown()
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            socket.ConnectAsync(new UnixDomainSocketEndPoint(SocketPath), timeout.Token).AsTask().GetAwaiter().GetResult();
            using var stream = new NetworkStream(socket, ownsSocket: false);

            string helloId = Guid.NewGuid().ToString("N");
            WriteFrame(stream, new EngineRequest(1, helloId, "backend.hello"));
            EngineResponse hello = ReadFrame(stream);
            if (!hello.Ok || hello.RequestId != helloId || hello.Backend != "VaultKind Java Engine")
            {
                return false;
            }

            string shutdownId = Guid.NewGuid().ToString("N");
            WriteFrame(stream, new EngineRequest(1, shutdownId, "backend.shutdown"));
            EngineResponse shutdown = ReadFrame(stream);
            return shutdown.Ok && shutdown.RequestId == shutdownId;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static void WriteFrame(Stream stream, EngineRequest request)
    {
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(request, JsonOptions);
        byte[] header = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(header, payload.Length);
        stream.Write(header);
        stream.Write(payload);
        stream.Flush();
    }

    private static EngineResponse ReadFrame(Stream stream)
    {
        byte[] header = new byte[sizeof(int)];
        stream.ReadExactly(header);
        int length = BinaryPrimitives.ReadInt32BigEndian(header);
        if (length <= 0 || length > 64 * 1024)
        {
            throw new InvalidDataException("Invalid engine response length.");
        }

        byte[] payload = new byte[length];
        stream.ReadExactly(payload);
        return JsonSerializer.Deserialize<EngineResponse>(payload, JsonOptions) ?? throw new InvalidDataException("Empty engine response.");
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private sealed record EngineRequest(int Protocol, string RequestId, string Operation);
    private sealed record EngineResponse(int Protocol, string RequestId, bool Ok, string? Backend, string? Error, IReadOnlyList<string>? Capabilities, string? Profile);

    private static string SocketPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VaultKind",
        "bridge",
        "native-bridge-v1.sock");

    private static bool IsCompatibleBackendListening(string expectedProfile, out bool backendListening)
    {
        backendListening = false;
        if (!File.Exists(SocketPath))
        {
            return false;
        }

        try
        {
            using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
            socket.ConnectAsync(new UnixDomainSocketEndPoint(SocketPath), timeout.Token).AsTask().GetAwaiter().GetResult();
            backendListening = socket.Connected;
            using var stream = new NetworkStream(socket, ownsSocket: false);
            string helloId = Guid.NewGuid().ToString("N");
            WriteFrame(stream, new EngineRequest(1, helloId, "backend.hello"));
            EngineResponse hello = ReadFrame(stream);
            return IsExpectedBackendIdentity(hello.Protocol, hello.RequestId, hello.Ok, hello.Backend, hello.Capabilities, hello.Profile, helloId, expectedProfile);
        }
        catch (Exception)
        {
            return false;
        }
    }

    internal static bool IsExpectedProfile(string? reportedProfile, string expectedProfile)
    {
        if (string.IsNullOrWhiteSpace(reportedProfile) || string.IsNullOrWhiteSpace(expectedProfile))
        {
            return false;
        }

        try
        {
            return Path.GetFullPath(reportedProfile).Equals(Path.GetFullPath(expectedProfile), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return false;
        }
    }

    internal static bool IsExpectedBackendIdentity(int protocol, string? requestId, bool ok, string? backend, IReadOnlyList<string>? capabilities, string? profile, string expectedRequestId, string expectedProfile) =>
        protocol == 1 &&
        requestId == expectedRequestId &&
        ok &&
        backend == "VaultKind Java Engine" &&
        capabilities is not null &&
        RequiredCapabilities.All(capability => capabilities.Contains(capability, StringComparer.Ordinal)) &&
        IsExpectedProfile(profile, expectedProfile);

    internal static string ResolveExpectedSettingsPath()
    {
        BundledEngineLayout? bundledEngine = FindBundledEngine();
        string? repositoryRoot = bundledEngine is null ? FindRepositoryRoot() : null;
        return ResolveExpectedSettingsPath(bundledEngine, repositoryRoot);
    }

    private static string ResolveExpectedSettingsPath(BundledEngineLayout? bundledEngine, string? repositoryRoot)
    {
        string profileRoot = bundledEngine is null
            ? Path.Combine(repositoryRoot ?? string.Empty, "target", "ui-dev-profile")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VaultKind", "engine");
        return Path.Combine(profileRoot, "settings.json");
    }

    private static bool WaitForBackendToStop()
    {
        for (int attempt = 0; attempt < 50; attempt++)
        {
            if (!File.Exists(SocketPath))
            {
                return true;
            }
            Thread.Sleep(100);
        }

        return !File.Exists(SocketPath);
    }

    private static BundledEngineLayout? FindBundledEngine()
    {
        string engineRoot = Path.Combine(AppContext.BaseDirectory, "Engine");
        string javaExecutable = Path.Combine(engineRoot, "runtime", "bin", "javaw.exe");
        string classesDirectory = Path.Combine(engineRoot, "classes");
        string librariesDirectory = Path.Combine(engineRoot, "lib");
        return File.Exists(javaExecutable)
            && Directory.Exists(classesDirectory)
            && Directory.Exists(librariesDirectory)
                ? new BundledEngineLayout(engineRoot, javaExecutable, classesDirectory, librariesDirectory)
                : null;
    }

    private static string? FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "pom.xml")) &&
                Directory.Exists(Path.Combine(current.FullName, "native", "VaultKind.Windows")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return null;
    }

    private static string? FindJavaExecutable(string? repositoryRoot)
    {
        if (repositoryRoot is not null)
        {
            string bundledJava = Path.Combine(repositoryRoot, "runtime", "bin", "javaw.exe");
            if (File.Exists(bundledJava))
            {
                return bundledJava;
            }
        }

        string? javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (!string.IsNullOrWhiteSpace(javaHome))
        {
            string javaFromHome = Path.Combine(javaHome, "bin", "javaw.exe");
            if (File.Exists(javaFromHome))
            {
                return javaFromHome;
            }
        }

        string adoptiumRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Eclipse Adoptium");
        if (Directory.Exists(adoptiumRoot))
        {
            return Directory.EnumerateDirectories(adoptiumRoot, "jdk-*", SearchOption.TopDirectoryOnly)
                .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase)
                .Select(path => Path.Combine(path, "bin", "javaw.exe"))
                .FirstOrDefault(File.Exists);
        }

        return null;
    }

    private static string BuildDevelopmentClasspath(string repositoryRoot)
    {
        string mavenClasspathFile = Path.Combine(repositoryRoot, "target", "native-runtime-classpath.txt");
        if (File.Exists(mavenClasspathFile))
        {
            string dependencyClasspath = NormalizeMavenClasspath(File.ReadAllText(mavenClasspathFile).Trim());
            if (!string.IsNullOrWhiteSpace(dependencyClasspath))
            {
                return string.Join(Path.PathSeparator, Path.Combine(repositoryRoot, "target", "classes"), dependencyClasspath);
            }
        }

        string mavenRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".m2",
            "repository",
            "org",
            "openjfx");

        string[] entries =
        [
            Path.Combine(repositoryRoot, "target", "classes"),
            Path.Combine(repositoryRoot, "target", "mods", "*"),
            Path.Combine(repositoryRoot, "target", "libs", "*"),
            Path.Combine(mavenRoot, "javafx-base", "25.0.3", "*"),
            Path.Combine(mavenRoot, "javafx-controls", "25.0.3", "*"),
            Path.Combine(mavenRoot, "javafx-fxml", "25.0.3", "*"),
            Path.Combine(mavenRoot, "javafx-graphics", "25.0.3", "*"),
            Path.Combine(mavenRoot, "javafx-swing", "25.0.3", "*")
        ];
        return string.Join(Path.PathSeparator, entries);
    }

    private static string NormalizeMavenClasspath(string classpath)
    {
        const string repositoryMarker = "\\.m2\\repository\\";
        string localRepository = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".m2",
            "repository");

        return string.Join(Path.PathSeparator, classpath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(entry =>
            {
                int marker = entry.IndexOf(repositoryMarker, StringComparison.OrdinalIgnoreCase);
                return marker >= 0
                    ? Path.Combine(localRepository, entry[(marker + repositoryMarker.Length)..])
                    : entry;
            }));
    }

    private sealed record BundledEngineLayout(
        string EngineRoot,
        string JavaExecutable,
        string ClassesDirectory,
        string LibrariesDirectory)
    {
        internal string BuildClasspath()
        {
            IEnumerable<string> libraries = Directory.EnumerateFiles(LibrariesDirectory, "*.jar", SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);
            return string.Join(Path.PathSeparator, libraries.Prepend(ClassesDirectory));
        }
    }
}
