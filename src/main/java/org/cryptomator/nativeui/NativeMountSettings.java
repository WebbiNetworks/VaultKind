package org.cryptomator.nativeui;

import org.cryptomator.common.settings.EngineSettings;
import org.cryptomator.integrations.mount.MountCapability;
import org.cryptomator.integrations.mount.MountService;

import javax.inject.Inject;
import java.util.ArrayList;
import java.util.List;

public class NativeMountSettings {

	public static final String AUTOMATIC_ID = "automatic";
	private final EngineSettings settings;
	private final List<MountService> mountServices;

	@Inject
	public NativeMountSettings(EngineSettings settings, List<MountService> mountServices) {
		this.settings = settings;
		this.mountServices = mountServices;
	}

	public NativeMountSettingsResult get() {
		var providers = new ArrayList<NativeMountService>();
		providers.add(new NativeMountService(AUTOMATIC_ID, "Automatic (recommended)", true, true, true, true, true));
		providers.addAll(mountServices.stream().map(this::describe).toList());
		var selected = settings.selectedMountService();
		return new NativeMountSettingsResult(true, null, selected == null ? AUTOMATIC_ID : selected, List.copyOf(providers));
	}

	public NativeMountSettingsResult select(String serviceId) {
		if (serviceId == null || AUTOMATIC_ID.equals(serviceId)) {
			settings.selectMountService(null);
			return get();
		}
		if (mountServices.stream().noneMatch(service -> service.getClass().getName().equals(serviceId))) {
			return new NativeMountSettingsResult(false, "unknown_mount_service", null, List.of());
		}
		settings.selectMountService(serviceId);
		return get();
	}

	private NativeMountService describe(MountService service) {
		return new NativeMountService(service.getClass().getName(), service.displayName(),
				service.hasCapability(MountCapability.MOUNT_WITHIN_EXISTING_PARENT) || service.hasCapability(MountCapability.MOUNT_TO_EXISTING_DIR),
				service.hasCapability(MountCapability.MOUNT_AS_DRIVE_LETTER),
				service.hasCapability(MountCapability.LOOPBACK_PORT),
				service.hasCapability(MountCapability.MOUNT_FLAGS),
				service.hasCapability(MountCapability.READ_ONLY));
	}

	public record NativeMountService(String id, String name, boolean mountPoint, boolean driveLetter, boolean loopbackPort, boolean mountFlags, boolean readOnly) {}

	public record NativeMountSettingsResult(boolean ok, String error, String selectedMountService, List<NativeMountService> mountServices) {}
}
