namespace VaultKind_Windows.Services;

internal sealed class DisconnectedVaultBackend : IVaultBackend
{
    public Task<VaultBackendSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var snapshot = new VaultBackendSnapshot(
            BackendConnectionState.Disconnected,
            Array.Empty<VaultSummary>(),
            "VaultKind is running, but the local vault engine is not connected.");

        return Task.FromResult(snapshot);
    }

    public Task<VaultCommandResult> UnlockAsync(string vaultId, string password, CancellationToken cancellationToken = default) =>
        Task.FromResult(new VaultCommandResult(false, "engine_unavailable", null));

    public Task<VaultCommandResult> LockAsync(string vaultId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new VaultCommandResult(false, "engine_unavailable", null));

    public Task<VaultCommandResult> RevealAsync(string vaultId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new VaultCommandResult(false, "engine_unavailable", null));

    public Task<VaultCommandResult> RemoveAsync(string vaultId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new VaultCommandResult(false, "engine_unavailable", null));

    public Task<VaultCommandResult> ResetPasswordAsync(string vaultId, string recoveryKey, string newPassword, CancellationToken cancellationToken = default) =>
        Task.FromResult(new VaultCommandResult(false, "engine_unavailable", null));

    public Task<VaultCreateResult> CreateAsync(string path, string password, bool createRecoveryKey, bool useShortNames, CancellationToken cancellationToken = default) =>
        Task.FromResult(new VaultCreateResult(false, "engine_unavailable", null, null, null));

    public Task<VaultCreateResult> ConnectAsync(string path, CancellationToken cancellationToken = default) =>
        Task.FromResult(new VaultCreateResult(false, "engine_unavailable", null, null, null));
}
