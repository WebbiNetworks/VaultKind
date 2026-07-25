package org.cryptomator.common.vaults;

/**
 * Minimal, non-sensitive vault information suitable for the native UI boundary.
 */
public record VaultSummary(String id, String name, String state, String path, String mountPath) {
}
