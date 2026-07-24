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
	private final ObjectMapper objectMapper;
	private final VaultSummarySource vaultSummarySource;
	private final VaultCommandSource vaultCommandSource;
	private final VaultCreateSource vaultCreateSource;
	private final ShutdownSource shutdownSource;
	private final NativeBackendTerminator terminator;

	@Inject
	public NativeUiProtocol(VaultListSnapshotProvider vaultListSnapshotProvider, NativeVaultOperations vaultOperations, NativeVaultCreator vaultCreator, NativeBackendTerminator terminator) {
		this(new ObjectMapper(), vaultListSnapshotProvider::get, vaultOperations::execute, vaultCreator::create, vaultOperations::lockAll, terminator);
	}

	NativeUiProtocol(ObjectMapper objectMapper, VaultSummarySource vaultSummarySource) {
		this(objectMapper, vaultSummarySource, (operation, vaultId, password) -> NativeVaultOperations.NativeCommandResult.error("unsupported_operation"), (path, password, recovery, shortNames) -> NativeVaultCreator.NativeCreateResult.error("unsupported_operation"), () -> NativeVaultOperations.NativeCommandResult.error("unsupported_operation"), new NativeBackendTerminator());
	}

	NativeUiProtocol(ObjectMapper objectMapper, VaultSummarySource vaultSummarySource, VaultCommandSource vaultCommandSource) {
		this(objectMapper, vaultSummarySource, vaultCommandSource, (path, password, recovery, shortNames) -> NativeVaultCreator.NativeCreateResult.error("unsupported_operation"), () -> NativeVaultOperations.NativeCommandResult.error("unsupported_operation"), new NativeBackendTerminator());
	}

	NativeUiProtocol(ObjectMapper objectMapper, VaultSummarySource vaultSummarySource, VaultCommandSource vaultCommandSource, VaultCreateSource vaultCreateSource, ShutdownSource shutdownSource, NativeBackendTerminator terminator) {
		this.objectMapper = objectMapper;
		this.vaultSummarySource = vaultSummarySource;
		this.vaultCommandSource = vaultCommandSource;
		this.vaultCreateSource = vaultCreateSource;
		this.shutdownSource = shutdownSource;
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
		} else if ("vault.unlock".equals(request.operation()) || "vault.lock".equals(request.operation()) || "vault.reveal".equals(request.operation())) {
			var result = vaultCommandSource.execute(request.operation(), request.vaultId(), request.password());
			response = result.ok() ? NativeUiResponse.command(request.requestId(), result.state()) : NativeUiResponse.error(request.requestId(), result.error());
		} else if ("vault.create".equals(request.operation())) {
			var result = vaultCreateSource.create(request.vaultPath(), request.password(), request.createRecoveryKey(), request.useShortNames());
			response = result.ok() ? NativeUiResponse.created(request.requestId(), result.state(), result.vaultId(), result.recoveryKey()) : NativeUiResponse.error(request.requestId(), result.error());
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

	public record NativeUiRequest(int protocol, String requestId, String operation, String vaultId, char[] password, String vaultPath, boolean createRecoveryKey, boolean useShortNames) {
		public NativeUiRequest(int protocol, String requestId, String operation) {
			this(protocol, requestId, operation, null, null, null, false, false);
		}

		public NativeUiRequest(int protocol, String requestId, String operation, String vaultId, char[] password) {
			this(protocol, requestId, operation, vaultId, password, null, false, false);
		}
	}

	public record NativeUiResponse(int protocol, String requestId, boolean ok, String backend, String error, List<VaultSummary> vaults, String state, String vaultId, String recoveryKey) {

		static NativeUiResponse hello(String requestId) {
			return new NativeUiResponse(VERSION, requestId, true, "VaultKind Java Engine", null, null, null, null, null);
		}

		static NativeUiResponse vaultList(String requestId, List<VaultSummary> vaults) {
			return new NativeUiResponse(VERSION, requestId, true, "VaultKind Java Engine", null, List.copyOf(vaults), null, null, null);
		}

		static NativeUiResponse command(String requestId, String state) {
			return new NativeUiResponse(VERSION, requestId, true, "VaultKind Java Engine", null, null, state, null, null);
		}

		static NativeUiResponse created(String requestId, String state, String vaultId, String recoveryKey) {
			return new NativeUiResponse(VERSION, requestId, true, "VaultKind Java Engine", null, null, state, vaultId, recoveryKey);
		}

		static NativeUiResponse error(String requestId, String error) {
			return new NativeUiResponse(VERSION, requestId, false, null, error, null, null, null, null);
		}
	}

	@FunctionalInterface
	interface VaultSummarySource {
		List<VaultSummary> get() throws IOException;
	}

	@FunctionalInterface
	interface VaultCommandSource {
		NativeVaultOperations.NativeCommandResult execute(String operation, String vaultId, char[] password);
	}

	@FunctionalInterface
	interface VaultCreateSource {
		NativeVaultCreator.NativeCreateResult create(String path, char[] password, boolean createRecoveryKey, boolean useShortNames);
	}

	@FunctionalInterface
	interface ShutdownSource {
		NativeVaultOperations.NativeCommandResult lockAll();
	}
}
