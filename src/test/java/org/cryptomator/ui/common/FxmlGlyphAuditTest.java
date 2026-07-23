package org.cryptomator.ui.common;

import org.cryptomator.ui.controls.FontAwesome5Icon;
import org.junit.jupiter.api.Test;

import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.List;
import java.util.regex.Pattern;

import static org.junit.jupiter.api.Assertions.assertTrue;

public class FxmlGlyphAuditTest {

	private static final Pattern GLYPH_ATTRIBUTE = Pattern.compile("glyph=\\\"([^\\\"]+)\\\"");

	@Test
	public void everyFxmlGlyphMustExistInBundledIconSet() throws IOException {
		Path fxmlDirectory = Path.of("src", "main", "resources", "fxml");
		List<String> invalidGlyphs = new ArrayList<>();
		try (var files = Files.walk(fxmlDirectory)) {
			files.filter(path -> path.toString().endsWith(".fxml")).forEach(path -> inspect(path, invalidGlyphs));
		}
		assertTrue(invalidGlyphs.isEmpty(), () -> "Unsupported FXML glyphs: " + String.join(", ", invalidGlyphs));
	}

	private void inspect(Path path, List<String> invalidGlyphs) {
		try {
			var matcher = GLYPH_ATTRIBUTE.matcher(Files.readString(path));
			while (matcher.find()) {
				String glyph = matcher.group(1);
				if (glyph.startsWith("${")) {
					continue;
				}
				try {
					FontAwesome5Icon.valueOf(glyph);
				} catch (IllegalArgumentException e) {
					invalidGlyphs.add(path.getFileName() + ":" + glyph);
				}
			}
		} catch (IOException e) {
			throw new RuntimeException(e);
		}
	}
}
