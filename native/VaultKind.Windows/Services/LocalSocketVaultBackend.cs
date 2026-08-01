using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text.Json;

namespace VaultKind_Windows.Services;

internal sealed class LocalSocketVaultBackend : IVaultBackend
{
    private const int ProtocolVersion = 1;
    private const int MaxMessageBytes = 64 * 1024;
    private static readonly TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(2);
    public async Task<VaultBackendSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(ConnectionTimeout);
            using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            string socketPath = VaultKindDataPaths.SocketPath;
            await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), timeout.Token);
            await using var stream = new NetworkStream(socket, ownsSocket: false);

            var requestId = Guid.NewGuid().ToString("N");
            await WriteFrameAsync(stream, new ProtocolRequest(ProtocolVersion, requestId, "backend.hello"), timeout.Token);
            var response = await ReadFrameAsync<ProtocolResponse>(stream, timeout.Token);

            if (!IsValidHello(response, requestId))
            {
                return Unavailable("The Java vault engine returned an invalid identity or protocol response.");
            }

            var listRequestId = Guid.NewGuid().ToString("N");
            await WriteFrameAsync(stream, new ProtocolRequest(ProtocolVersion, listRequestId, "vault.list"), timeout.Token);
            var listResponse = await ReadFrameAsync<ProtocolResponse>(stream, timeout.Token);
            if (listResponse.Protocol != ProtocolVersion || listResponse.RequestId != listRequestId || !listResponse.Ok || listResponse.Vaults is null)
            {
                return Unavailable("The Java vault engine returned an invalid vault summary response.");
            }

            var vaults = listResponse.Vaults
                .Where(vault => !string.IsNullOrWhiteSpace(vault.Id) && !string.IsNullOrWhiteSpace(vault.Name) && !string.IsNullOrWhiteSpace(vault.State) && !string.IsNullOrWhiteSpace(vault.Path))
                .Select(vault => new VaultSummary(vault.Id, vault.Name, vault.State, vault.Path, vault.MountPath))
                .ToArray();

            return new VaultBackendSnapshot(
                BackendConnectionState.Ready,
                vaults,
                $"Connected securely to the Java vault engine. {vaults.Length} configured vault(s) reported.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Unavailable("Waiting for the local VaultKind engine. The app remains available.");
        }
        catch (Exception)
        {
            return Unavailable("The Java vault engine is currently unavailable. No vault data was requested.");
        }
    }

    public async Task<VaultCommandResult> UnlockAsync(string vaultId, string password, CancellationToken cancellationToken = default)
        => await ExecuteCommandAsync("vault.unlock", vaultId, password, TimeSpan.FromSeconds(45), cancellationToken);

    public async Task<VaultCommandResult> LockAsync(string vaultId, CancellationToken cancellationToken = default)
        => await ExecuteCommandAsync("vault.lock", vaultId, null, TimeSpan.FromSeconds(30), cancellationToken);

    public async Task<VaultCommandResult> RevealAsync(string vaultId, CancellationToken cancellationToken = default)
        => await ExecuteCommandAsync("vault.reveal", vaultId, null, TimeSpan.FromSeconds(10), cancellationToken);

    public async Task<VaultCommandResult> RemoveAsync(string vaultId, CancellationToken cancellationToken = default)
        => await ExecuteCommandAsync("vault.remove", vaultId, null, TimeSpan.FromSeconds(10), cancellationToken);

    public async Task<VaultCommandResult> RenameAsync(string vaultId, string displayName, CancellationToken cancellationToken = default)
        => await ExecuteCommandAsync("vault.rename", vaultId, null, TimeSpan.FromSeconds(10), cancellationToken, displayName: displayName);

    public async Task<VaultStatisticsResult> GetStatisticsAsync(string vaultId, CancellationToken cancellationToken = default)
    {
        ProtocolResponse response = await ExecuteRawCommandAsync("vault.stats", vaultId, null, TimeSpan.FromSeconds(10), cancellationToken);
        return new VaultStatisticsResult(response.Ok, response.Error, response.Statistics is null
            ? null
            : new VaultStatistics(
                response.Statistics.BytesPerSecondRead,
                response.Statistics.BytesPerSecondWritten,
                response.Statistics.BytesPerSecondDecrypted,
                response.Statistics.BytesPerSecondEncrypted,
                response.Statistics.CacheHitRate,
                response.Statistics.TotalBytesRead,
                response.Statistics.TotalBytesWritten,
                response.Statistics.TotalBytesDecrypted,
                response.Statistics.TotalBytesEncrypted,
                response.Statistics.TotalFilesAccessed));
    }

    public async Task<FileNameDecryptResult> DecryptFileNameAsync(string vaultId, string filePath, CancellationToken cancellationToken = default)
    {
        ProtocolResponse response = await ExecuteRawCommandAsync("vault.decrypt_filename", vaultId, null, TimeSpan.FromSeconds(15), cancellationToken, vaultPath: filePath);
        return new FileNameDecryptResult(response.Ok, response.Error, response.FileNameMapping is null
            ? null
            : new FileNameMapping(response.FileNameMapping.EncryptedName, response.FileNameMapping.CleartextName));
    }

    public async Task<FileNameDecryptResult> LocateEncryptedFileAsync(string vaultId, string filePath, CancellationToken cancellationToken = default)
    {
        ProtocolResponse response = await ExecuteRawCommandAsync("vault.locate_encrypted", vaultId, null, TimeSpan.FromSeconds(15), cancellationToken, vaultPath: filePath);
        return new FileNameDecryptResult(response.Ok, response.Error, response.FileNameMapping is null
            ? null
            : new FileNameMapping(response.FileNameMapping.EncryptedName, response.FileNameMapping.CleartextName));
    }

    public async Task<VaultCommandResult> ChangePasswordAsync(string vaultId, string currentPassword, string newPassword, CancellationToken cancellationToken = default)
        => await ExecuteCommandAsync("vault.change_password", vaultId, currentPassword, TimeSpan.FromSeconds(45), cancellationToken, newPassword: newPassword);

    public async Task<VaultCommandResult> ShowRecoveryKeyAsync(string vaultId, string password, CancellationToken cancellationToken = default)
        => await ExecuteCommandAsync("vault.show_recovery_key", vaultId, password, TimeSpan.FromSeconds(45), cancellationToken);

    public async Task<VaultCommandResult> ResetPasswordAsync(string vaultId, string recoveryKey, string newPassword, CancellationToken cancellationToken = default)
        => await ExecuteCommandAsync("vault.reset_password", vaultId, null, TimeSpan.FromSeconds(45), cancellationToken, recoveryKey, newPassword);

    public async Task<VaultCreateResult> CreateAsync(string path, string password, bool createRecoveryKey, bool useShortNames, CancellationToken cancellationToken = default)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(90));
            using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            string socketPath = VaultKindDataPaths.SocketPath;
            await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), timeout.Token);
            await using var stream = new NetworkStream(socket, ownsSocket: false);

            var helloId = Guid.NewGuid().ToString("N");
            await WriteFrameAsync(stream, new ProtocolRequest(ProtocolVersion, helloId, "backend.hello"), timeout.Token);
            var hello = await ReadFrameAsync<ProtocolResponse>(stream, timeout.Token);
            if (!IsValidHello(hello, helloId))
            {
                return new VaultCreateResult(false, "engine_unavailable", null, null, null);
            }

            var requestId = Guid.NewGuid().ToString("N");
            await WriteFrameAsync(stream, new ProtocolRequest(ProtocolVersion, requestId, "vault.create", Password: password, VaultPath: path, CreateRecoveryKey: createRecoveryKey, UseShortNames: useShortNames), timeout.Token);
            var response = await ReadFrameAsync<ProtocolResponse>(stream, timeout.Token);
            if (response.Protocol != ProtocolVersion || response.RequestId != requestId)
            {
                return new VaultCreateResult(false, "invalid_response", null, null, null);
            }

            return new VaultCreateResult(response.Ok, response.Error, response.State, response.VaultId, response.RecoveryKey);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new VaultCreateResult(false, "timeout", null, null, null);
        }
        catch (Exception)
        {
            return new VaultCreateResult(false, "engine_unavailable", null, null, null);
        }
    }

    public async Task<VaultCreateResult> ConnectAsync(string path, CancellationToken cancellationToken = default)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(20));
            using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            string socketPath = VaultKindDataPaths.SocketPath;
            await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), timeout.Token);
            await using var stream = new NetworkStream(socket, ownsSocket: false);

            var helloId = Guid.NewGuid().ToString("N");
            await WriteFrameAsync(stream, new ProtocolRequest(ProtocolVersion, helloId, "backend.hello"), timeout.Token);
            var hello = await ReadFrameAsync<ProtocolResponse>(stream, timeout.Token);
            if (!IsValidHello(hello, helloId))
            {
                return new VaultCreateResult(false, "engine_unavailable", null, null, null);
            }

            var requestId = Guid.NewGuid().ToString("N");
            await WriteFrameAsync(stream, new ProtocolRequest(ProtocolVersion, requestId, "vault.connect", VaultPath: path), timeout.Token);
            var response = await ReadFrameAsync<ProtocolResponse>(stream, timeout.Token);
            return response.Protocol == ProtocolVersion && response.RequestId == requestId
                ? new VaultCreateResult(response.Ok, response.Error, response.State, response.VaultId, null)
                : new VaultCreateResult(false, "invalid_response", null, null, null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new VaultCreateResult(false, "timeout", null, null, null);
        }
        catch (Exception)
        {
            return new VaultCreateResult(false, "engine_unavailable", null, null, null);
        }
    }

    public async Task<MountSettingsResult> GetMountSettingsAsync(CancellationToken cancellationToken = default) =>
        ToMountSettingsResult(await ExecuteRawCommandAsync("settings.mount.list", string.Empty, null, TimeSpan.FromSeconds(10), cancellationToken));

    public async Task<MountSettingsResult> SetMountServiceAsync(string mountServiceId, CancellationToken cancellationToken = default) =>
        ToMountSettingsResult(await ExecuteRawCommandAsync("settings.mount.select", string.Empty, null, TimeSpan.FromSeconds(10), cancellationToken, mountService: mountServiceId));

    private static MountSettingsResult ToMountSettingsResult(ProtocolResponse response) => new(
        response.Ok,
        response.Error,
        response.SelectedMountService,
        response.MountServices?.Select(service => new MountServiceOption(service.Id, service.Name, service.MountPoint, service.DriveLetter, service.LoopbackPort, service.MountFlags, service.ReadOnly)).ToArray() ?? []);

    private static async Task<VaultCommandResult> ExecuteCommandAsync(string operation, string vaultId, string? password, TimeSpan commandTimeout, CancellationToken cancellationToken, string? recoveryKey = null, string? newPassword = null, string? displayName = null)
    {
        ProtocolResponse response = await ExecuteRawCommandAsync(operation, vaultId, password, commandTimeout, cancellationToken, recoveryKey, newPassword, displayName);
        return new VaultCommandResult(response.Ok, response.Error, response.State, response.RecoveryKey);
    }

    private static async Task<ProtocolResponse> ExecuteRawCommandAsync(string operation, string vaultId, string? password, TimeSpan commandTimeout, CancellationToken cancellationToken, string? recoveryKey = null, string? newPassword = null, string? displayName = null, string? vaultPath = null, string? mountService = null)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(commandTimeout);
            using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            string socketPath = VaultKindDataPaths.SocketPath;
            await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), timeout.Token);
            await using var stream = new NetworkStream(socket, ownsSocket: false);

            var helloId = Guid.NewGuid().ToString("N");
            await WriteFrameAsync(stream, new ProtocolRequest(ProtocolVersion, helloId, "backend.hello"), timeout.Token);
            var hello = await ReadFrameAsync<ProtocolResponse>(stream, timeout.Token);
            if (!IsValidHello(hello, helloId))
            {
                return ProtocolResponse.Failure("engine_unavailable");
            }

            var requestId = Guid.NewGuid().ToString("N");
            await WriteFrameAsync(stream, new ProtocolRequest(ProtocolVersion, requestId, operation, vaultId, password, recoveryKey, newPassword, displayName, vaultPath, MountService: mountService), timeout.Token);
            var response = await ReadFrameAsync<ProtocolResponse>(stream, timeout.Token);
            if (response.Protocol != ProtocolVersion || response.RequestId != requestId)
            {
                return ProtocolResponse.Failure("invalid_response");
            }

            return response;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ProtocolResponse.Failure("timeout");
        }
        catch (Exception)
        {
            return ProtocolResponse.Failure("engine_unavailable");
        }
    }

    private static VaultBackendSnapshot Unavailable(string message) => new(BackendConnectionState.Unavailable, [], message);

    private static bool IsValidHello(ProtocolResponse response, string requestId) =>
        JavaVaultEngineHost.IsExpectedBackendIdentity(
            response.Protocol,
            response.RequestId,
            response.Ok,
            response.Backend,
            response.Capabilities,
            response.Profile,
            requestId,
            JavaVaultEngineHost.ResolveExpectedSettingsPath());

    private static async Task WriteFrameAsync<T>(Stream stream, T message, CancellationToken cancellationToken)
    {
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions);
        if (payload.Length > MaxMessageBytes)
        {
            throw new InvalidDataException("Native bridge request exceeds the message limit.");
        }

        byte[] header = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(header, payload.Length);
        await stream.WriteAsync(header, cancellationToken);
        await stream.WriteAsync(payload, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static async Task<T> ReadFrameAsync<T>(Stream stream, CancellationToken cancellationToken)
    {
        byte[] header = new byte[sizeof(int)];
        await stream.ReadExactlyAsync(header, cancellationToken);
        int length = BinaryPrimitives.ReadInt32BigEndian(header);
        if (length <= 0 || length > MaxMessageBytes)
        {
            throw new InvalidDataException("Invalid native bridge response length.");
        }

        byte[] payload = new byte[length];
        await stream.ReadExactlyAsync(payload, cancellationToken);
        return JsonSerializer.Deserialize<T>(payload, JsonOptions) ?? throw new InvalidDataException("The native bridge response was empty.");
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record ProtocolRequest(int Protocol, string RequestId, string Operation, string? VaultId = null, string? Password = null, string? RecoveryKey = null, string? NewPassword = null, string? DisplayName = null, string? VaultPath = null, bool CreateRecoveryKey = false, bool UseShortNames = false, string? MountService = null);
    private sealed record ProtocolResponse(int Protocol, string RequestId, bool Ok, string? Backend, string? Error, IReadOnlyList<ProtocolVault>? Vaults, string? State, string? VaultId, string? RecoveryKey, ProtocolStatistics? Statistics, ProtocolFileNameMapping? FileNameMapping, IReadOnlyList<string>? Capabilities, string? SelectedMountService, IReadOnlyList<ProtocolMountService>? MountServices, string? Profile)
    {
        internal static ProtocolResponse Failure(string error) => new(ProtocolVersion, string.Empty, false, null, error, null, null, null, null, null, null, null, null, null, null);
    }
    private sealed record ProtocolVault(string Id, string Name, string State, string Path, string? MountPath);
    private sealed record ProtocolStatistics(long BytesPerSecondRead, long BytesPerSecondWritten, long BytesPerSecondDecrypted, long BytesPerSecondEncrypted, double CacheHitRate, long TotalBytesRead, long TotalBytesWritten, long TotalBytesDecrypted, long TotalBytesEncrypted, long TotalFilesAccessed);
    private sealed record ProtocolFileNameMapping(string EncryptedName, string CleartextName);
    private sealed record ProtocolMountService(string Id, string Name, bool MountPoint, bool DriveLetter, bool LoopbackPort, bool MountFlags, bool ReadOnly);
}
