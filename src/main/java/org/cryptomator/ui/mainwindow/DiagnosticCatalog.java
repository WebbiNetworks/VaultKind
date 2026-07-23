package org.cryptomator.ui.mainwindow;

import java.util.Comparator;
import java.util.List;
import java.util.Optional;

final class DiagnosticCatalog {

	private static final List<DiagnosticCase> CASES = List.of(
			new DiagnosticCase("VK-0001", DiagnosticCase.Category.STARTUP, "diagnostics.VK-0001.title", "diagnostics.VK-0001.cause", "diagnostics.VK-0001.checks", "diagnostics.VK-0001.fix", "settings corrupt", "invalid preferences", "startup settings", "cannot start"),
			new DiagnosticCase("VK-0002", DiagnosticCase.Category.STARTUP, "diagnostics.VK-0002.title", "diagnostics.VK-0002.cause", "diagnostics.VK-0002.checks", "diagnostics.VK-0002.fix", "missing dll", "missing dependency", "runtime missing", "startup dependency"),
			new DiagnosticCase("VK-1001", DiagnosticCase.Category.VAULT, "diagnostics.VK-1001.title", "diagnostics.VK-1001.cause", "diagnostics.VK-1001.checks", "diagnostics.VK-1001.fix", "wrong password", "invalid password", "cannot unlock", "passphrase", "caps lock"),
			new DiagnosticCase("VK-1002", DiagnosticCase.Category.VAULT, "diagnostics.VK-1002.title", "diagnostics.VK-1002.cause", "diagnostics.VK-1002.checks", "diagnostics.VK-1002.fix", "vault missing", "folder moved", "vault not found", "external drive missing", "re-link vault"),
			new DiagnosticCase("VK-1003", DiagnosticCase.Category.VAULT, "diagnostics.VK-1003.title", "diagnostics.VK-1003.cause", "diagnostics.VK-1003.checks", "diagnostics.VK-1003.fix", "config invalid", "vault.cryptomator", "masterkey.cryptomator", "corrupt config", "missing master key"),
			new DiagnosticCase("VK-1004", DiagnosticCase.Category.VAULT, "diagnostics.VK-1004.title", "diagnostics.VK-1004.cause", "diagnostics.VK-1004.checks", "diagnostics.VK-1004.fix", "already connected", "duplicate vault", "connected to vaultkind"),
			new DiagnosticCase("VK-2001", DiagnosticCase.Category.FILESYSTEM, "diagnostics.VK-2001.title", "diagnostics.VK-2001.cause", "diagnostics.VK-2001.checks", "diagnostics.VK-2001.fix", "read only", "permission denied", "access denied", "not writable", "folder permissions"),
			new DiagnosticCase("VK-2002", DiagnosticCase.Category.FILESYSTEM, "diagnostics.VK-2002.title", "diagnostics.VK-2002.cause", "diagnostics.VK-2002.checks", "diagnostics.VK-2002.fix", "disk full", "no space", "storage full", "insufficient space"),
			new DiagnosticCase("VK-2003", DiagnosticCase.Category.FILESYSTEM, "diagnostics.VK-2003.title", "diagnostics.VK-2003.cause", "diagnostics.VK-2003.checks", "diagnostics.VK-2003.fix", "file locked", "cannot lock", "vault busy", "in use", "open files"),
			new DiagnosticCase("VK-2004", DiagnosticCase.Category.FILESYSTEM, "diagnostics.VK-2004.title", "diagnostics.VK-2004.cause", "diagnostics.VK-2004.checks", "diagnostics.VK-2004.fix", "cloud unavailable", "sync conflict", "synchronization problem", "dropbox", "onedrive", "google drive"),
			new DiagnosticCase("VK-2005", DiagnosticCase.Category.FILESYSTEM, "diagnostics.VK-2005.title", "diagnostics.VK-2005.cause", "diagnostics.VK-2005.checks", "diagnostics.VK-2005.fix", "mount failed", "virtual drive missing", "mount backend", "bts9:kt9r:kt9r", "nosuchelementexception", "no value present"),
			new DiagnosticCase("VK-3001", DiagnosticCase.Category.RECOVERY, "diagnostics.VK-3001.title", "diagnostics.VK-3001.cause", "diagnostics.VK-3001.checks", "diagnostics.VK-3001.fix", "forgot password", "recovery key", "reset password", "recover access"),
			new DiagnosticCase("VK-3002", DiagnosticCase.Category.RECOVERY, "diagnostics.VK-3002.title", "diagnostics.VK-3002.cause", "diagnostics.VK-3002.checks", "diagnostics.VK-3002.fix", "health check", "verify integrity", "vault integrity", "damaged vault")
	);

	private DiagnosticCatalog() {
	}

	static Optional<DiagnosticCase.DiagnosticMatch> findBestMatch(String query) {
		return CASES.stream()
				.map(diagnosticCase -> diagnosticCase.match(query))
				.filter(match -> match.score() > 0)
				.max(Comparator.comparingInt(DiagnosticCase.DiagnosticMatch::score));
	}

	static DiagnosticCase byId(String id) {
		return CASES.stream().filter(diagnosticCase -> diagnosticCase.id().equals(id)).findFirst().orElseThrow();
	}

	static List<DiagnosticCase> all() {
		return CASES;
	}

	static List<DiagnosticCase> byCategory(DiagnosticCase.Category category) {
		return CASES.stream().filter(diagnosticCase -> diagnosticCase.category() == category).toList();
	}
}
