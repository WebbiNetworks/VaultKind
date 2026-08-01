namespace VaultKind_Windows.Services;

internal interface IVaultBackend
{
    Task<VaultBackendSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
    Task<VaultCommandResult> UnlockAsync(string vaultId, string password, CancellationToken cancellationToken = default);
    Task<VaultCommandResult> LockAsync(string vaultId, CancellationToken cancellationToken = default);
    Task<VaultCommandResult> RevealAsync(string vaultId, CancellationToken cancellationToken = default);
    Task<VaultCommandResult> RemoveAsync(string vaultId, CancellationToken cancellationToken = default);
    Task<VaultCommandResult> RenameAsync(string vaultId, string displayName, CancellationToken cancellationToken = default);
    Task<VaultStatisticsResult> GetStatisticsAsync(string vaultId, CancellationToken cancellationToken = default);
    Task<FileNameDecryptResult> LocateEncryptedFileAsync(string vaultId, string filePath, CancellationToken cancellationToken = default);
    Task<FileNameDecryptResult> DecryptFileNameAsync(string vaultId, string filePath, CancellationToken cancellationToken = default);
    Task<VaultCommandResult> ChangePasswordAsync(string vaultId, string currentPassword, string newPassword, CancellationToken cancellationToken = default);
    Task<VaultCommandResult> ShowRecoveryKeyAsync(string vaultId, string password, CancellationToken cancellationToken = default);
    Task<VaultCommandResult> ResetPasswordAsync(string vaultId, string recoveryKey, string newPassword, CancellationToken cancellationToken = default);
    Task<VaultCreateResult> CreateAsync(string path, string displayName, string password, bool createRecoveryKey, bool useShortNames, CancellationToken cancellationToken = default);
    Task<VaultCreateResult> ConnectAsync(string path, CancellationToken cancellationToken = default);
    Task<MountSettingsResult> GetMountSettingsAsync(CancellationToken cancellationToken = default);
    Task<MountSettingsResult> SetMountServiceAsync(string mountServiceId, CancellationToken cancellationToken = default);
}

internal enum BackendConnectionState
{
    Disconnected,
    Connecting,
    Ready,
    Unavailable
}

internal sealed record VaultSummary(string Id, string Name, string State, string Path, string? MountPath);

internal sealed record VaultCommandResult(bool Succeeded, string? Error, string? State, string? RecoveryKey = null);

internal sealed record VaultCreateResult(bool Succeeded, string? Error, string? State, string? VaultId, string? RecoveryKey);

internal sealed record VaultStatisticsResult(bool Succeeded, string? Error, VaultStatistics? Statistics);

internal sealed record FileNameDecryptResult(bool Succeeded, string? Error, FileNameMapping? Mapping);

internal sealed record FileNameMapping(string EncryptedName, string CleartextName);

internal sealed record MountServiceOption(string Id, string Name, bool MountPoint, bool DriveLetter, bool LoopbackPort, bool MountFlags, bool ReadOnly);

internal sealed record MountSettingsResult(bool Succeeded, string? Error, string? SelectedMountService, IReadOnlyList<MountServiceOption> MountServices);

internal sealed record VaultStatistics(
    long BytesPerSecondRead,
    long BytesPerSecondWritten,
    long BytesPerSecondDecrypted,
    long BytesPerSecondEncrypted,
    double CacheHitRate,
    long TotalBytesRead,
    long TotalBytesWritten,
    long TotalBytesDecrypted,
    long TotalBytesEncrypted,
    long TotalFilesAccessed);

internal sealed record VaultBackendSnapshot(
    BackendConnectionState ConnectionState,
    IReadOnlyList<VaultSummary> Vaults,
    string StatusMessage)
{
    internal int UnlockedCount => Vaults.Count(vault => vault.State.Equals("unlocked", StringComparison.OrdinalIgnoreCase));
    internal int LockedCount => Vaults.Count(vault => vault.State.Equals("locked", StringComparison.OrdinalIgnoreCase));
}
