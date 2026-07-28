package org.cryptomator.nativeui;

import org.cryptomator.common.Constants;
import org.cryptomator.common.vaults.Vault;
import org.cryptomator.common.vaults.VaultRegistry;
import org.cryptomator.common.vaults.VaultState;
import org.cryptomator.cryptofs.common.BackupHelper;
import org.cryptomator.cryptofs.VaultKeyInvalidException;
import org.cryptomator.cryptolib.api.CryptoException;
import org.cryptomator.cryptolib.api.InvalidPassphraseException;
import org.cryptomator.cryptolib.api.MasterkeyLoadingFailedException;
import org.cryptomator.cryptolib.common.MasterkeyFileAccess;
import org.cryptomator.integrations.mount.MountFailedException;
import org.cryptomator.integrations.mount.Mountpoint;
import org.cryptomator.common.recovery.RecoveryKeyFactory;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import javax.inject.Inject;
import java.awt.Desktop;
import java.io.IOException;
import java.nio.CharBuffer;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.StandardCopyOption;
import java.nio.file.StandardOpenOption;
import java.util.Arrays;
import java.util.List;

public class NativeVaultOperations {

	private static final Logger LOG = LoggerFactory.getLogger(NativeVaultOperations.class);
	private static final int MAX_PASSWORD_CHARS = 4096;
	private final VaultRegistry vaults;
	private final MasterkeyFileAccess masterkeyFileAccess;
	private final RecoveryKeyFactory recoveryKeyFactory;

	@Inject
	public NativeVaultOperations(VaultRegistry vaults, MasterkeyFileAccess masterkeyFileAccess, RecoveryKeyFactory recoveryKeyFactory) {
		this.vaults = vaults;
		this.masterkeyFileAccess = masterkeyFileAccess;
		this.recoveryKeyFactory = recoveryKeyFactory;
	}

	public NativeCommandResult execute(String operation, String vaultId, char[] password, char[] recoveryKey, char[] newPassword, String displayName, String vaultPath) {
		return switch (operation) {
			case "vault.unlock" -> unlock(vaultId, password);
			case "vault.lock" -> lock(vaultId);
			case "vault.reveal" -> reveal(vaultId);
			case "vault.remove" -> remove(vaultId);
			case "vault.rename" -> rename(vaultId, displayName);
			case "vault.stats" -> statistics(vaultId);
			case "vault.locate_encrypted" -> locateEncryptedFile(vaultId, vaultPath);
			case "vault.decrypt_filename" -> decryptFileName(vaultId, vaultPath);
			case "vault.reset_password" -> resetPassword(vaultId, recoveryKey, newPassword);
			case "vault.change_password" -> changePassword(vaultId, password, newPassword);
			case "vault.show_recovery_key" -> showRecoveryKey(vaultId, password);
			default -> NativeCommandResult.error("unsupported_operation");
		};
	}

	public NativeCommandResult locateEncryptedFile(String vaultId, String filePath) {
		var vault = findVault(vaultId);
		if (vault == null) {
			return NativeCommandResult.error("vault_not_found");
		}
		if (!vault.isUnlocked()) {
			return NativeCommandResult.error("vault_locked");
		}
		if (filePath == null || filePath.isBlank()) {
			return NativeCommandResult.error("invalid_request");
		}

		try {
			Path selectedPath = Path.of(filePath).toAbsolutePath().normalize();
			if (!Files.isRegularFile(selectedPath)) {
				return NativeCommandResult.error("invalid_request");
			}
			if (!(vault.getMountPoint() instanceof Mountpoint.WithPath mountpoint)) {
				return NativeCommandResult.error("unsupported_mount");
			}
			Path readableRoot = normalizedMountRoot(mountpoint.path());
			if (!selectedPath.startsWith(readableRoot)) {
				return NativeCommandResult.error("foreign_file");
			}
			// A Windows drive mount can be represented as either "H:" or "H:\\".
			// Preserve the mountpoint's original path form when handing the path back
			// to Vault, while using an absolute drive root for the containment check.
			Path relativePath = readableRoot.relativize(selectedPath);
			Path vaultCompatiblePath = mountpoint.path().resolve(relativePath).normalize();
			Path encryptedPath = vault.getCiphertextPath(vaultCompatiblePath).toAbsolutePath().normalize();
			return NativeCommandResult.fileName(new FileNameMapping(encryptedPath.toString(), selectedPath.getFileName().toString()));
		} catch (IOException e) {
			LOG.info("Native encrypted-file location failed for {}", filePath, e);
			return NativeCommandResult.error("locate_encrypted_failed");
		} catch (IllegalArgumentException e) {
			return NativeCommandResult.error("foreign_file");
		} catch (UnsupportedOperationException e) {
			return NativeCommandResult.error("unsupported_mount");
		}
	}

	private Path normalizedMountRoot(Path mountPath) {
		String mountText = mountPath.toString();
		if (mountText.matches("(?i)^[a-z]:$")) {
			return Path.of(mountText + "\\").toAbsolutePath().normalize();
		}
		return mountPath.toAbsolutePath().normalize();
	}

	public NativeCommandResult decryptFileName(String vaultId, String filePath) {
		var vault = findVault(vaultId);
		if (vault == null) {
			return NativeCommandResult.error("vault_not_found");
		}
		if (!vault.isUnlocked()) {
			return NativeCommandResult.error("vault_locked");
		}
		if (filePath == null || filePath.isBlank()) {
			return NativeCommandResult.error("invalid_request");
		}

		try {
			Path selectedPath = Path.of(filePath).toAbsolutePath().normalize();
			Path vaultPath = vault.getPath().toAbsolutePath().normalize();
			if (!selectedPath.startsWith(vaultPath) || !Files.isRegularFile(selectedPath)) {
				return NativeCommandResult.error("foreign_file");
			}
			String cleartextName = vault.getCleartextName(selectedPath);
			return NativeCommandResult.fileName(new FileNameMapping(selectedPath.getFileName().toString(), cleartextName));
		} catch (IOException e) {
			LOG.info("Native filename decryption failed for {}", filePath, e);
			return NativeCommandResult.error("decrypt_filename_failed");
		} catch (IllegalArgumentException | UnsupportedOperationException e) {
			return NativeCommandResult.error("vault_internal_file");
		}
	}

	public NativeCommandResult statistics(String vaultId) {
		var vault = findVault(vaultId);
		if (vault == null) {
			return NativeCommandResult.error("vault_not_found");
		}
		if (!vault.isUnlocked()) {
			return NativeCommandResult.error("vault_locked");
		}
		return NativeCommandResult.statistics(vault.getStats().nativeSnapshot());
	}

	public NativeCommandResult rename(String vaultId, String displayName) {
		var vault = findVault(vaultId);
		if (vault == null) {
			return NativeCommandResult.error("vault_not_found");
		}
		if (vault.getState() == VaultState.Value.PROCESSING) {
			return NativeCommandResult.error("invalid_state");
		}
		String trimmedName = displayName == null ? "" : displayName.trim();
		if (trimmedName.isEmpty() || trimmedName.length() > 50) {
			return NativeCommandResult.error("invalid_name");
		}

		vault.setDisplayName(trimmedName);
		return NativeCommandResult.success("renamed");
	}

	public NativeCommandResult showRecoveryKey(String vaultId, char[] password) {
		if (vaultId == null || vaultId.isBlank() || password == null || password.length == 0 || password.length > MAX_PASSWORD_CHARS) {
			clear(password);
			return NativeCommandResult.error("invalid_request");
		}

		try {
			var vault = findVault(vaultId);
			if (vault == null) {
				return NativeCommandResult.error("vault_not_found");
			}
			if (vault.getState() == VaultState.Value.PROCESSING) {
				return NativeCommandResult.error("invalid_state");
			}
			String recoveryKey = recoveryKeyFactory.createRecoveryKey(vault.getPath(), CharBuffer.wrap(password));
			return NativeCommandResult.recoveryKey(recoveryKey);
		} catch (InvalidPassphraseException e) {
			return NativeCommandResult.error("wrong_password");
		} catch (IOException | CryptoException e) {
			LOG.warn("Native recovery-key display failed", e);
			return NativeCommandResult.error("recovery_key_failed");
		} finally {
			clear(password);
		}
	}

	public NativeCommandResult changePassword(String vaultId, char[] currentPassword, char[] newPassword) {
		if (vaultId == null || vaultId.isBlank() || currentPassword == null || currentPassword.length == 0 || currentPassword.length > MAX_PASSWORD_CHARS || newPassword == null || newPassword.length < 8 || newPassword.length > MAX_PASSWORD_CHARS) {
			clear(currentPassword);
			clear(newPassword);
			return NativeCommandResult.error("invalid_request");
		}

		try {
			var vault = findVault(vaultId);
			if (vault == null) {
				return NativeCommandResult.error("vault_not_found");
			}
			if (vault.isUnlocked()) {
				return NativeCommandResult.error("vault_unlocked");
			}
			if (vault.getState() == VaultState.Value.PROCESSING) {
				return NativeCommandResult.error("invalid_state");
			}

			var masterkeyPath = vault.getPath().resolve(Constants.MASTERKEY_FILENAME);
			byte[] oldMasterkeyBytes = Files.readAllBytes(masterkeyPath);
			byte[] newMasterkeyBytes = masterkeyFileAccess.changePassphrase(oldMasterkeyBytes, CharBuffer.wrap(currentPassword), CharBuffer.wrap(newPassword));
			var backupKeyPath = vault.getPath().resolve(Constants.MASTERKEY_FILENAME + BackupHelper.generateFileIdSuffix(oldMasterkeyBytes) + Constants.MASTERKEY_BACKUP_SUFFIX);
			Files.move(masterkeyPath, backupKeyPath, StandardCopyOption.REPLACE_EXISTING, StandardCopyOption.ATOMIC_MOVE);
			try {
				Files.write(masterkeyPath, newMasterkeyBytes, StandardOpenOption.CREATE_NEW, StandardOpenOption.WRITE);
			} catch (IOException e) {
				Files.move(backupKeyPath, masterkeyPath, StandardCopyOption.REPLACE_EXISTING, StandardCopyOption.ATOMIC_MOVE);
				throw e;
			}
			return NativeCommandResult.success("password_changed");
		} catch (InvalidPassphraseException e) {
			return NativeCommandResult.error("wrong_password");
		} catch (IOException | CryptoException e) {
			LOG.warn("Native password change failed", e);
			return NativeCommandResult.error("password_change_failed");
		} finally {
			clear(currentPassword);
			clear(newPassword);
		}
	}

	public NativeCommandResult resetPassword(String vaultId, char[] recoveryKey, char[] newPassword) {
		if (vaultId == null || vaultId.isBlank() || recoveryKey == null || recoveryKey.length == 0 || newPassword == null || newPassword.length < 8 || newPassword.length > MAX_PASSWORD_CHARS) {
			clear(recoveryKey);
			clear(newPassword);
			return NativeCommandResult.error("invalid_request");
		}

		try {
			var vault = findVault(vaultId);
			if (vault == null) {
				return NativeCommandResult.error("vault_not_found");
			}
			if (vault.isUnlocked()) {
				return NativeCommandResult.error("vault_unlocked");
			}
			if (vault.getState() == VaultState.Value.PROCESSING) {
				return NativeCommandResult.error("invalid_state");
			}

			String recoveryPhrase = new String(recoveryKey);
			var unverifiedConfig = vault.getVaultConfigCache().get();
			boolean belongsToVault = recoveryKeyFactory.validateRecoveryKey(recoveryPhrase, rawKey -> {
				try {
					unverifiedConfig.verify(rawKey, unverifiedConfig.allegedVaultVersion());
					return true;
				} catch (Exception e) {
					return false;
				}
			});
			if (!belongsToVault) {
				return NativeCommandResult.error("invalid_recovery_key");
			}

			recoveryKeyFactory.newMasterkeyFileWithPassphrase(vault.getPath(), recoveryPhrase, CharBuffer.wrap(newPassword));
			return NativeCommandResult.success("password_reset");
		} catch (IOException e) {
			LOG.warn("Native password recovery could not update the masterkey file", e);
			return NativeCommandResult.error("recovery_write_failed");
		} catch (IllegalArgumentException e) {
			return NativeCommandResult.error("invalid_recovery_key");
		} finally {
			clear(recoveryKey);
			clear(newPassword);
		}
	}

	public NativeCommandResult remove(String vaultId) {
		var vault = findVault(vaultId);
		if (vault == null) {
			return NativeCommandResult.error("vault_not_found");
		}
		if (vault.isUnlocked()) {
			return NativeCommandResult.error("vault_unlocked");
		}
		if (vault.getState() == VaultState.Value.PROCESSING) {
			return NativeCommandResult.error("invalid_state");
		}

		vaults.remove(vault);
		return NativeCommandResult.success("removed");
	}

	public NativeCommandResult unlock(String vaultId, char[] password) {
		if (vaultId == null || vaultId.isBlank() || password == null || password.length == 0 || password.length > MAX_PASSWORD_CHARS) {
			clear(password);
			return NativeCommandResult.error("invalid_request");
		}

		try {
			var vault = vaults.findById(vaultId).orElse(null);
			if (vault == null) {
				return NativeCommandResult.error("vault_not_found");
			}
			if (!vault.transitionState(VaultState.Value.LOCKED, VaultState.Value.PROCESSING)) {
				return NativeCommandResult.error(vault.isUnlocked() ? "already_unlocked" : "invalid_state");
			}

			try {
				vault.unlock(keyId -> {
					if (!"masterkeyfile".equalsIgnoreCase(keyId.getScheme())) {
						throw new MasterkeyLoadingFailedException("Unsupported key source for native password unlock");
					}
					var masterkeyPath = vault.getPath().resolve(Constants.MASTERKEY_FILENAME);
					if (!Files.isRegularFile(masterkeyPath)) {
						throw new MasterkeyLoadingFailedException("Vault masterkey file is unavailable");
					}
					var masterkey = masterkeyFileAccess.load(masterkeyPath, CharBuffer.wrap(password));
					try {
						BackupHelper.attemptBackup(masterkeyPath);
					} catch (IOException e) {
						LOG.warn("Unable to create a masterkey backup after native unlock");
					}
					return masterkey;
				});
				vault.transitionState(VaultState.Value.PROCESSING, VaultState.Value.UNLOCKED);
				return NativeCommandResult.success("unlocked");
			} catch (InvalidPassphraseException e) {
				vault.transitionState(VaultState.Value.PROCESSING, VaultState.Value.LOCKED);
				return NativeCommandResult.error("wrong_password");
			} catch (MountFailedException e) {
				vault.transitionState(VaultState.Value.PROCESSING, VaultState.Value.LOCKED);
				LOG.warn("Native unlock could not mount vault {}", vault.getDisplayName(), e);
				return NativeCommandResult.error("mount_failed");
			} catch (VaultKeyInvalidException e) {
				vault.transitionState(VaultState.Value.PROCESSING, VaultState.Value.LOCKED);
				LOG.warn("Native unlock could not verify the vault configuration for {}", vault.getDisplayName(), e);
				return NativeCommandResult.error("vault_key_invalid");
			} catch (IOException | RuntimeException e) {
				vault.transitionState(VaultState.Value.PROCESSING, VaultState.Value.LOCKED);
				LOG.warn("Native unlock failed for vault {}", vault.getDisplayName(), e);
				return NativeCommandResult.error("unlock_failed");
			}
		} finally {
			clear(password);
		}
	}

	public NativeCommandResult lockAll() {
		for (var vault : vaults.snapshot()) {
			if (!vault.isUnlocked()) {
				continue;
			}
			if (!vault.transitionState(VaultState.Value.UNLOCKED, VaultState.Value.PROCESSING)) {
				return NativeCommandResult.error("invalid_state");
			}
			try {
				vault.lock(false);
				vault.transitionState(VaultState.Value.PROCESSING, VaultState.Value.LOCKED);
			} catch (Exception e) {
				vault.transitionState(VaultState.Value.PROCESSING, VaultState.Value.UNLOCKED);
				LOG.warn("Unable to lock vault {} during native shutdown", vault.getDisplayName(), e);
				return NativeCommandResult.error("vault_in_use");
			}
		}
		return NativeCommandResult.success("stopped");
	}

	public NativeCommandResult lock(String vaultId) {
		var vault = findVault(vaultId);
		if (vault == null) {
			return NativeCommandResult.error("vault_not_found");
		}
		if (!vault.transitionState(VaultState.Value.UNLOCKED, VaultState.Value.PROCESSING)) {
			return NativeCommandResult.error(vault.isUnlocked() ? "invalid_state" : "already_locked");
		}
		try {
			vault.lock(false);
			vault.transitionState(VaultState.Value.PROCESSING, VaultState.Value.LOCKED);
			return NativeCommandResult.success("locked");
		} catch (Exception e) {
			vault.transitionState(VaultState.Value.PROCESSING, VaultState.Value.UNLOCKED);
			LOG.warn("Native lock failed for vault {}", vault.getDisplayName(), e);
			return NativeCommandResult.error("vault_in_use");
		}
	}

	public NativeCommandResult reveal(String vaultId) {
		var vault = findVault(vaultId);
		if (vault == null) {
			return NativeCommandResult.error("vault_not_found");
		}
		if (!vault.isUnlocked()) {
			return NativeCommandResult.error("vault_locked");
		}
		try {
			return switch (vault.getMountPoint()) {
				case Mountpoint.WithPath mountpoint -> {
					if (!Desktop.isDesktopSupported()) {
						yield NativeCommandResult.error("reveal_unavailable");
					}
					Desktop.getDesktop().open(mountpoint.path().toFile());
					yield NativeCommandResult.success("revealed");
				}
				case null, default -> NativeCommandResult.error("mount_unavailable");
			};
		} catch (IOException | RuntimeException e) {
			LOG.warn("Unable to reveal the readable view for vault {}", vault.getDisplayName(), e);
			return NativeCommandResult.error("reveal_failed");
		}
	}

	private Vault findVault(String vaultId) {
		if (vaultId == null || vaultId.isBlank()) {
			return null;
		}
		return vaults.findById(vaultId).orElse(null);
	}

	private static void clear(char[] password) {
		if (password != null) {
			Arrays.fill(password, '\0');
		}
	}

	public record NativeCommandResult(boolean ok, String error, String state, String recoveryKey, org.cryptomator.common.vaults.VaultStats.NativeSnapshot statistics, FileNameMapping fileNameMapping) {
		static NativeCommandResult success(String state) {
			return new NativeCommandResult(true, null, state, null, null, null);
		}

		static NativeCommandResult recoveryKey(String recoveryKey) {
			return new NativeCommandResult(true, null, "recovery_key_ready", recoveryKey, null, null);
		}

		static NativeCommandResult statistics(org.cryptomator.common.vaults.VaultStats.NativeSnapshot statistics) {
			return new NativeCommandResult(true, null, "statistics_ready", null, statistics, null);
		}

		static NativeCommandResult fileName(FileNameMapping mapping) {
			return new NativeCommandResult(true, null, "filename_ready", null, null, mapping);
		}

		static NativeCommandResult error(String error) {
			return new NativeCommandResult(false, error, null, null, null, null);
		}
	}

	public record FileNameMapping(String encryptedName, String cleartextName) {}
}
