package org.cryptomator.launcher;

import org.cryptomator.common.settings.Settings;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import javax.inject.Inject;
import javax.inject.Singleton;
import java.util.List;
import java.util.Locale;

@Singleton
public class SupportedLanguages {

	private static final Logger LOG = LoggerFactory.getLogger(SupportedLanguages.class);
	public static final String ENGLISH = "en";
	public static final List<String> LANGUAGE_TAGS = List.of(ENGLISH);

	private final List<String> sortedLanguageTags = LANGUAGE_TAGS;
	private final Locale preferredLocale = Locale.ENGLISH;

	@Inject
	public SupportedLanguages(Settings settings) {
		settings.language.set(ENGLISH);
	}

	public void applyPreferred() {
		LOG.debug("Using locale {}", preferredLocale);
		Locale.setDefault(preferredLocale);
	}

	public List<String> getLanguageTags() {
		return sortedLanguageTags;
	}

}
