package org.cryptomator.nativeui;

import org.cryptomator.common.ShutdownHook;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import javax.inject.Inject;
import java.io.DataInputStream;
import java.io.DataOutputStream;
import java.io.IOException;
import java.io.EOFException;
import java.net.StandardProtocolFamily;
import java.net.UnixDomainSocketAddress;
import java.nio.channels.Channels;
import java.nio.channels.ServerSocketChannel;
import java.nio.file.Files;
import java.nio.file.LinkOption;
import java.nio.file.Path;
import java.nio.file.attribute.AclEntry;
import java.nio.file.attribute.AclEntryFlag;
import java.nio.file.attribute.AclEntryPermission;
import java.nio.file.attribute.AclEntryType;
import java.nio.file.attribute.AclFileAttributeView;
import java.util.EnumSet;
import java.util.List;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.atomic.AtomicBoolean;
import java.util.concurrent.atomic.AtomicReference;

public class NativeUiBridge {

	private static final Logger LOG = LoggerFactory.getLogger(NativeUiBridge.class);
	private static final Path SOCKET_PATH = resolveSocketPath();
	private static final Path BRIDGE_DIRECTORY = SOCKET_PATH.getParent();
	private final NativeUiProtocol protocol;
	private final AtomicReference<ServerSocketChannel> server = new AtomicReference<>();
	private final ExecutorService executor = Executors.newSingleThreadExecutor(r -> {
		var thread = new Thread(r, "NativeUiBridge");
		thread.setDaemon(true);
		return thread;
	});
	private final AtomicBoolean running = new AtomicBoolean();

	@Inject
	public NativeUiBridge(NativeUiProtocol protocol, ShutdownHook shutdownHook) {
		this.protocol = protocol;
		shutdownHook.runOnShutdown(this::close);
	}

	private static Path resolveSocketPath() {
		var configuredPath = System.getenv("VAULTKIND_BRIDGE_PATH");
		if (configuredPath != null && !configuredPath.isBlank()) {
			var path = Path.of(configuredPath).normalize();
			if (!path.isAbsolute()) {
				throw new IllegalStateException("The configured native bridge path must be absolute");
			}
			return path;
		}
		return Path.of(System.getenv("LOCALAPPDATA"), "VaultKind", "bridge", "native-bridge-v1.sock");
	}

	public void start() {
		if (running.compareAndSet(false, true)) {
			executor.execute(this::serve);
		}
	}

	private void serve() {
		try {
			Files.createDirectories(BRIDGE_DIRECTORY);
			restrictBridgeDirectoryToOwner();
			Files.deleteIfExists(SOCKET_PATH);
			try (var channel = ServerSocketChannel.open(StandardProtocolFamily.UNIX)) {
				server.set(channel);
				channel.bind(UnixDomainSocketAddress.of(SOCKET_PATH));
				LOG.info("Native Windows UI bridge listening locally");
				while (running.get()) {
					var client = channel.accept();
					try (client;
						 var in = new DataInputStream(Channels.newInputStream(client));
						 var out = new DataOutputStream(Channels.newOutputStream(client))) {
						while (client.isConnected()) {
							try {
								protocol.handleOne(in, out);
							} catch (EOFException e) {
								break;
							}
						}
					} catch (IOException e) {
						if (running.get()) {
							LOG.debug("Native Windows UI bridge rejected a client request", e);
						}
					}
				}
			}
		} catch (IOException e) {
			if (running.get()) {
				LOG.warn("Native Windows UI bridge stopped", e);
			}
		} finally {
			server.set(null);
			try {
				Files.deleteIfExists(SOCKET_PATH);
			} catch (IOException e) {
				LOG.debug("Unable to remove native UI socket file", e);
			}
		}
	}

	private static void restrictBridgeDirectoryToOwner() throws IOException {
		var aclView = Files.getFileAttributeView(BRIDGE_DIRECTORY, AclFileAttributeView.class, LinkOption.NOFOLLOW_LINKS);
		if (aclView == null) {
			throw new IOException("The native bridge requires Windows ACL support");
		}
		var owner = Files.getOwner(BRIDGE_DIRECTORY, LinkOption.NOFOLLOW_LINKS);
		var ownerOnly = AclEntry.newBuilder() //
				.setType(AclEntryType.ALLOW) //
				.setPrincipal(owner) //
				.setPermissions(EnumSet.allOf(AclEntryPermission.class)) //
				.setFlags(AclEntryFlag.FILE_INHERIT, AclEntryFlag.DIRECTORY_INHERIT) //
				.build();
		aclView.setAcl(List.of(ownerOnly));
	}

	public void close() {
		running.set(false);
		var channel = server.getAndSet(null);
		if (channel != null) {
			try {
				channel.close();
			} catch (IOException e) {
				LOG.debug("Unable to close native UI socket", e);
			}
		}
		executor.shutdownNow();
	}
}
