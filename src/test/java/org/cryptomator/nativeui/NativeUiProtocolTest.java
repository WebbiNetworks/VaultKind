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
		protocol = new NativeUiProtocol(objectMapper, () -> List.of(new VaultSummary("vault-1", "Personal", "locked", "F:\\Vaults\\Personal", null)));
	}

	@Test
	void acceptsVersionedHello() throws IOException {
		var response = exchange(new NativeUiProtocol.NativeUiRequest(1, "request-1", "backend.hello"));

		assertTrue(response.ok());
		assertEquals(1, response.protocol());
		assertEquals("request-1", response.requestId());
		assertEquals("VaultKind Java Engine", response.backend());
		assertTrue(response.capabilities().contains("vault.show_recovery_key"));
		assertTrue(response.capabilities().contains("vault.reset_password"));
		assertTrue(response.capabilities().contains("vault.rename"));
		assertTrue(response.capabilities().contains("vault.stats"));
		assertTrue(response.capabilities().contains("vault.locate_encrypted"));
		assertTrue(response.capabilities().contains("vault.decrypt_filename"));
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
		protocol = new NativeUiProtocol(objectMapper, () -> List.of(), (operation, vaultId, suppliedPassword, suppliedRecoveryKey, suppliedNewPassword, suppliedDisplayName, suppliedVaultPath) -> {
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
		protocol = new NativeUiProtocol(objectMapper, () -> List.of(), (operation, vaultId, suppliedPassword, suppliedRecoveryKey, suppliedNewPassword, suppliedDisplayName, suppliedVaultPath) -> {
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
	void dispatchesVaultRename() throws IOException {
		protocol = new NativeUiProtocol(objectMapper, () -> List.of(), (operation, vaultId, suppliedPassword, suppliedRecoveryKey, suppliedNewPassword, suppliedDisplayName, suppliedVaultPath) -> {
			assertEquals("vault.rename", operation);
			assertEquals("vault-1", vaultId);
			assertEquals("Family files", suppliedDisplayName);
			return NativeVaultOperations.NativeCommandResult.success("renamed");
		});

		var request = new NativeUiProtocol.NativeUiRequest(1, "request-rename", "vault.rename", "vault-1", null, null, null, "Family files", null, false, false, null);
		var response = exchange(request);

		assertTrue(response.ok());
		assertEquals("renamed", response.state());
	}

	@Test
	void returnsVaultStatisticsForRequestedCommand() throws IOException {
		var statistics = new org.cryptomator.common.vaults.VaultStats.NativeSnapshot(12, 8, 10, 6, 0.75, 120, 80, 100, 60, 9);
		protocol = new NativeUiProtocol(objectMapper, () -> List.of(), (operation, vaultId, suppliedPassword, suppliedRecoveryKey, suppliedNewPassword, suppliedDisplayName, suppliedVaultPath) -> {
			assertEquals("vault.stats", operation);
			assertEquals("vault-1", vaultId);
			return NativeVaultOperations.NativeCommandResult.statistics(statistics);
		});

		var response = exchange(new NativeUiProtocol.NativeUiRequest(1, "request-stats", "vault.stats", "vault-1", null));

		assertTrue(response.ok());
		assertEquals("statistics_ready", response.state());
		assertEquals(statistics, response.statistics());
	}

	@Test
	void returnsDecryptedFileNameForRequestedCommand() throws IOException {
		var mapping = new NativeVaultOperations.FileNameMapping("ABCD.c9r", "Budget.xlsx");
		protocol = new NativeUiProtocol(objectMapper, () -> List.of(), (operation, vaultId, suppliedPassword, suppliedRecoveryKey, suppliedNewPassword, suppliedDisplayName, suppliedVaultPath) -> {
			assertEquals("vault.decrypt_filename", operation);
			assertEquals("vault-1", vaultId);
			assertEquals("F:\\Vaults\\Personal\\d\\ABCD.c9r", suppliedVaultPath);
			return NativeVaultOperations.NativeCommandResult.fileName(mapping);
		});

		var request = new NativeUiProtocol.NativeUiRequest(1, "request-filename", "vault.decrypt_filename", "vault-1", null, null, null, null, "F:\\Vaults\\Personal\\d\\ABCD.c9r", false, false, null);
		var response = exchange(request);

		assertTrue(response.ok());
		assertEquals("filename_ready", response.state());
		assertEquals(mapping, response.fileNameMapping());
	}

	@Test
	void returnsEncryptedLocationForRequestedCommand() throws IOException {
		var mapping = new NativeVaultOperations.FileNameMapping("F:\\Vaults\\Personal\\d\\AB\\ENTRY.c9r", "Budget.xlsx");
		protocol = new NativeUiProtocol(objectMapper, () -> List.of(), (operation, vaultId, suppliedPassword, suppliedRecoveryKey, suppliedNewPassword, suppliedDisplayName, suppliedVaultPath) -> {
			assertEquals("vault.locate_encrypted", operation);
			assertEquals("vault-1", vaultId);
			assertEquals("H:\\Budget.xlsx", suppliedVaultPath);
			return NativeVaultOperations.NativeCommandResult.fileName(mapping);
		});

		var request = new NativeUiProtocol.NativeUiRequest(1, "request-location", "vault.locate_encrypted", "vault-1", null, null, null, null, "H:\\Budget.xlsx", false, false, null);
		var response = exchange(request);

		assertTrue(response.ok());
		assertEquals(mapping, response.fileNameMapping());
	}

	@Test
	void dispatchesPasswordChangeWithoutEchoingSecrets() throws IOException {
		var currentPassword = "current-password".toCharArray();
		var newPassword = "new-password".toCharArray();
		protocol = new NativeUiProtocol(objectMapper, () -> List.of(), (operation, vaultId, suppliedPassword, suppliedRecoveryKey, suppliedNewPassword, suppliedDisplayName, suppliedVaultPath) -> {
			assertEquals("vault.change_password", operation);
			assertEquals("vault-1", vaultId);
			assertEquals("current-password", new String(suppliedPassword));
			assertEquals("new-password", new String(suppliedNewPassword));
			return NativeVaultOperations.NativeCommandResult.success("password_changed");
		});

		var request = new NativeUiProtocol.NativeUiRequest(1, "request-change", "vault.change_password", "vault-1", currentPassword, null, newPassword, null, null, false, false, null);
		var response = exchange(request);

		assertTrue(response.ok());
		assertEquals("password_changed", response.state());
	}

	@Test
	void returnsRecoveryKeyOnlyForRequestedCommand() throws IOException {
		protocol = new NativeUiProtocol(objectMapper, () -> List.of(), (operation, vaultId, suppliedPassword, suppliedRecoveryKey, suppliedNewPassword, suppliedDisplayName, suppliedVaultPath) -> {
			assertEquals("vault.show_recovery_key", operation);
			assertEquals("vault-1", vaultId);
			assertEquals("current-password", new String(suppliedPassword));
			return NativeVaultOperations.NativeCommandResult.recoveryKey("word one two three");
		});

		var response = exchange(new NativeUiProtocol.NativeUiRequest(1, "request-key", "vault.show_recovery_key", "vault-1", "current-password".toCharArray()));

		assertTrue(response.ok());
		assertEquals("recovery_key_ready", response.state());
		assertEquals("word one two three", response.recoveryKey());
	}

	@Test
	void returnsSafeVaultSummaries() throws IOException {
		var response = exchange(new NativeUiProtocol.NativeUiRequest(1, "request-4", "vault.list"));

		assertTrue(response.ok());
		assertEquals(List.of(new VaultSummary("vault-1", "Personal", "locked", "F:\\Vaults\\Personal", null)), response.vaults());
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
