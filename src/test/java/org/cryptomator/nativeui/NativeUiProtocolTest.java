package org.cryptomator.nativeui;

import com.fasterxml.jackson.databind.ObjectMapper;
import org.cryptomator.common.vaults.VaultSummary;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;

import java.io.ByteArrayInputStream;
import java.io.ByteArrayOutputStream;
import java.io.DataInputStream;
import java.io.DataOutputStream;
import java.io.IOException;
import java.util.List;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;

class NativeUiProtocolTest {

	private ObjectMapper objectMapper;
	private NativeUiProtocol protocol;

	@BeforeEach
	void setUp() {
		objectMapper = new ObjectMapper();
		protocol = new NativeUiProtocol(objectMapper, () -> List.of(new VaultSummary("vault-1", "Personal", "locked", "F:\\Vaults\\Personal")));
	}

	@Test
	void acceptsVersionedHello() throws IOException {
		var response = exchange(new NativeUiProtocol.NativeUiRequest(1, "request-1", "backend.hello"));

		assertTrue(response.ok());
		assertEquals(1, response.protocol());
		assertEquals("request-1", response.requestId());
		assertEquals("VaultKind Java Engine", response.backend());
	}

	@Test
	void rejectsUnknownProtocolAndOperation() throws IOException {
		var wrongVersion = exchange(new NativeUiProtocol.NativeUiRequest(2, "request-2", "backend.hello"));
		var wrongOperation = exchange(new NativeUiProtocol.NativeUiRequest(1, "request-3", "vault.delete"));

		assertFalse(wrongVersion.ok());
		assertEquals("unsupported_protocol", wrongVersion.error());
		assertFalse(wrongOperation.ok());
		assertEquals("unknown_operation", wrongOperation.error());
	}

	@Test
	void dispatchesNativeUnlockWithoutEchoingPassword() throws IOException {
		var password = "test-password".toCharArray();
		protocol = new NativeUiProtocol(objectMapper, () -> List.of(), (operation, vaultId, suppliedPassword, suppliedRecoveryKey) -> {
			assertEquals("vault-1", vaultId);
			assertEquals("test-password", new String(suppliedPassword));
			return NativeVaultOperations.NativeCommandResult.success("unlocked");
		});

		var response = exchange(new NativeUiProtocol.NativeUiRequest(1, "request-unlock", "vault.unlock", "vault-1", password));

		assertTrue(response.ok());
		assertEquals("unlocked", response.state());
		assertEquals(null, response.vaults());
	}

	@Test
	void dispatchesNativeVaultRemoval() throws IOException {
		protocol = new NativeUiProtocol(objectMapper, () -> List.of(), (operation, vaultId, suppliedPassword, suppliedRecoveryKey) -> {
			assertEquals("vault.remove", operation);
			assertEquals("vault-1", vaultId);
			assertEquals(null, suppliedPassword);
			return NativeVaultOperations.NativeCommandResult.success("removed");
		});

		var response = exchange(new NativeUiProtocol.NativeUiRequest(1, "request-remove", "vault.remove", "vault-1", null));

		assertTrue(response.ok());
		assertEquals("removed", response.state());
	}

	@Test
	void returnsSafeVaultSummaries() throws IOException {
		var response = exchange(new NativeUiProtocol.NativeUiRequest(1, "request-4", "vault.list"));

		assertTrue(response.ok());
		assertEquals(List.of(new VaultSummary("vault-1", "Personal", "locked", "F:\\Vaults\\Personal")), response.vaults());
	}

	@Test
	void rejectsOversizedFramesBeforeParsing() {
		var bytes = new ByteArrayOutputStream();
		try (var out = new DataOutputStream(bytes)) {
			out.writeInt(NativeUiProtocol.MAX_MESSAGE_BYTES + 1);
		} catch (IOException e) {
			throw new AssertionError(e);
		}

		assertThrows(IOException.class, () -> protocol.handleOne(new DataInputStream(new ByteArrayInputStream(bytes.toByteArray())), new DataOutputStream(new ByteArrayOutputStream())));
	}

	private NativeUiProtocol.NativeUiResponse exchange(NativeUiProtocol.NativeUiRequest request) throws IOException {
		byte[] requestBytes = objectMapper.writeValueAsBytes(request);
		var framedRequest = new ByteArrayOutputStream();
		try (var out = new DataOutputStream(framedRequest)) {
			out.writeInt(requestBytes.length);
			out.write(requestBytes);
		}

		var responseBytes = new ByteArrayOutputStream();
		protocol.handleOne(new DataInputStream(new ByteArrayInputStream(framedRequest.toByteArray())), new DataOutputStream(responseBytes));
		try (var in = new DataInputStream(new ByteArrayInputStream(responseBytes.toByteArray()))) {
			return objectMapper.readValue(in.readNBytes(in.readInt()), NativeUiProtocol.NativeUiResponse.class);
		}
	}
}
