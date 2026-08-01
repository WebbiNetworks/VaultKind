package org.cryptomator.common.mount;

import org.cryptomator.common.settings.EngineSettings;
import org.cryptomator.common.settings.VaultSettings;
import org.cryptomator.integrations.mount.MountService;

import javax.inject.Inject;
import javax.inject.Singleton;
import java.util.List;

/** Resolves global and per-vault mount-provider choices without UI properties. */
@Singleton
public class MountServiceSelector {

	private final List<MountService> mountServices;
	private final EngineSettings settings;
	private final MountService fallback;

	@Inject
	public MountServiceSelector(List<MountService> mountServices, EngineSettings settings) {
		this.mountServices = List.copyOf(mountServices);
		this.settings = settings;
		this.fallback = this.mountServices.stream().findFirst().orElseThrow(() -> new IllegalStateException("No mount service is available."));
	}

	public MountService defaultMountService() {
		return find(settings.selectedMountService());
	}

	public MountService forVault(VaultSettings vaultSettings) {
		var serviceId = vaultSettings.mountService();
		return serviceId == null ? defaultMountService() : find(serviceId);
	}

	private MountService find(String serviceId) {
		if (serviceId == null) {
			return fallback;
		}
		return mountServices.stream().filter(service -> service.getClass().getName().equals(serviceId)).findFirst().orElse(fallback);
	}
}
