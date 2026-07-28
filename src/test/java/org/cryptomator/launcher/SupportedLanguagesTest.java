package org.cryptomator.launcher;

import org.junit.jupiter.api.Assertions;
import org.junit.jupiter.api.Test;

import java.util.Locale;
import java.util.ResourceBundle;

public class SupportedLanguagesTest {

	@Test
	public void supportsEnglishOnly() {
		Assertions.assertEquals(java.util.List.of("en"), SupportedLanguages.LANGUAGE_TAGS);
		var bundle = Assertions.assertDoesNotThrow(() -> ResourceBundle.getBundle("i18n.strings", Locale.ENGLISH));
		Assertions.assertFalse(bundle.keySet().isEmpty());
	}
}
