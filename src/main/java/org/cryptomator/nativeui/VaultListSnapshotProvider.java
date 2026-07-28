package org.cryptomator.nativeui;

import org.cryptomator.common.vaults.VaultListSnapshotMapper;
import org.cryptomator.common.vaults.VaultRegistry;
import org.cryptomator.common.vaults.VaultSummary;

import javax.inject.Inject;
import java.io.IOException;
import java.util.List;

public class VaultListSnapshotProvider {

	private final VaultRegistry vaults;
	private final VaultListSnapshotMapper mapper;

	@Inject
	public VaultListSnapshotProvider(VaultRegistry vaults, VaultListSnapshotMapper mapper) {
		this.vaults = vaults;
		this.mapper = mapper;
	}

	public List<VaultSummary> get() throws IOException {
		return mapper.map(vaults.snapshot());
	}
}
