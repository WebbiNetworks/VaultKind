package org.cryptomator.nativeui;

import com.fasterxml.jackson.databind.ObjectMapper;
import org.cryptomator.common.vaults.VaultSummary;

import javax.inject.Inject;
import java.io.DataInputStream;
import java.io.DataOutputStream;
import java.io.IOException;
import java.util.List;

public class NativeUiProtocol {

	public static final int VERSION = 1;
	public static final int MAX_MESSAGE_BYTES = 64 * 1024;
	public static final List<String> CAPABILITIES = List.of("vault.list", "vault.unlock", "vault.lock", "vault.reveal", "vault.remove", "vault.rename", "vault.stats", "vault.locate_encrypted", "vault.decrypt_filename", "vault.create", "vault.connect", "vault.reset_password", "vault.change_password", "vault.show_recovery_key", "settings.mount.list", "settings.mount.select", "backend.shutdown");
	private final ObjectMapper objectMapper;
	private final VaultSummarySource vaultSummarySource;
	private final VaultCommandSource vaultCommandSource;
	private final VaultCreateSource vaultCreateSource;
	private final VaultConnectSource vaultConnectSource;
	private final ShutdownSource shutdownSource;
	private final MountSettingsSource mountSettingsSource;
	private final NativeBackendTerminator terminator;

	@Inject
	public NativeUiProtocol(VaultListSnapshotProvider vaultListSnapshotProvider, NativeVaultOperations vaultOperations, NativeVaultCreator vaultCreator, NativeMountSettings mountSettings, NativeBackendTerminator terminator) {
		this(new ObjectMapper(), vaultListSnapshotProvider::get, vaultOperations::execute, vaultCreator::create, vaultCreator::connect, vaultOperations::lockAll, (operation, serviceId) -> "settings.mount.select".equals(operation) ? mountSettings.select(serviceId) : mountSettings.get(), terminator);
	}

	NativeUiProtocol(ObjectMapper objectMapper, VaultSummarySource vaultSummarySource) {
		this(objectMapper, vaultSummarySource, (operation, vaultId, password, recoveryKey, newPassword, displayName, vaultPath) -> NativeVaultOperations.NativeCommandResult.error("unsupported_operation"), (path, password, recovery, shortNames) -> NativeVaultCreator.NativeCreateResult.error("unsupported_operation"), path -> NativeVaultCreator.NativeCreateResult.error("unsupported_operation"), () -> NativeVaultOperations.NativeCommandResult.error("unsupported_operation"), (operation, serviceId) -> new NativeMountSettings.NativeMountSettingsResult(false, "unsupported_operation", null, List.of()), new NativeBackendTerminator());
	}

	NativeUiProtocol(ObjectMapper objectMapper, VaultSummarySource vaultSummarySource, VaultCommandSource vaultCommandSource) {
		this(objectMapper, vaultSummarySource, vaultCommandSource, (path, password, recovery, shortNames) -> NativeVaultCreator.NativeCreateResult.error("unsupported_operation"), path -> NativeVaultCreator.NativeCreateResult.error("unsupported_operation"), () -> NativeVaultOperations.NativeCommandResult.error("unsupported_operation"), (operation, serviceId) -> new NativeMountSettings.NativeMountSettingsResult(false, "unsupported_operation", null, List.of()), new NativeBackendTerminator());
	}

	NativeUiProtocol(ObjectMapper objectMapper, VaultSummarySource vaultSummarySource, VaultCommandSource vaultCommandSource, VaultCreateSource vaultCreateSource, VaultConnectSource vaultConnectSource, ShutdownSource shutdownSource, MountSettingsSource mountSettingsSource, NativeBackendTerminator terminator) {
		this.objectMapper = objectMapper;
		this.vaultSummarySource = vaultSummarySource;
		this.vaultCommandSource = vaultCommandSource;
		this.vaultCreateSource = vaultCreateSource;
		this.vaultConnectSource = vaultConnectSource;
		this.shutdownSource = shutdownSource;
		this.mountSettingsSource = mountSettingsSource;
		this.terminator = terminator;
	}

	public void handleOne(DataInputStream in, DataOutputStream out) throws IOException {
		var request = read(in, NativeUiRequest.class);
		NativeUiResponse response;
		boolean shutdown = false;
		if (request.protocol() != VERSION) {
			response = NativeUiResponse.error(request.requestId(), "unsupported_protocol");
		} else if ("backend.hello".equals(request.operation())) {
			response = NativeUiResponse.hello(request.requestId());
		} else if ("vault.list".equals(request.operation())) {
			response = NativeUiResponse.vaultList(request.requestId(), vaultSummarySource.get());
		} else if ("vault.unlock".equals(request.operation()) || "vault.lock".equals(request.operation()) || "vault.reveal".equals(request.operation()) || "vault.remove".equals(request.operation()) || "vault.rename".equals(request.operation()) || "vault.stats".equals(request.operation()) || "vault.locate_encrypted".equals(request.operation()) || "vault.decrypt_filename".equals(request.operation()) || "vault.reset_password".equals(request.operation()) || "vault.change_password".equals(request.operation()) || "vault.show_recovery_key".equals(request.operation())) {
			var result = vaultCommandSource.execute(request.operation(), request.vaultId(), request.password(), request.recoveryKey(), request.newPassword(), request.displayName(), request.vaultPath());
			response = result.ok() ? NativeUiResponse.command(request.requestId(), result.state(), result.recoveryKey(), result.statistics(), result.fileNameMapping()) : NativeUiResponse.error(request.requestId(), result.error());
		} else if ("vault.create".equals(request.operation())) {
			var result = vaultCreateSource.create(request.vaultPath(), request.password(), request.createRecoveryKey(), request.useShortNames());
			response = result.ok() ? NativeUiResponse.created(request.requestId(), result.state(), result.vaultId(), result.recoveryKey()) : NativeUiResponse.error(request.requestId(), result.error());
		} else if ("vault.connect".equals(request.operation())) {
			var result = vaultConnectSource.connect(request.vaultPath());
			response = result.ok() ? NativeUiResponse.created(request.requestId(), result.state(), result.vaultId(), null) : NativeUiResponse.error(request.requestId(), result.error());
		} else if ("settings.mount.list".equals(request.operation()) || "settings.mount.select".equals(request.operation())) {
			var result = mountSettingsSource.execute(request.operation(), request.mountService());
			response = result.ok() ? NativeUiResponse.mountSettings(request.requestId(), result.selectedMountService(), result.mountServices()) : NativeUiResponse.error(request.requestId(), result.error());
		} else if ("backend.shutdown".equals(request.operation())) {
			var result = shutdownSource.lockAll();
			response = result.ok() ? NativeUiResponse.command(request.requestId(), result.state()) : NativeUiResponse.error(request.requestId(), result.error());
			shutdown = result.ok();
		} else {
			response = NativeUiResponse.error(request.requestId(), "unknown_operation");
		}
		write(out, response);
		if (shutdown) {
			terminator.requestShutdown();
		}
	}

	private <T> T read(DataInputStream in, Class<T> type) throws IOException {
		int length = in.readInt();
		if (length <= 0 || length > MAX_MESSAGE_BYTES) {
			throw new IOException("Invalid native UI message length");
		}
		return objectMapper.readValue(in.readNBytes(length), type);
	}

	private void write(DataOutputStream out, Object value) throws IOException {
		byte[] payload = objectMapper.writeValueAsBytes(value);
		if (payload.length > MAX_MESSAGE_BYTES) {
			throw new IOException("Native UI response exceeds message limit");
		}
		out.writeInt(payload.length);
		out.write(payload);
		out.flush();
	}

	public record NativeUiRequest(int protocol, String requestId, String operation, String vaultId, char[] password, char[] recoveryKey, char[] newPassword, String displayName, String vaultPath, boolean createRecoveryKey, boolean useShortNames, String mountService) {
		public NativeUiRequest(int protocol, String requestId, String operation) {
			this(protocol, requestId, operation, null, null, null, null, null, null, false, false, null);
		}

		public NativeUiRequest(int protocol, String requestId, String operation, String vaultId, char[] password) {
			this(protocol, requestId, operation, vaultId, password, null, null, null, null, false, false, null);
		}
	}

	public record NativeUiResponse(int protocol, String requestId, boolean ok, String backend, String error, List<VaultSummary> vaults, String state, String vaultId, String recoveryKey, org.cryptomator.common.vaults.VaultStats.NativeSnapshot statistics, NativeVaultOperations.FileNameMapping fileNameMapping, List<String> capabilities, String selectedMountService, List<NativeMountSettings.NativeMountService> mountServices) {

		static NativeUiResponse hello(String requestId) {
			return new NativeUiResponse(VERSION, requestId, true, "VaultKind Java Engine", null, null, null, null, null, null, null, CAPABILITIES, null, null);
		}

		static NativeUiResponse vaultList(String requestId, List<VaultSummary> vaults) {
			return new NativeUiResponse(VERSION, requestId, true, "VaultKind Java Engine", null, List.copyOf(vaults), null, null, null, null, null, null, null, null);
		}

		static NativeUiResponse command(String requestId, String state) {
			return new NativeUiResponse(VERSION, requestId, true, "VaultKind Java Engine", null, null, state, null, null, null, null, null, null, null);
		}

		static NativeUiResponse command(String requestId, String state, String recoveryKey) {
			return command(requestId, state, recoveryKey, null, null);
		}

		static NativeUiResponse command(String requestId, String state, String recoveryKey, org.cryptomator.common.vaults.VaultStats.NativeSnapshot statistics, NativeVaultOperations.FileNameMapping fileNameMapping) {
			return new NativeUiResponse(VERSION, requestId, true, "VaultKind Java Engine", null, null, state, null, recoveryKey, statistics, fileNameMapping, null, null, null);
		}

		static NativeUiResponse created(String requestId, String state, String vaultId, String recoveryKey) {
			return new NativeUiResponse(VERSION, requestId, true, "VaultKind Java Engine", null, null, state, vaultId, recoveryKey, null, null, null, null, null);
		}

		static NativeUiResponse mountSettings(String requestId, String selectedMountService, List<NativeMountSettings.NativeMountService> mountServices) {
			return new NativeUiResponse(VERSION, requestId, true, "VaultKind Java Engine", null, null, null, null, null, null, null, null, selectedMountService, List.copyOf(mountServices));
		}

		static NativeUiResponse error(String requestId, String error) {
			return new NativeUiResponse(VERSION, requestId, false, null, error, null, null, null, null, null, null, null, null, null);
		}
	}

	@FunctionalInterface
	interface VaultSummarySource {
		List<VaultSummary> get() throws IOException;
	}

	@FunctionalInterface
	interface VaultCommandSource {
		NativeVaultOperations.NativeCommandResult execute(String operation, String vaultId, char[] password, char[] recoveryKey, char[] newPassword, String displayName, String vaultPath);
	}

	@FunctionalInterface
	interface VaultCreateSource {
		NativeVaultCreator.NativeCreateResult create(String path, char[] password, boolean createRecoveryKey, boolean useShortNames);
	}

	@FunctionalInterface
	interface VaultConnectSource {
		NativeVaultCreator.NativeCreateResult connect(String path);
	}

	@FunctionalInterface
	interface ShutdownSource {
		NativeVaultOperations.NativeCommandResult lockAll();
	}

	@FunctionalInterface
	interface MountSettingsSource {
		NativeMountSettings.NativeMountSettingsResult execute(String operation, String serviceId);
	}
}
