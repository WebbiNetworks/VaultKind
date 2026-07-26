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

if (failures > 0)
{
    Console.Error.WriteLine($"{failures} native policy check(s) failed.");
    return 1;
}

Console.WriteLine($"Passed {doctorCases.Length + lockFailureCases.Length + 1} native policy and preference checks.");
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
            || helloRoot.GetProperty("backend").GetString() != "VaultKind Java Engine")
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
