namespace VaultKind_Windows.Services;

internal interface IVaultBackend
{
    Task<VaultBackendSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
    Task<VaultCommandResult> UnlockAsync(string vaultId, string password, CancellationToken cancellationToken = default);
    Task<VaultCommandResult> LockAsync(string vaultId, CancellationToken cancellationToken = default);
    Task<VaultCommandResult> RevealAsync(string vaultId, CancellationToken cancellationToken = default);
    Task<VaultCreateResult> CreateAsync(string path, string password, bool createRecoveryKey, bool useShortNames, CancellationToken cancellationToken = default);
}

internal enum BackendConnectionState
{
    Disconnected,
    Connecting,
    Ready,
    Unavailable
}

internal sealed record VaultSummary(string Id, string Name, string State, string Path);

internal sealed record VaultCommandResult(bool Succeeded, string? Error, string? State);

internal sealed record VaultCreateResult(bool Succeeded, string? Error, string? State, string? VaultId, string? RecoveryKey);

internal sealed record VaultBackendSnapshot(
    BackendConnectionState ConnectionState,
    IReadOnlyList<VaultSummary> Vaults,
    string StatusMessage)
{
    internal int UnlockedCount => Vaults.Count(vault => vault.State.Equals("unlocked", StringComparison.OrdinalIgnoreCase));
    internal int LockedCount => Vaults.Count(vault => vault.State.Equals("locked", StringComparison.OrdinalIgnoreCase));
}
