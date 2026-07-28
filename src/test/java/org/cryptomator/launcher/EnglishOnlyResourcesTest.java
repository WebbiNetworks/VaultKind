package org.cryptomator.launcher;

import org.junit.jupiter.api.Assertions;
import org.junit.jupiter.api.Test;

import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.Set;
import java.util.stream.Collectors;

public class EnglishOnlyResourcesTest {

	@Test
	public void containsOnlyReviewedEnglishResources() throws IOException {
		try (var files = Files.list(Path.of("src", "main", "resources", "i18n"))) {
			Set<String> names = files.map(path -> path.getFileName().toString()).collect(Collectors.toSet());
			Assertions.assertEquals(Set.of("strings.properties", "4096words_en.txt"), names);
		}
	}
}
