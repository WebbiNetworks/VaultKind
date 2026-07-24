package org.cryptomator.common.vaults;

import java.util.Collection;
import java.util.List;
import java.util.Locale;
import javax.inject.Inject;

/**
 * Converts the live vault list into a read-only snapshot for the native UI.
 * Secret-bearing data is intentionally excluded. The user-facing local path is
 * included because the native sidebar uses it to identify configured vaults.
 */
public class VaultListSnapshotMapper {

	@Inject
	public VaultListSnapshotMapper() {
	}

	public List<VaultSummary> map(Collection<Vault> vaults) {
		return vaults.stream() //
				.map(this::map) //
				.toList();
	}

	private VaultSummary map(Vault vault) {
		return new VaultSummary(vault.getId(), vault.getDisplayName(), vault.getState().name().toLowerCase(Locale.ROOT), vault.getDisplayablePath());
	}
}
