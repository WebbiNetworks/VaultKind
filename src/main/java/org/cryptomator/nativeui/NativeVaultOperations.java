package org.cryptomator.nativeui;

import org.cryptomator.common.Constants;
import org.cryptomator.common.vaults.Vault;
import org.cryptomator.common.vaults.VaultState;
import org.cryptomator.cryptofs.common.BackupHelper;
import org.cryptomator.cryptolib.api.InvalidPassphraseException;
import org.cryptomator.cryptolib.api.MasterkeyLoadingFailedException;
import org.cryptomator.cryptolib.common.MasterkeyFileAccess;
import org.cryptomator.integrations.mount.MountFailedException;
import org.cryptomator.integrations.mount.Mountpoint;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import javax.inject.Inject;
import javafx.collections.ObservableList;
import java.awt.Desktop;
import java.io.IOException;
import java.nio.CharBuffer;
import java.nio.file.Files;
import java.util.Arrays;
import java.util.List;

public class NativeVaultOperations {

	private static final Logger LOG = LoggerFactory.getLogger(NativeVaultOperations.class);
	private static final int MAX_PASSWORD_CHARS = 4096;
	private final ObservableList<Vault> vaults;
	private final MasterkeyFileAccess masterkeyFileAccess;

	@Inject
	public NativeVaultOperations(ObservableList<Vault> vaults, MasterkeyFileAccess masterkeyFileAccess) {
		this.vaults = vaults;
		this.masterkeyFileAccess = masterkeyFileAccess;
	}

	public NativeCommandResult execute(String operation, String vaultId, char[] password) {
		return switch (operation) {
			case "vault.unlock" -> unlock(vaultId, password);
			case "vault.lock" -> lock(vaultId);
			case "vault.reveal" -> reveal(vaultId);
			default -> NativeCommandResult.error("unsupported_operation");
		};
	}

	public NativeCommandResult unlock(String vaultId, char[] password) {
		if (vaultId == null || vaultId.isBlank() || password == null || password.length == 0 || password.length > MAX_PASSWORD_CHARS) {
			clear(password);
			return NativeCommandResult.error("invalid_request");
		}

		try {
			var vault = vaults.stream().filter(candidate -> vaultId.equals(candidate.getId())).findFirst().orElse(null);
			if (vault == null) {
				return NativeCommandResult.error("vault_not_found");
			}
			if (!vault.stateProperty().transition(VaultState.Value.LOCKED, VaultState.Value.PROCESSING)) {
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
				vault.stateProperty().transition(VaultState.Value.PROCESSING, VaultState.Value.UNLOCKED);
				return NativeCommandResult.success("unlocked");
			} catch (InvalidPassphraseException e) {
				vault.stateProperty().transition(VaultState.Value.PROCESSING, VaultState.Value.LOCKED);
				return NativeCommandResult.error("wrong_password");
			} catch (MountFailedException e) {
				vault.stateProperty().transition(VaultState.Value.PROCESSING, VaultState.Value.LOCKED);
				LOG.warn("Native unlock could not mount vault {}", vault.getDisplayName(), e);
				return NativeCommandResult.error("mount_failed");
			} catch (IOException | RuntimeException e) {
				vault.stateProperty().transition(VaultState.Value.PROCESSING, VaultState.Value.LOCKED);
				LOG.warn("Native unlock failed for vault {}", vault.getDisplayName(), e);
				return NativeCommandResult.error("unlock_failed");
			}
		} finally {
			clear(password);
		}
	}

	public NativeCommandResult lockAll() {
		for (var vault : List.copyOf(vaults)) {
			if (!vault.isUnlocked()) {
				continue;
			}
			if (!vault.stateProperty().transition(VaultState.Value.UNLOCKED, VaultState.Value.PROCESSING)) {
				return NativeCommandResult.error("invalid_state");
			}
			try {
				vault.lock(false);
				vault.stateProperty().transition(VaultState.Value.PROCESSING, VaultState.Value.LOCKED);
			} catch (Exception e) {
				vault.stateProperty().transition(VaultState.Value.PROCESSING, VaultState.Value.UNLOCKED);
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
		if (!vault.stateProperty().transition(VaultState.Value.UNLOCKED, VaultState.Value.PROCESSING)) {
			return NativeCommandResult.error(vault.isUnlocked() ? "invalid_state" : "already_locked");
		}
		try {
			vault.lock(false);
			vault.stateProperty().transition(VaultState.Value.PROCESSING, VaultState.Value.LOCKED);
			return NativeCommandResult.success("locked");
		} catch (Exception e) {
			vault.stateProperty().transition(VaultState.Value.PROCESSING, VaultState.Value.UNLOCKED);
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
		return vaults.stream().filter(candidate -> vaultId.equals(candidate.getId())).findFirst().orElse(null);
	}

	private static void clear(char[] password) {
		if (password != null) {
			Arrays.fill(password, '\0');
		}
	}

	public record NativeCommandResult(boolean ok, String error, String state) {
		static NativeCommandResult success(String state) {
			return new NativeCommandResult(true, null, state);
		}

		static NativeCommandResult error(String error) {
			return new NativeCommandResult(false, error, null);
		}
	}
}
