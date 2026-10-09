# VaultKind signing authorization

Before an agent uses the Webbi Networks private signing key for any production commit or release tag, obtain explicit owner approval for the concrete change or exact tag target. Prepare the final diff, relevant validation, proposed commit message, and any tag name, target commit, and annotation for review first.

A request to develop code, update documentation, or push changes does not by itself authorize a production signature. Enabled Git signing defaults and a cached passphrase are not approval. Do not bypass this requirement by creating or publishing an unsigned production commit. Keep prepared changes uncommitted while approval is pending. A signing test requires its own explicit authorization and scope.

Preserve existing Git history, existing tags, and the `v1.0.2` release. Do not rewrite history, replace an existing tag, or force-push to add signatures.

Use the existing local private key only from its dedicated signing home, with the full approved fingerprint and secure Pinentry. Never print, log, upload, commit, or transmit private-key material, and never store or pass the passphrase through scripts, command arguments, environment variables, configuration, or logs. Public-key registration and detached-file verification use public material only.
