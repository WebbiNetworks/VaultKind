# VaultKind Keyboard Controls

This document is the authoritative reference for keyboard behavior in the native Windows interface. Its marked user-facing sections are embedded directly into the Learning Center, while the release checklist remains documentation-only. It records what is implemented today and what must still be verified on an exact release build.

<!-- learning-center-summary: Discover the keyboard controls available throughout VaultKind, including navigation, search, and vault workflows. -->
<!-- learning-center-tip: Press / outside a text field to open Learning Center with focus ready in Search. -->
<!-- learning-center:start -->

## Windows control basics

VaultKind uses native WinUI controls. Standard Windows keyboard behavior remains available:

- `Tab` moves forward through visible, enabled controls.
- `Shift+Tab` moves backward.
- `Enter` or `Space` activates a focused button. `Space` operates standard check boxes, radio buttons, and toggles according to Windows conventions.
- Arrow keys operate native lists, selectors, and other controls where Windows normally provides that behavior.
- Standard text-editing and selection keys remain available in text and password fields.

VaultKind does not trap keyboard focus inside a custom control. When the window opens or becomes active and focus is not already inside the workspace, focus is returned to Dashboard. Existing focus inside the page is preserved.

## Global VaultKind shortcut

| Key | Result |
| --- | --- |
| `/` | Opens Learning Center and places focus in its search field. The shortcut is ignored while focus is in a text box, password box, or rich-text editor so it can be typed normally. |

There are currently no global `Ctrl` shortcuts, Alt-letter mnemonics, or global Escape/Back command. Documentation and testing must not imply that Escape always leaves the current page.

## Main sidebar

The sidebar sequence is Dashboard, Vault Doctor, Add Vault, Vault Manager, every configured vault in displayed order, Activity, Preferences, and Learning Center. Vault Manager is unavailable until at least one vault is configured.

Opening Vault Manager displays every configured vault without selecting one automatically. Focus moves to the first vault card; use `Enter` or `Space` to open that vault's management tools.

Each configured vault in the main sidebar is followed by a **More actions** button. Use `Tab` to reach it, then `Enter` or `Space` to open the same vault menu without requiring a multi-key context-menu gesture.

| Key | Result while a sidebar destination has focus |
| --- | --- |
| `Up Arrow` | Moves focus to the previous visible, enabled destination. Wraps from the first destination to the last. |
| `Down Arrow` | Moves focus to the next visible, enabled destination. Wraps from the last destination to the first. |
| `Home` | Moves focus to Dashboard, the first destination. |
| `End` | Moves focus to Learning Center, the last destination. |
| `Enter` or `Space` | Opens the focused destination or vault. Arrow movement alone does not activate it. |

`Tab` and `Shift+Tab` remain available; arrow navigation is an additional faster route when many vaults are configured.

## Preferences tabs

| Key | Result while a Preferences tab has focus |
| --- | --- |
| `Left Arrow` | Selects and focuses the previous tab, wrapping from General to About. |
| `Right Arrow` | Selects and focuses the next tab, wrapping from About to General. |
| `Home` | Selects and focuses General. |
| `End` | Selects and focuses About. |

The five tabs are General, Appearance, Virtual Drive, Privacy, and About.

## Learning Center

Opening Learning Center places focus in Search.

| Key | Location | Result |
| --- | --- | --- |
| `Down Arrow` | Search | Moves focus to the first topic currently visible after filtering. |
| `Up Arrow` | Search | Moves focus to the last visible topic. |
| `Enter` | Search | Opens the first visible matching topic. |
| `Escape` | Search with text | Clears the search text and remains in Learning Center. |
| `Escape` | Empty Search | Returns to Dashboard. |
| `Down Arrow` | Topic button | Moves focus to the next visible topic; focus stays on the last topic at the end. |
| `Up Arrow` | Topic button | Moves focus to the previous visible topic; from the first topic it returns to Search. |
| `Enter` or `Space` | Topic button or answer | Opens the focused topic or expands the focused answer using normal button behavior. |

Learning Center filtering happens while text is typed; Enter is not required to run the search.

## VaultKind Assistant

Assistant filtering also happens while text is typed. `Enter` in the Assistant search field runs Find a Fix and opens the closest reviewed diagnostic match when one is available.

## Forms and vault workflows

Enter actions only run when the corresponding action is currently valid and enabled.

| Workflow and focus | `Enter` result |
| --- | --- |
| New Vault: Name | Advances to storage selection when the name is valid. |
| New Vault: Password | Moves to Confirm Password. |
| New Vault: Confirm Password | When both valid passwords match, moves to the recovery-option controls. It does not create the vault automatically. |
| Unlock: Password | Activates Unlock. |
| Reset Password: either new-password field | Activates Reset Password when the key, matching password, and acknowledgement requirements are satisfied. |
| Change Password: any password field | Activates Change Password when the complete form is valid and acknowledged. |
| Show Recovery Key: Password | Activates Show Recovery Key when enabled. |
| Rename Vault: Name | Saves the rename. |

`Escape` in the Rename Vault field cancels the inline rename. Escape has no special implementation in the other password and creation fields.

Password-reveal buttons are reachable with Tab and return focus to their associated password field after Show/Hide is activated.

## Deliberate focus placement

VaultKind moves keyboard focus to the first meaningful or safest next control when a workflow changes the visible workspace:

- Dashboard navigation returns focus to Dashboard.
- Learning Center focuses Search.
- New Vault focuses Name, then the first password field when protection begins.
- Password reset focuses the recovery-key field.
- Unlock focuses the vault-password field and returns there after an unlock error.
- Change Password focuses Current Password; leaving the form returns focus to its Vault Manager action.
- Show Recovery Key focuses its password field; leaving returns focus to its Vault Manager action.
- Rename Vault focuses and selects the current name.
- Remove from VaultKind focuses the typed-name confirmation; cancellation returns focus to Remove from VaultKind.
- Locate Encrypted File focuses Choose File; a successful result focuses Copy Encrypted Path.
- Decrypt File Name focuses Identify Recent Entry.
- Clear Activity confirmation initially focuses Cancel; cancellation returns focus to Clear Activity.

Focus placement is delayed briefly after an embedded page change so the newly visible control wins focus instead of the button that opened it. The target is also brought into view for reduced window sizes.

<!-- learning-center:end -->

## Release verification checklist

The automated native checks cover sidebar index movement and wrap boundaries. They do not replace hands-on keyboard testing. Before version 1.0:

1. Test `Tab` and `Shift+Tab` through every primary page without a focus trap or unreachable enabled control.
2. Test sidebar Up/Down/Home/End with zero, one, and many configured vaults.
3. Test Preferences and Learning Center arrow-key boundaries.
4. Test every conditional Enter action in both disabled and enabled states.
5. Confirm focus placement and focus restoration for embedded confirmations and sensitive workflows.
6. Confirm visible focus indicators in Light, Dark, larger-text, minimum-window, and high-DPI configurations.
7. Verify the standard Windows Menu key and `Shift+F10` context-menu gestures on release-candidate hardware. Regardless of platform gesture behavior, confirm every vault's adjacent **More actions** button remains reachable by `Tab` and opens with `Enter` or `Space`.
8. Repeat the core pass with Narrator so accessible names, control roles, focus changes, and live status announcements are heard in the correct order.
