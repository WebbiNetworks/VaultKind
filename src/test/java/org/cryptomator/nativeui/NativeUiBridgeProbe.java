package org.cryptomator.nativeui;

import com.fasterxml.jackson.databind.ObjectMapper;
import org.cryptomator.common.vaults.VaultSummary;

import java.io.DataInputStream;
import java.io.DataOutputStream;
import java.net.StandardProtocolFamily;
import java.net.UnixDomainSocketAddress;
import java.nio.channels.Channels;
import java.nio.channels.ServerSocketChannel;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.List;

/**
 * Manual integration probe for the real Windows named pipe.
 */
public class NativeUiBridgeProbe {

	private static final Path SOCKET_PATH = System.getenv("VAULTKIND_BRIDGE_PATH") == null
			? Path.of(System.getenv("LOCALAPPDATA"), "VaultKind", "bridge", "native-bridge-v1.sock")
			: Path.of(System.getenv("VAULTKIND_BRIDGE_PATH"));

	public static void main(String[] args) throws Exception {
		Files.createDirectories(SOCKET_PATH.getParent());
		Files.deleteIfExists(SOCKET_PATH);
		try (var server = ServerSocketChannel.open(StandardProtocolFamily.UNIX)) {
			server.bind(UnixDomainSocketAddress.of(SOCKET_PATH));
			System.out.println("LIVE_SOCKET_READY");
			try (var client = server.accept();
				 var in = new DataInputStream(Channels.newInputStream(client));
				 var out = new DataOutputStream(Channels.newOutputStream(client))) {
				var protocol = new NativeUiProtocol(new ObjectMapper(), () -> List.of(new VaultSummary("probe-vault", "Interop Probe", "locked", "F:\\Vaults\\Interop Probe", null)));
				protocol.handleOne(in, out);
				protocol.handleOne(in, out);
				System.out.println("LIVE_HANDSHAKE_OK");
			}
		} finally {
			Files.deleteIfExists(SOCKET_PATH);
		}
	}
}
