using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using VaultKind_Windows.Services;
using System.Text.Json;

if (args is ["--probe-socket", var socketPath])
{
    return await ProbeBundledEngineAsync(socketPath);
}

if (args is ["--restore-vault-registration", var restoreSocketPath, var displayName, var vaultPath])
{
    return await RestoreVaultRegistrationAsync(restoreSocketPath, displayName, vaultPath);
}

if (args is ["--inspect-live-engine", var inspectSocketPath])
{
    return await InspectLiveEngineAsync(inspectSocketPath);
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

string persistentSettingsPath = JavaVaultEngineHost.ResolvePersistentSettingsPath(Path.Combine("C:\\", "Users", "Greg", "AppData", "Local"));
string expectedPersistentSettingsPath = Path.Combine("C:\\", "Users", "Greg", "AppData", "Local", "VaultKind", "engine", "settings.json");
if (!persistentSettingsPath.Equals(expectedPersistentSettingsPath, StringComparison.OrdinalIgnoreCase) || persistentSettingsPath.Contains("target", StringComparison.OrdinalIgnoreCase))
{
    Console.Error.WriteLine($"FAIL: persistent engine settings path={persistentSettingsPath}, expected={expectedPersistentSettingsPath}");
    failures++;
}

(string Path, bool Expected)[] developmentClasspathCases =
[
    (Path.Combine("C:\\libs", "cryptolib-2.2.2.jar"), true),
    (Path.Combine("C:\\libs", "junit-jupiter-6.1.0.jar"), false),
    (Path.Combine("C:\\libs", "byte-buddy-agent-1.17.7.jar"), false),
    (Path.Combine("C:\\libs", "javafx-swing-25.0.3-win.jar"), false)
];
foreach ((string path, bool expected) in developmentClasspathCases)
{
    bool actual = JavaVaultEngineHost.IsDevelopmentRuntimeLibrary(path);
    if (actual == expected)
    {
        continue;
    }

    Console.Error.WriteLine($"FAIL: development classpath entry={path}, expected={expected}, actual={actual}");
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

var emptySnapshot = new VaultBackendSnapshot(BackendConnectionState.Ready, [], "Ready");
var mixedSnapshot = new VaultBackendSnapshot(
    BackendConnectionState.Ready,
    [
        new VaultSummary("1", "One", "unlocked", "C:\\Vaults\\One", "X:\\"),
        new VaultSummary("2", "Two", "UnLoCkEd", "C:\\Vaults\\Two", "Y:\\"),
        new VaultSummary("3", "Three", "locked", "C:\\Vaults\\Three", null),
        new VaultSummary("4", "Four", "LOCKED", "C:\\Vaults\\Four", null),
        new VaultSummary("5", "Five", "error", "C:\\Vaults\\Five", null)
    ],
    "Ready");
(string Name, int Actual, int Expected)[] vaultStateCountCases =
[
    ("empty unlocked count", emptySnapshot.UnlockedCount, 0),
    ("empty locked count", emptySnapshot.LockedCount, 0),
    ("mixed-case unlocked count", mixedSnapshot.UnlockedCount, 2),
    ("mixed-case locked count with unknown state ignored", mixedSnapshot.LockedCount, 2)
];
foreach ((string name, int actual, int expected) in vaultStateCountCases)
{
    if (actual == expected)
    {
        continue;
    }

    Console.Error.WriteLine($"FAIL: vault state {name}, expected={expected}, actual={actual}");
    failures++;
}

const string previousPreferenceJson = """
    {"RememberWindowPlacement":false,"RecordActivityHistory":false,"AppearanceMode":"light","UseLargerText":true,"RetiredSetting":"ignored","SignatureSoundsEnabled":false}
    """;
int preferencePersistenceChecks = 0;
string preferenceTestDirectory = Path.Combine(Path.GetTempPath(), $"VaultKind-PreferenceTests-{Guid.NewGuid():N}");
string preferenceTestPath = Path.Combine(preferenceTestDirectory, "preferences.json");
try
{
    AppPreferences defaultPreferences = new();
    AppPreferences missingPreferences = AppPreferencesStore.Load(preferenceTestPath);
    preferencePersistenceChecks++;
    if (missingPreferences != defaultPreferences)
    {
        Console.Error.WriteLine("FAIL: a missing preferences file did not produce safe defaults.");
        failures++;
    }

    Directory.CreateDirectory(preferenceTestDirectory);
    File.WriteAllText(preferenceTestPath, "{not valid json");
    AppPreferences corruptPreferences = AppPreferencesStore.Load(preferenceTestPath);
    preferencePersistenceChecks++;
    if (corruptPreferences != defaultPreferences || !File.Exists(preferenceTestPath))
    {
        Console.Error.WriteLine("FAIL: corrupt preferences were not preserved with safe defaults.");
        failures++;
    }

    File.WriteAllText(preferenceTestPath, previousPreferenceJson);
    AppPreferences migratedPreferences = AppPreferencesStore.Load(preferenceTestPath);
    preferencePersistenceChecks++;
    if (migratedPreferences.RememberWindowPlacement
        || migratedPreferences.RecordActivityHistory
        || migratedPreferences.AppearanceMode != "light"
        || !migratedPreferences.UseLargerText
        || migratedPreferences.SignatureSoundsEnabled)
    {
        Console.Error.WriteLine("FAIL: preferences containing an unknown retired field did not migrate safely.");
        failures++;
    }

    AppPreferences expectedRoundTrip = new(false, true, "light", true, false);
    AppPreferencesStore.Save(preferenceTestPath, expectedRoundTrip with { AppearanceMode = "LIGHT" });
    AppPreferences roundTrippedPreferences = AppPreferencesStore.Load(preferenceTestPath);
    preferencePersistenceChecks++;
    if (roundTrippedPreferences != expectedRoundTrip)
    {
        Console.Error.WriteLine("FAIL: valid preferences did not round-trip with a canonical appearance value.");
        failures++;
    }

    AppPreferencesStore.Save(preferenceTestPath, expectedRoundTrip with { AppearanceMode = "sepia" });
    AppPreferences normalizedPreferences = AppPreferencesStore.Load(preferenceTestPath);
    preferencePersistenceChecks++;
    if (normalizedPreferences.AppearanceMode != "dark"
        || File.ReadAllText(preferenceTestPath).Contains("sepia", StringComparison.OrdinalIgnoreCase))
    {
        Console.Error.WriteLine("FAIL: an unsupported appearance value did not fall back to canonical dark mode.");
        failures++;
    }
}
finally
{
    DeleteTestDirectory(preferenceTestDirectory);
}

int activityPersistenceChecks = 0;
string activityTestDirectory = Path.Combine(Path.GetTempPath(), $"VaultKind-ActivityTests-{Guid.NewGuid():N}");
string activityTestPath = Path.Combine(activityTestDirectory, "activity.json");
try
{
    IReadOnlyList<SessionActivity> missingHistory = ActivityHistoryStore.Load(activityTestPath);
    activityPersistenceChecks++;
    if (missingHistory.Count != 0)
    {
        Console.Error.WriteLine("FAIL: a missing Activity history file did not produce an empty history.");
        failures++;
    }

    Directory.CreateDirectory(activityTestDirectory);
    File.WriteAllText(activityTestPath, "{not valid json");
    IReadOnlyList<SessionActivity> corruptHistory = ActivityHistoryStore.Load(activityTestPath);
    activityPersistenceChecks++;
    if (corruptHistory.Count != 0 || !File.Exists(activityTestPath))
    {
        Console.Error.WriteLine("FAIL: corrupt Activity history was not preserved with an empty in-memory fallback.");
        failures++;
    }

    DateTime startingTimestamp = new(2026, 7, 27, 12, 0, 0, DateTimeKind.Local);
    List<SessionActivity> history = Enumerable.Range(0, 503)
        .Select(index => new SessionActivity(startingTimestamp.AddMinutes(index), $"Event {index}", $"Detail {index}", "vaults"))
        .ToList();
    history.Insert(250, new SessionActivity(startingTimestamp, " ", "Invalid event", "vaults"));
    ActivityHistoryStore.Save(activityTestPath, history);
    IReadOnlyList<SessionActivity> roundTrippedHistory = ActivityHistoryStore.Load(activityTestPath);

    activityPersistenceChecks += 3;
    if (roundTrippedHistory.Count != 500)
    {
        Console.Error.WriteLine($"FAIL: Activity history retained {roundTrippedHistory.Count} entries instead of 500.");
        failures++;
    }
    if (roundTrippedHistory.FirstOrDefault()?.Title != "Event 3" || roundTrippedHistory.LastOrDefault()?.Title != "Event 502")
    {
        Console.Error.WriteLine("FAIL: Activity history did not retain the newest entries in order.");
        failures++;
    }
    if (roundTrippedHistory.Any(activity => string.IsNullOrWhiteSpace(activity.Title) || string.IsNullOrWhiteSpace(activity.Category)))
    {
        Console.Error.WriteLine("FAIL: Activity history retained an invalid entry.");
        failures++;
    }
}
finally
{
    DeleteTestDirectory(activityTestDirectory);
}

int doctorSummaryPersistenceChecks = 0;
string doctorSummaryTestDirectory = Path.Combine(Path.GetTempPath(), $"VaultKind-DoctorSummaryTests-{Guid.NewGuid():N}");
string doctorSummaryTestPath = Path.Combine(doctorSummaryTestDirectory, "doctor-summary.json");
DateTimeOffset doctorSummaryReferenceTime = new(2026, 7, 27, 18, 0, 0, TimeSpan.Zero);
try
{
    DoctorRunSummary? missingSummary = DoctorSummaryStore.Load(doctorSummaryTestPath, doctorSummaryReferenceTime);
    doctorSummaryPersistenceChecks++;
    if (missingSummary is not null)
    {
        Console.Error.WriteLine("FAIL: a missing Vault Doctor summary did not produce an empty cache.");
        failures++;
    }

    Directory.CreateDirectory(doctorSummaryTestDirectory);
    File.WriteAllText(doctorSummaryTestPath, "{not valid json");
    DoctorRunSummary? corruptSummary = DoctorSummaryStore.Load(doctorSummaryTestPath, doctorSummaryReferenceTime);
    doctorSummaryPersistenceChecks++;
    if (corruptSummary is not null || !File.Exists(doctorSummaryTestPath))
    {
        Console.Error.WriteLine("FAIL: a corrupt Vault Doctor summary was not preserved with an empty-cache fallback.");
        failures++;
    }

    DoctorRunSummary expectedSummary = new(4, 1, 2, doctorSummaryReferenceTime.AddMinutes(-1));
    DoctorSummaryStore.Save(doctorSummaryTestPath, expectedSummary);
    DoctorRunSummary? roundTrippedSummary = DoctorSummaryStore.Load(doctorSummaryTestPath, doctorSummaryReferenceTime);
    doctorSummaryPersistenceChecks++;
    if (roundTrippedSummary != expectedSummary)
    {
        Console.Error.WriteLine("FAIL: a valid Vault Doctor summary did not round-trip.");
        failures++;
    }

    DoctorRunSummary[] negativeCountSummaries =
    [
        expectedSummary with { Healthy = -1 },
        expectedSummary with { Attention = -1 },
        expectedSummary with { Information = -1 }
    ];
    foreach (DoctorRunSummary invalidSummary in negativeCountSummaries)
    {
        DoctorSummaryStore.Save(doctorSummaryTestPath, invalidSummary);
        doctorSummaryPersistenceChecks++;
        if (DoctorSummaryStore.Load(doctorSummaryTestPath, doctorSummaryReferenceTime) is not null)
        {
            Console.Error.WriteLine("FAIL: a Vault Doctor summary with a negative count was accepted.");
            failures++;
        }
    }

    DoctorSummaryStore.Save(doctorSummaryTestPath, expectedSummary with { CompletedAt = doctorSummaryReferenceTime.AddMinutes(6) });
    doctorSummaryPersistenceChecks++;
    if (DoctorSummaryStore.Load(doctorSummaryTestPath, doctorSummaryReferenceTime) is not null)
    {
        Console.Error.WriteLine("FAIL: a Vault Doctor summary with an implausibly future timestamp was accepted.");
        failures++;
    }
}
finally
{
    DeleteTestDirectory(doctorSummaryTestDirectory);
}

int learningProgressPersistenceChecks = 0;
string learningProgressTestDirectory = Path.Combine(Path.GetTempPath(), $"VaultKind-LearningProgressTests-{Guid.NewGuid():N}");
string learningProgressTestPath = Path.Combine(learningProgressTestDirectory, "learning-progress.json");
string[] validLearningTopics = ["how", "first", "faq"];
try
{
    IReadOnlyList<string> missingProgress = LearningProgressStore.Load(learningProgressTestPath, validLearningTopics);
    learningProgressPersistenceChecks++;
    if (missingProgress.Count != 0)
    {
        Console.Error.WriteLine("FAIL: missing Learning Center progress did not produce an empty state.");
        failures++;
    }

    Directory.CreateDirectory(learningProgressTestDirectory);
    File.WriteAllText(learningProgressTestPath, "{not valid json");
    IReadOnlyList<string> corruptProgress = LearningProgressStore.Load(learningProgressTestPath, validLearningTopics);
    learningProgressPersistenceChecks++;
    if (corruptProgress.Count != 0 || !File.Exists(learningProgressTestPath))
    {
        Console.Error.WriteLine("FAIL: corrupt Learning Center progress was not preserved with an empty-state fallback.");
        failures++;
    }

    File.WriteAllText(learningProgressTestPath, "[\"faq\",\"unknown\",\"how\",\"faq\",\"first\"]");
    IReadOnlyList<string> sanitizedProgress = LearningProgressStore.Load(learningProgressTestPath, validLearningTopics);
    learningProgressPersistenceChecks += 3;
    if (sanitizedProgress.Contains("unknown", StringComparer.Ordinal) || sanitizedProgress.Count != 3)
    {
        Console.Error.WriteLine("FAIL: Learning Center progress retained an unknown topic.");
        failures++;
    }
    if (sanitizedProgress.Distinct(StringComparer.Ordinal).Count() != sanitizedProgress.Count)
    {
        Console.Error.WriteLine("FAIL: Learning Center progress retained a duplicate topic.");
        failures++;
    }
    if (!sanitizedProgress.SequenceEqual(["faq", "first", "how"], StringComparer.Ordinal))
    {
        Console.Error.WriteLine("FAIL: Learning Center progress was not loaded in deterministic order.");
        failures++;
    }

    LearningProgressStore.Save(learningProgressTestPath, ["how", "faq", "how", "first"]);
    IReadOnlyList<string> roundTrippedProgress = LearningProgressStore.Load(learningProgressTestPath, validLearningTopics);
    learningProgressPersistenceChecks++;
    if (!roundTrippedProgress.SequenceEqual(["faq", "first", "how"], StringComparer.Ordinal)
        || File.ReadAllText(learningProgressTestPath) != "[\"faq\",\"first\",\"how\"]")
    {
        Console.Error.WriteLine("FAIL: Learning Center progress did not round-trip in canonical form.");
        failures++;
    }
}
finally
{
    DeleteTestDirectory(learningProgressTestDirectory);
}

int windowPlacementPersistenceChecks = 0;
string windowPlacementTestDirectory = Path.Combine(Path.GetTempPath(), $"VaultKind-WindowPlacementTests-{Guid.NewGuid():N}");
string windowPlacementTestPath = Path.Combine(windowPlacementTestDirectory, "native-window-placement.json");
try
{
    WindowPlacement? missingPlacement = WindowPlacementPersistence.Load(windowPlacementTestPath);
    windowPlacementPersistenceChecks++;
    if (missingPlacement is not null)
    {
        Console.Error.WriteLine("FAIL: missing window placement did not produce an empty state.");
        failures++;
    }

    Directory.CreateDirectory(windowPlacementTestDirectory);
    File.WriteAllText(windowPlacementTestPath, "{not valid json");
    WindowPlacement? corruptPlacement = WindowPlacementPersistence.Load(windowPlacementTestPath);
    windowPlacementPersistenceChecks++;
    if (corruptPlacement is not null || !File.Exists(windowPlacementTestPath))
    {
        Console.Error.WriteLine("FAIL: corrupt window placement was not preserved with an empty-state fallback.");
        failures++;
    }

    WindowPlacement expectedPlacement = new(120, 80, 1440, 900, true);
    WindowPlacementPersistence.Save(windowPlacementTestPath, expectedPlacement);
    windowPlacementPersistenceChecks++;
    if (WindowPlacementPersistence.Load(windowPlacementTestPath) != expectedPlacement)
    {
        Console.Error.WriteLine("FAIL: valid window placement did not round-trip.");
        failures++;
    }

    WindowPlacement replacementPlacement = new(-400, 40, 1100, 760, false);
    WindowPlacementPersistence.Save(windowPlacementTestPath, replacementPlacement);
    windowPlacementPersistenceChecks++;
    if (WindowPlacementPersistence.Load(windowPlacementTestPath) != replacementPlacement
        || File.Exists(windowPlacementTestPath + ".tmp"))
    {
        Console.Error.WriteLine("FAIL: window placement was not atomically replaced.");
        failures++;
    }
}
finally
{
    DeleteTestDirectory(windowPlacementTestDirectory);
}

KeyboardControlsGuide keyboardGuide = KeyboardControlsDocument.Load(typeof(KeyboardControlsDocument).Assembly);
(string Name, bool Passed)[] keyboardDocumentCases =
[
    ("contains global slash shortcut", keyboardGuide.Sections.Any(section => section.Body.Contains("/: Opens Learning Center", StringComparison.Ordinal))),
    ("contains sidebar navigation", keyboardGuide.Sections.Any(section => section.Title == "Main sidebar" && section.Body.Contains("Down Arrow", StringComparison.Ordinal))),
    ("contains primary vault destinations", keyboardGuide.Sections.Any(section => section.Title == "Main sidebar" && section.Body.Contains("Add Vault, Vault Manager", StringComparison.Ordinal))),
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

Console.WriteLine($"Passed {doctorCases.Length + lockFailureCases.Length + keyboardNavigationCases.Length + engineProfileCases.Length + 1 + developmentClasspathCases.Length + backendIdentityCases.Length + vaultStateCountCases.Length + keyboardDocumentCases.Length + activityPersistenceChecks + preferencePersistenceChecks + doctorSummaryPersistenceChecks + learningProgressPersistenceChecks + windowPlacementPersistenceChecks} native policy, persistence, keyboard navigation, documentation, backend identity, profile, preference, and workflow checks.");
return 0;

static void DeleteTestDirectory(string path)
{
    try
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
    catch (IOException)
    {
        // Test cleanup must not replace an actionable assertion with a Windows error dialog.
    }
    catch (UnauthorizedAccessException)
    {
        // Test cleanup must not replace an actionable assertion with a Windows error dialog.
    }
}

static async Task<int> ProbeBundledEngineAsync(string socketPath)
{
    try
    {
        await AssertMalformedRequestRejectedAsync(socketPath);

        using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
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

        using JsonDocument vaultList = await InvokeAsync(stream, "vault.list", timeout.Token);
        JsonElement vaultListRoot = vaultList.RootElement;
        if (!vaultListRoot.GetProperty("ok").GetBoolean()
            || vaultListRoot.GetProperty("vaults").GetArrayLength() != 0)
        {
            throw new InvalidDataException("The isolated engine did not return an empty vault list.");
        }

        using JsonDocument mountSettings = await InvokeAsync(stream, "settings.mount.list", timeout.Token);
        JsonElement mountSettingsRoot = mountSettings.RootElement;
        if (!mountSettingsRoot.GetProperty("ok").GetBoolean()
            || mountSettingsRoot.GetProperty("selectedMountService").GetString() != "automatic"
            || !mountSettingsRoot.GetProperty("mountServices").EnumerateArray().Any(service => service.GetProperty("id").GetString() == "automatic"))
        {
            throw new InvalidDataException("The isolated engine returned invalid mount-provider settings.");
        }

        string bridgeDirectory = Path.GetDirectoryName(socketPath) ?? throw new InvalidDataException("The probe socket has no parent directory.");
        string isolatedTestRoot = Path.GetFullPath(Path.Combine(bridgeDirectory, "..", ".."));
        string systemTemporaryRoot = Path.GetFullPath(Path.GetTempPath());
        string systemTemporaryPrefix = systemTemporaryRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!isolatedTestRoot.StartsWith(systemTemporaryPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The disposable vault fixture must remain inside the Windows temporary directory.");
        }
        string fixtureParent = Path.Combine(isolatedTestRoot, "fixtures");
        string fixtureVaultPath = Path.Combine(fixtureParent, "DisposableVault");
        Directory.CreateDirectory(fixtureParent);
        const string originalPassword = "Probe-Original-2026!";
        const string changedPassword = "Probe-Changed-2026!";
        const string recoveredPassword = "Probe-Recovered-2026!";

        using JsonDocument createVault = await InvokeAsync(stream, "vault.create", timeout.Token, password: originalPassword, vaultPath: fixtureVaultPath, createRecoveryKey: true);
        JsonElement createRoot = createVault.RootElement;
        AssertProtocolSuccess(createRoot, "created", "disposable vault creation");
        string vaultId = createRoot.GetProperty("vaultId").GetString() ?? throw new InvalidDataException("Disposable vault creation returned no vault ID.");
        string fixtureRecoveryKey = createRoot.GetProperty("recoveryKey").GetString() ?? throw new InvalidDataException("Disposable vault creation returned no recovery key.");
        if (!Directory.Exists(fixtureVaultPath) || string.IsNullOrWhiteSpace(vaultId) || string.IsNullOrWhiteSpace(fixtureRecoveryKey))
        {
            throw new InvalidDataException("Disposable vault creation did not produce its isolated fixture.");
        }

        using JsonDocument createdVaultList = await InvokeAsync(stream, "vault.list", timeout.Token);
        JsonElement[] createdVaults = createdVaultList.RootElement.GetProperty("vaults").EnumerateArray().ToArray();
        if (createdVaults.Length != 1 || createdVaults[0].GetProperty("id").GetString() != vaultId)
        {
            throw new InvalidDataException("The disposable vault was not registered in the isolated profile.");
        }

        using JsonDocument initialRecovery = await InvokeAsync(stream, "vault.show_recovery_key", timeout.Token, vaultId: vaultId, password: originalPassword);
        AssertRecoveryKey(initialRecovery.RootElement, fixtureRecoveryKey, "initial recovery-key display");

        using JsonDocument wrongRecoveryPassword = await InvokeAsync(stream, "vault.show_recovery_key", timeout.Token, vaultId: vaultId, password: "incorrect-password");
        AssertProtocolError(wrongRecoveryPassword.RootElement, "wrong_password", "recovery-key display with the wrong password");

        using JsonDocument changePassword = await InvokeAsync(stream, "vault.change_password", timeout.Token, vaultId: vaultId, password: originalPassword, newPassword: changedPassword);
        AssertProtocolSuccess(changePassword.RootElement, "password_changed", "disposable password change");
        using JsonDocument oldPasswordAfterChange = await InvokeAsync(stream, "vault.show_recovery_key", timeout.Token, vaultId: vaultId, password: originalPassword);
        AssertProtocolError(oldPasswordAfterChange.RootElement, "wrong_password", "retired password after password change");
        using JsonDocument changedRecovery = await InvokeAsync(stream, "vault.show_recovery_key", timeout.Token, vaultId: vaultId, password: changedPassword);
        AssertRecoveryKey(changedRecovery.RootElement, fixtureRecoveryKey, "recovery-key display after password change");

        using JsonDocument resetPassword = await InvokeAsync(stream, "vault.reset_password", timeout.Token, vaultId: vaultId, recoveryKey: fixtureRecoveryKey, newPassword: recoveredPassword);
        AssertProtocolSuccess(resetPassword.RootElement, "password_reset", "disposable password recovery");
        using JsonDocument changedPasswordAfterReset = await InvokeAsync(stream, "vault.show_recovery_key", timeout.Token, vaultId: vaultId, password: changedPassword);
        AssertProtocolError(changedPasswordAfterReset.RootElement, "wrong_password", "retired password after recovery");
        using JsonDocument recoveredRecovery = await InvokeAsync(stream, "vault.show_recovery_key", timeout.Token, vaultId: vaultId, password: recoveredPassword);
        AssertRecoveryKey(recoveredRecovery.RootElement, fixtureRecoveryKey, "recovery-key display after password recovery");

        const string renamedVault = "Disposable Probe Vault";
        using JsonDocument renameVault = await InvokeAsync(stream, "vault.rename", timeout.Token, vaultId: vaultId, displayName: renamedVault);
        AssertProtocolSuccess(renameVault.RootElement, "renamed", "disposable vault rename");
        using JsonDocument renamedVaultList = await InvokeAsync(stream, "vault.list", timeout.Token);
        JsonElement renamedEntry = renamedVaultList.RootElement.GetProperty("vaults").EnumerateArray().Single(entry => entry.GetProperty("id").GetString() == vaultId);
        if (renamedEntry.GetProperty("name").GetString() != renamedVault)
        {
            throw new InvalidDataException("The disposable vault rename was not reflected in the isolated profile.");
        }

        using JsonDocument removeVault = await InvokeAsync(stream, "vault.remove", timeout.Token, vaultId: vaultId);
        AssertProtocolSuccess(removeVault.RootElement, "removed", "disposable vault removal");
        using JsonDocument connectVault = await InvokeAsync(stream, "vault.connect", timeout.Token, vaultPath: fixtureVaultPath);
        AssertProtocolSuccess(connectVault.RootElement, "created", "disposable vault reconnection");
        string reconnectedVaultId = connectVault.RootElement.GetProperty("vaultId").GetString() ?? throw new InvalidDataException("Disposable vault reconnection returned no vault ID.");
        using JsonDocument removeReconnectedVault = await InvokeAsync(stream, "vault.remove", timeout.Token, vaultId: reconnectedVaultId);
        AssertProtocolSuccess(removeReconnectedVault.RootElement, "removed", "reconnected disposable vault removal");

        using JsonDocument finalVaultList = await InvokeAsync(stream, "vault.list", timeout.Token);
        if (!finalVaultList.RootElement.GetProperty("ok").GetBoolean()
            || finalVaultList.RootElement.GetProperty("vaults").GetArrayLength() != 0)
        {
            throw new InvalidDataException("The disposable fixture remained registered after probe cleanup.");
        }

        (string Operation, string? Password, string? RecoveryKey, string? NewPassword)[] vaultOperations =
        [
            ("vault.unlock", "dummy-password", null, null),
            ("vault.lock", null, null, null),
            ("vault.reveal", null, null, null),
            ("vault.remove", null, null, null),
            ("vault.rename", null, null, null),
            ("vault.stats", null, null, null),
            ("vault.locate_encrypted", null, null, null),
            ("vault.decrypt_filename", null, null, null),
            ("vault.reset_password", null, "dummy-recovery-key", "dummy-new-password"),
            ("vault.change_password", "dummy-password", null, "dummy-new-password"),
            ("vault.show_recovery_key", "dummy-password", null, null)
        ];
        foreach ((string operation, string? password, string? recoveryKey, string? newPassword) in vaultOperations)
        {
            using JsonDocument missingVault = await InvokeAsync(stream, operation, timeout.Token, vaultId: "missing-vault", password: password, recoveryKey: recoveryKey, newPassword: newPassword);
            AssertProtocolError(missingVault.RootElement, "vault_not_found", operation);
        }

        using JsonDocument wrongProtocol = await InvokeAsync(stream, "backend.hello", timeout.Token, protocol: 2);
        AssertProtocolError(wrongProtocol.RootElement, "unsupported_protocol", "unsupported protocol");

        using JsonDocument unknownOperation = await InvokeAsync(stream, "vault.delete", timeout.Token);
        AssertProtocolError(unknownOperation.RootElement, "unknown_operation", "unknown operation");

        using JsonDocument shutdown = await InvokeAsync(stream, "backend.shutdown", timeout.Token);
        if (!shutdown.RootElement.GetProperty("ok").GetBoolean())
        {
            throw new InvalidDataException("The bundled engine rejected backend.shutdown.");
        }

        Console.WriteLine($"Backend: VaultKind Java Engine; verified malformed-request isolation, disposable create/connect, recovery and password rotation, rename/remove, {vaultOperations.Length} missing-vault commands, mount providers, protocol errors, and shutdown.");
        return 0;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"Bundled engine protocol probe failed: {exception.Message}");
        return 1;
    }
}

static async Task<int> RestoreVaultRegistrationAsync(string socketPath, string displayName, string vaultPath)
{
    try
    {
        string resolvedVaultPath = Path.GetFullPath(vaultPath);
        if (!File.Exists(Path.Combine(resolvedVaultPath, "vault.cryptomator"))
            || !Directory.Exists(Path.Combine(resolvedVaultPath, "d")))
        {
            throw new InvalidDataException("The requested encrypted vault structure is incomplete.");
        }

        using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), timeout.Token);
        await using var stream = new NetworkStream(socket, ownsSocket: false);

        using JsonDocument hello = await InvokeAsync(stream, "backend.hello", timeout.Token);
        JsonElement helloRoot = hello.RootElement;
        string expectedProfile = JavaVaultEngineHost.ResolveExpectedSettingsPath();
        if (!helloRoot.GetProperty("ok").GetBoolean()
            || helloRoot.GetProperty("backend").GetString() != "VaultKind Java Engine"
            || !JavaVaultEngineHost.IsExpectedProfile(helloRoot.GetProperty("profile").GetString(), expectedProfile))
        {
            throw new InvalidDataException("The live engine did not report the expected persistent VaultKind profile.");
        }

        using JsonDocument before = await InvokeAsync(stream, "vault.list", timeout.Token);
        if (!before.RootElement.GetProperty("ok").GetBoolean()
            || before.RootElement.GetProperty("vaults").GetArrayLength() != 0)
        {
            throw new InvalidDataException("The persistent vault list is not empty; refusing an automatic registration repair.");
        }

        using JsonDocument connect = await InvokeAsync(stream, "vault.connect", timeout.Token, displayName: displayName, vaultPath: resolvedVaultPath);
        AssertProtocolSuccess(connect.RootElement, "created", "persistent vault registration repair");

        using JsonDocument after = await InvokeAsync(stream, "vault.list", timeout.Token);
        JsonElement[] registeredVaults = after.RootElement.GetProperty("vaults").EnumerateArray().ToArray();
        if (registeredVaults.Length != 1
            || !Path.GetFullPath(registeredVaults[0].GetProperty("path").GetString() ?? string.Empty).Equals(resolvedVaultPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The repaired vault registration did not read back correctly.");
        }

        Console.WriteLine($"Restored {registeredVaults[0].GetProperty("name").GetString()} at {resolvedVaultPath} in {expectedProfile}.");
        return 0;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"Vault registration repair failed: {exception.Message}");
        return 1;
    }
}

static async Task<int> InspectLiveEngineAsync(string socketPath)
{
    try
    {
        using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), timeout.Token);
        await using var stream = new NetworkStream(socket, ownsSocket: false);

        using JsonDocument hello = await InvokeAsync(stream, "backend.hello", timeout.Token);
        using JsonDocument vaultList = await InvokeAsync(stream, "vault.list", timeout.Token);
        using JsonDocument mountSettings = await InvokeAsync(stream, "settings.mount.list", timeout.Token);
        JsonElement helloRoot = hello.RootElement;
        JsonElement mountSettingsRoot = mountSettings.RootElement;
        string expectedProfile = JavaVaultEngineHost.ResolveExpectedSettingsPath();
        if (!helloRoot.GetProperty("ok").GetBoolean()
            || helloRoot.GetProperty("backend").GetString() != "VaultKind Java Engine"
            || !JavaVaultEngineHost.IsExpectedProfile(helloRoot.GetProperty("profile").GetString(), expectedProfile)
            || !vaultList.RootElement.GetProperty("ok").GetBoolean()
            || !mountSettingsRoot.GetProperty("ok").GetBoolean())
        {
            throw new InvalidDataException("The live engine identity, profile, vault list, or mount-provider list was invalid.");
        }

        JsonElement[] vaults = vaultList.RootElement.GetProperty("vaults").EnumerateArray().ToArray();
        JsonElement[] mountServices = mountSettingsRoot.GetProperty("mountServices").EnumerateArray().ToArray();
        string? selectedMountService = mountSettingsRoot.GetProperty("selectedMountService").GetString();
        string?[] mountServiceIds = mountServices.Select(service => service.GetProperty("id").GetString()).ToArray();
        if (mountServiceIds.Any(string.IsNullOrWhiteSpace)
            || mountServiceIds.Distinct(StringComparer.Ordinal).Count() != mountServiceIds.Length
            || !mountServiceIds.Contains("automatic", StringComparer.Ordinal)
            || !mountServiceIds.Contains(selectedMountService, StringComparer.Ordinal))
        {
            throw new InvalidDataException("The live engine returned an inconsistent mount-provider selection or inventory.");
        }
        Console.WriteLine($"Backend: VaultKind Java Engine; profile: {expectedProfile}; vaults: {vaults.Length}.");
        foreach (JsonElement vault in vaults)
        {
            Console.WriteLine($"{vault.GetProperty("name").GetString()} | {vault.GetProperty("state").GetString()} | {vault.GetProperty("path").GetString()}");
        }
        Console.WriteLine($"Selected mount provider: {selectedMountService}; providers: {mountServices.Length}.");
        foreach (JsonElement service in mountServices)
        {
            Console.WriteLine($"{service.GetProperty("id").GetString()} | {service.GetProperty("name").GetString()} | mountPoint={service.GetProperty("mountPoint").GetBoolean()} | driveLetter={service.GetProperty("driveLetter").GetBoolean()} | loopbackPort={service.GetProperty("loopbackPort").GetBoolean()} | mountFlags={service.GetProperty("mountFlags").GetBoolean()} | readOnly={service.GetProperty("readOnly").GetBoolean()}");
        }
        return 0;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"Live engine inspection failed: {exception.Message}");
        return 1;
    }
}

static async Task AssertMalformedRequestRejectedAsync(string socketPath)
{
    using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), timeout.Token);
    await using var stream = new NetworkStream(socket, ownsSocket: false);
    byte[] malformedPayload = "{"u8.ToArray();
    byte[] header = new byte[sizeof(int)];
    BinaryPrimitives.WriteInt32BigEndian(header, malformedPayload.Length);
    await stream.WriteAsync(header, timeout.Token);
    await stream.WriteAsync(malformedPayload, timeout.Token);
    await stream.FlushAsync(timeout.Token);

    byte[] response = new byte[1];
    int bytesRead = await stream.ReadAsync(response, timeout.Token);
    if (bytesRead != 0)
    {
        throw new InvalidDataException("The bundled engine returned data for malformed JSON.");
    }
}

static void AssertProtocolError(JsonElement response, string expectedError, string scenario)
{
    if (response.GetProperty("ok").GetBoolean()
        || response.GetProperty("error").GetString() != expectedError)
    {
        throw new InvalidDataException($"Unexpected response for {scenario}; expected {expectedError}.");
    }
}

static void AssertProtocolSuccess(JsonElement response, string expectedState, string scenario)
{
    if (!response.GetProperty("ok").GetBoolean()
        || response.GetProperty("state").GetString() != expectedState)
    {
        throw new InvalidDataException($"Unexpected response for {scenario}; expected successful state {expectedState}.");
    }
}

static void AssertRecoveryKey(JsonElement response, string expectedRecoveryKey, string scenario)
{
    AssertProtocolSuccess(response, "recovery_key_ready", scenario);
    if (response.GetProperty("recoveryKey").GetString() != expectedRecoveryKey)
    {
        throw new InvalidDataException($"The recovery key changed during {scenario}.");
    }
}

static async Task<JsonDocument> InvokeAsync(NetworkStream stream, string operation, CancellationToken cancellationToken, int protocol = 1, string? vaultId = null, string? password = null, string? recoveryKey = null, string? newPassword = null, string? displayName = null, string? vaultPath = null, bool createRecoveryKey = false, bool useShortNames = false)
{
    string requestId = Guid.NewGuid().ToString("N");
    byte[] payload = JsonSerializer.SerializeToUtf8Bytes(new { protocol, requestId, operation, vaultId, password, recoveryKey, newPassword, displayName, vaultPath, createRecoveryKey, useShortNames });
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
