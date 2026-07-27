using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using VaultKind_Windows.Services;
using System.Text.Json;

if (args is ["--probe-socket", var socketPath])
{
    return await ProbeBundledEngineAsync(socketPath);
}

(string Kind, string? CaseId, bool Expected)[] doctorCases =
[
    ("attention", "VK-1003", true),
    ("attention", "VK-3002", true),
    ("attention", "VK-1002", false),
    ("attention", null, false),
    ("healthy", "VK-1003", false),
    ("information", "VK-3002", false)
];

int failures = 0;
foreach ((string kind, string? caseId, bool expected) in doctorCases)
{
    bool actual = DoctorFindingPolicy.IsCritical(kind, caseId);
    if (actual == expected)
    {
        continue;
    }

    Console.Error.WriteLine($"FAIL: kind={kind}, case={caseId ?? "<null>"}, expected={expected}, actual={actual}");
    failures++;
}

(string? Error, bool Expected)[] lockFailureCases =
[
    ("vault_in_use", true),
    ("timeout", false),
    ("engine_unavailable", false),
    (null, false)
];
foreach ((string? error, bool expected) in lockFailureCases)
{
    bool actual = SignatureSoundPolicy.ShouldWarnForLockFailure(error);
    if (actual == expected)
    {
        continue;
    }

    Console.Error.WriteLine($"FAIL: lock error={error ?? "<null>"}, expected warning={expected}, actual={actual}");
    failures++;
}

(int Current, int Count, KeyboardNavigationCommand Command, int Expected)[] keyboardNavigationCases =
[
    (0, 8, KeyboardNavigationCommand.Previous, 7),
    (7, 8, KeyboardNavigationCommand.Next, 0),
    (3, 8, KeyboardNavigationCommand.Previous, 2),
    (3, 8, KeyboardNavigationCommand.Next, 4),
    (3, 8, KeyboardNavigationCommand.First, 0),
    (3, 8, KeyboardNavigationCommand.Last, 7),
    (3, 8, KeyboardNavigationCommand.None, -1),
    (-1, 8, KeyboardNavigationCommand.Next, -1)
];
foreach ((int current, int count, KeyboardNavigationCommand command, int expected) in keyboardNavigationCases)
{
    int actual = KeyboardNavigationPolicy.ResolveNextIndex(current, count, command);
    if (actual == expected)
    {
        continue;
    }

    Console.Error.WriteLine($"FAIL: keyboard navigation current={current}, count={count}, command={command}, expected={expected}, actual={actual}");
    failures++;
}

(string? Reported, string Expected, bool Matches)[] engineProfileCases =
[
    (Path.Combine("C:\\", "VaultKind", "settings.json"), Path.Combine("c:\\", "vaultkind", "settings.json"), true),
    (Path.Combine("C:\\", "VaultKind", "portable", "settings.json"), Path.Combine("C:\\", "VaultKind", "development", "settings.json"), false),
    (null, Path.Combine("C:\\", "VaultKind", "settings.json"), false),
    ("", Path.Combine("C:\\", "VaultKind", "settings.json"), false)
];
foreach ((string? reported, string expected, bool matches) in engineProfileCases)
{
    bool actual = JavaVaultEngineHost.IsExpectedProfile(reported, expected);
    if (actual == matches)
    {
        continue;
    }

    Console.Error.WriteLine($"FAIL: engine profile={reported ?? "<null>"}, expected={expected}, match={matches}, actual={actual}");
    failures++;
}

string expectedBackendProfile = Path.Combine("C:\\", "VaultKind", "development", "settings.json");
string[] requiredCapabilities = ["vault.list", "vault.unlock", "vault.lock", "vault.reveal", "vault.remove", "vault.rename", "vault.stats", "vault.locate_encrypted", "vault.decrypt_filename", "vault.create", "vault.connect", "vault.reset_password", "vault.change_password", "vault.show_recovery_key", "settings.mount.list", "settings.mount.select", "backend.shutdown"];
(int Protocol, string? RequestId, bool Ok, string? Backend, IReadOnlyList<string>? Capabilities, string? Profile, bool Expected)[] backendIdentityCases =
[
    (1, "hello-1", true, "VaultKind Java Engine", requiredCapabilities, expectedBackendProfile, true),
    (1, "hello-1", true, "VaultKind Java Engine", requiredCapabilities, null, false),
    (1, "hello-1", true, "VaultKind Java Engine", requiredCapabilities, Path.Combine("C:\\", "VaultKind", "portable", "settings.json"), false),
    (1, "wrong-request", true, "VaultKind Java Engine", requiredCapabilities, expectedBackendProfile, false),
    (2, "hello-1", true, "VaultKind Java Engine", requiredCapabilities, expectedBackendProfile, false),
    (1, "hello-1", true, "VaultKind Java Engine", requiredCapabilities[..^1], expectedBackendProfile, false)
];
foreach ((int protocol, string? requestId, bool ok, string? backend, IReadOnlyList<string>? capabilities, string? profile, bool expected) in backendIdentityCases)
{
    bool actual = JavaVaultEngineHost.IsExpectedBackendIdentity(protocol, requestId, ok, backend, capabilities, profile, "hello-1", expectedBackendProfile);
    if (actual == expected)
    {
        continue;
    }

    Console.Error.WriteLine($"FAIL: backend identity protocol={protocol}, request={requestId ?? "<null>"}, profile={profile ?? "<null>"}, expected={expected}, actual={actual}");
    failures++;
}

const string previousPreferenceJson = """
    {"RememberWindowPlacement":false,"RecordActivityHistory":false,"AppearanceMode":"light","UseLargerText":true,"LanguageCode":"de","SignatureSoundsEnabled":false}
    """;
AppPreferences? migratedPreferences = JsonSerializer.Deserialize<AppPreferences>(previousPreferenceJson);
if (migratedPreferences is null
    || migratedPreferences.RememberWindowPlacement
    || migratedPreferences.RecordActivityHistory
    || migratedPreferences.AppearanceMode != "light"
    || !migratedPreferences.UseLargerText
    || migratedPreferences.SignatureSoundsEnabled)
{
    Console.Error.WriteLine("FAIL: preferences containing the retired LanguageCode field did not migrate safely.");
    failures++;
}

KeyboardControlsGuide keyboardGuide = KeyboardControlsDocument.Load(typeof(KeyboardControlsDocument).Assembly);
(string Name, bool Passed)[] keyboardDocumentCases =
[
    ("contains global slash shortcut", keyboardGuide.Sections.Any(section => section.Body.Contains("/: Opens Learning Center", StringComparison.Ordinal))),
    ("contains sidebar navigation", keyboardGuide.Sections.Any(section => section.Title == "Main sidebar" && section.Body.Contains("Down Arrow", StringComparison.Ordinal))),
    ("excludes release-only checklist", keyboardGuide.Sections.All(section => section.Title != "Release verification checklist"))
];
foreach ((string name, bool passed) in keyboardDocumentCases)
{
    if (passed)
    {
        continue;
    }

    Console.Error.WriteLine($"FAIL: embedded keyboard controls guide {name}.");
    failures++;
}

if (failures > 0)
{
    Console.Error.WriteLine($"{failures} native policy check(s) failed.");
    return 1;
}

Console.WriteLine($"Passed {doctorCases.Length + lockFailureCases.Length + keyboardNavigationCases.Length + engineProfileCases.Length + backendIdentityCases.Length + keyboardDocumentCases.Length + 1} native policy, keyboard navigation, documentation, backend identity, profile, and preference checks.");
return 0;

static async Task<int> ProbeBundledEngineAsync(string socketPath)
{
    try
    {
        using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), timeout.Token);
        await using var stream = new NetworkStream(socket, ownsSocket: false);

        using JsonDocument hello = await InvokeAsync(stream, "backend.hello", timeout.Token);
        JsonElement helloRoot = hello.RootElement;
        if (!helloRoot.GetProperty("ok").GetBoolean()
            || helloRoot.GetProperty("backend").GetString() != "VaultKind Java Engine"
            || string.IsNullOrWhiteSpace(helloRoot.GetProperty("profile").GetString()))
        {
            throw new InvalidDataException("Unexpected backend.hello response.");
        }

        using JsonDocument shutdown = await InvokeAsync(stream, "backend.shutdown", timeout.Token);
        if (!shutdown.RootElement.GetProperty("ok").GetBoolean())
        {
            throw new InvalidDataException("The bundled engine rejected backend.shutdown.");
        }

        Console.WriteLine("Backend: VaultKind Java Engine");
        return 0;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"Bundled engine protocol probe failed: {exception.Message}");
        return 1;
    }
}

static async Task<JsonDocument> InvokeAsync(NetworkStream stream, string operation, CancellationToken cancellationToken)
{
    string requestId = Guid.NewGuid().ToString("N");
    byte[] payload = JsonSerializer.SerializeToUtf8Bytes(new { protocol = 1, requestId, operation });
    byte[] header = new byte[sizeof(int)];
    BinaryPrimitives.WriteInt32BigEndian(header, payload.Length);
    await stream.WriteAsync(header, cancellationToken);
    await stream.WriteAsync(payload, cancellationToken);
    await stream.FlushAsync(cancellationToken);

    await stream.ReadExactlyAsync(header, cancellationToken);
    int responseLength = BinaryPrimitives.ReadInt32BigEndian(header);
    if (responseLength <= 0 || responseLength > 64 * 1024)
    {
        throw new InvalidDataException($"Invalid engine response length: {responseLength}");
    }

    byte[] response = new byte[responseLength];
    await stream.ReadExactlyAsync(response, cancellationToken);
    JsonDocument document = JsonDocument.Parse(response);
    if (document.RootElement.GetProperty("requestId").GetString() != requestId)
    {
        document.Dispose();
        throw new InvalidDataException("The engine response request ID did not match.");
    }
    return document;
}
