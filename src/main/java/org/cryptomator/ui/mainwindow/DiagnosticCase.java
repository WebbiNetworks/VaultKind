package org.cryptomator.ui.mainwindow;

import java.util.List;
import java.util.Locale;

record DiagnosticCase(String id, Category category, String titleKey, String causeKey, String checksKey, String fixKey, List<String> keywords) {

	DiagnosticCase(String id, Category category, String titleKey, String causeKey, String checksKey, String fixKey, String... keywords) {
		this(id, category, titleKey, causeKey, checksKey, fixKey, List.of(keywords));
	}

	DiagnosticMatch match(String rawQuery) {
		String query = normalize(rawQuery);
		if (query.isEmpty()) {
			return new DiagnosticMatch(this, 0, List.of());
		}

		List<String> matchedTerms = keywords.stream()
				.filter(keyword -> query.contains(normalize(keyword)) || normalize(keyword).contains(query))
				.toList();
		int score;
		if (query.equals(normalize(id)) || matchedTerms.stream().anyMatch(term -> query.equals(normalize(term)))) {
			score = 100;
		} else if (!matchedTerms.isEmpty()) {
			score = Math.min(95, 58 + matchedTerms.size() * 12);
		} else {
			long matchingTokens = List.of(query.split("\\s+")).stream()
					.filter(token -> token.length() >= 3)
					.filter(token -> keywords.stream().anyMatch(keyword -> normalize(keyword).contains(token)))
					.count();
			score = (int) Math.min(70, matchingTokens * 22);
		}
		return new DiagnosticMatch(this, score, matchedTerms);
	}

	private static String normalize(String value) {
		return value == null ? "" : value.strip().toLowerCase(Locale.ROOT);
	}

	enum Category {
		STARTUP, VAULT, FILESYSTEM, RECOVERY
	}

	record DiagnosticMatch(DiagnosticCase diagnosticCase, int score, List<String> matchedTerms) {
		Confidence confidence() {
			return score >= 80 ? Confidence.STRONG : score >= 35 ? Confidence.POSSIBLE : Confidence.MORE_CHECKS;
		}
	}

	enum Confidence {
		STRONG, POSSIBLE, MORE_CHECKS
	}
}
