namespace VaultKind_Windows.Services;

internal static class SignatureSoundPolicy
{
    internal static bool ShouldWarnForLockFailure(string? error) => error == "vault_in_use";
}
