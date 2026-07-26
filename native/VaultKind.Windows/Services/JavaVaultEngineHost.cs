using System.Diagnostics;
using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text.Json;

namespace VaultKind_Windows.Services;

/// <summary>
/// Starts the existing Java vault engine for the native Windows shell during development.
/// The future packaged build will use a bundled Java runtime and engine location; these
/// discovery fallbacks keep the current source-tree preview self-starting.
/// </summary>
internal sealed class JavaVaultEngineHost : IDisposable
{
    private static readonly string[] RequiredCapabilities = ["vault.list", "vault.unlock", "vault.lock", "vault.reveal", "vault.remove", "vault.rename", "vault.stats", "vault.locate_encrypted", "vault.decrypt_filename", "vault.create", "vault.connect", "vault.reset_password", "vault.change_password", "vault.show_recovery_key", "settings.mount.list", "settings.mount.select", "backend.shutdown"];
    private Process? ownedProcess;

    internal bool StartIfNeeded()
    {
        if (IsCompatibleBackendListening(out bool backendListening))
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

        string? repositoryRoot = FindRepositoryRoot();
        string? javaExecutable = FindJavaExecutable(repositoryRoot);
        if (repositoryRoot is null || javaExecutable is null)
        {
            return false;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = javaExecutable,
            WorkingDirectory = repositoryRoot,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        string profileRoot = Path.Combine(repositoryRoot, "target", "ui-dev-profile");
        startInfo.ArgumentList.Add($"-Dlogback.configurationFile={Path.Combine(repositoryRoot, "target", "classes", "logback-native.xml")}");
        startInfo.ArgumentList.Add($"-Dcryptomator.settingsPath={Path.Combine(profileRoot, "settings.json")}");
        startInfo.ArgumentList.Add($"-Dcryptomator.pluginDir={Path.Combine(profileRoot, "plugins")}");
        startInfo.ArgumentList.Add($"-Dcryptomator.logDir={Path.Combine(profileRoot, "logs")}");
        startInfo.ArgumentList.Add($"-Dcryptomator.mountPointsDir={Path.Combine(profileRoot, "mnt")}");
        startInfo.ArgumentList.Add("-Dcryptomator.disableUpdateCheck=true");
        startInfo.ArgumentList.Add("-cp");
        startInfo.ArgumentList.Add(BuildDevelopmentClasspath(repositoryRoot));
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
    private sealed record EngineResponse(int Protocol, string RequestId, bool Ok, string? Backend, string? Error, IReadOnlyList<string>? Capabilities);

    private static string SocketPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VaultKind",
        "bridge",
        "native-bridge-v1.sock");

    private static bool IsCompatibleBackendListening(out bool backendListening)
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
            return hello.Ok &&
                   hello.RequestId == helloId &&
                   hello.Backend == "VaultKind Java Engine" &&
                   hello.Capabilities is not null &&
                   RequiredCapabilities.All(capability => hello.Capabilities.Contains(capability, StringComparer.Ordinal));
        }
        catch (Exception)
        {
            return false;
        }
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
}
