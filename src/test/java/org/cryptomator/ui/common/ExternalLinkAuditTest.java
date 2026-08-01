package org.cryptomator.ui.common;

import org.junit.jupiter.api.Assertions;
import org.junit.jupiter.api.Test;

import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.List;
import java.util.Locale;
import java.util.Set;

class ExternalLinkAuditTest {

	private static final Set<String> SCANNED_EXTENSIONS = Set.of(".java", ".fxml", ".properties", ".css");
	private static final List<String> BLOCKED_RUNTIME_HOSTS = List.of( //
			"cryptomator.org", //
			"github.com/cryptomator");

	@Test
	void runtimeUiMustNotIntroduceUpstreamLinks() throws IOException {
		var violations = new ArrayList<String>();
		try (var paths = Files.walk(Path.of("src", "main"))) {
			for (Path path : paths.filter(Files::isRegularFile).filter(ExternalLinkAuditTest::isScannedFile).toList()) {
				int lineNumber = 0;
				for (String line : Files.readAllLines(path)) {
					lineNumber++;
					String trimmed = line.stripLeading();
					if (isSourceComment(trimmed)) {
						continue; // provenance and implementation references are not user-facing links
					}
					String normalized = line.toLowerCase(Locale.ROOT);
					if (BLOCKED_RUNTIME_HOSTS.stream().anyMatch(normalized::contains)) {
						violations.add(path + ":" + lineNumber + " " + line.trim());
					}
				}
			}
		}
		Assertions.assertTrue(violations.isEmpty(), "Upstream user-facing links found:\n" + String.join("\n", violations));
	}

	private static boolean isScannedFile(Path path) {
		String filename = path.getFileName().toString();
		return SCANNED_EXTENSIONS.stream().anyMatch(filename::endsWith);
	}

	private static boolean isSourceComment(String line) {
		return line.startsWith("//") || line.startsWith("/*") || line.startsWith("*");
	}
}
