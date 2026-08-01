# VaultKind Shell Specification

**One shell. One identity.**

This is a product-architecture specification, not a website document or screenshot guide. It is the authoritative public-interface rule for VaultKind. The shell is not decoration around the product. It is the recognizable environment in which VaultKind explains itself, communicates status, presents choices, and earns trust.

Every public-facing VaultKind surface must feel like another legitimate VaultKind window:

- the Windows desktop application;
- VaultKind Web;
- branded documentation and help;
- a future updater or release-status experience;
- diagnostics and Vault Doctor reports;
- a future assistant, including any deliberately approved AI-assisted experience.

The desktop application remains the canonical implementation; all other surfaces derive from it. Other surfaces reuse its language and interaction model without pretending to provide desktop capabilities they do not possess.

## Core principles

These principles outlive any particular screenshot, layout revision, framework, or implementation technology:

1. **The shell is the public identity of VaultKind.** It is the consistent environment through which the product is recognized and understood.
2. **Every VaultKind surface feels like another VaultKind workspace.** Moving between surfaces should feel familiar without disguising what each surface actually is.
3. **The desktop application remains the canonical implementation; all other surfaces derive from it.** When a public-interface question has an established answer in the app, start there.
4. **Navigation teaches the real application structure.** Public navigation uses authentic VaultKind concepts and helps future users understand where they will be in the desktop product.
5. **Never simulate functionality that does not exist.** Familiar presentation must not become fake vaults, activity, diagnostics, preferences, progress, downloads, or actions.
6. **Documentation, VaultKind Web, and future companion tools reuse the shell.** They do not invent independent interface identities or component systems.
7. **Visual consistency is preferred over marketing trends.** VaultKind should remain recognizably itself rather than periodically becoming a generic landing page or fashionable SaaS interface.
8. **Simplicity and clarity take precedence over animation and visual effects.** Motion is allowed only when it improves orientation and never becomes the identity.
9. **Principles are authoritative; screenshots are evidence.** Screenshots may demonstrate a current implementation, but they do not freeze the design or override this specification.

Five years from now, the answer to “How should this website, updater, document viewer, diagnostic tool, assistant, or utility look and behave?” should remain: **it is another VaultKind window.**

## 1. Governing principle

The VaultKind shell is the brand.

A person who explores any VaultKind surface should already understand the product's basic geography before opening another one. Moving between the website, documentation, diagnostics, updater, and desktop application should feel familiar rather than require relearning.

Consistency applies to:

- information architecture;
- navigation placement and selection;
- title and contextual-header structure;
- typography and spacing;
- cards, buttons, status treatments, and focus indicators;
- plain-language terminology;
- motion and reduced-motion behavior;
- privacy, safety, and accessibility expectations.

Consistency does not mean copying unavailable functionality. Each surface must identify itself honestly and expose only real actions.

## 2. Surface identity

Every shell uses the VaultKind name and mark while naming its specific surface.

| Surface | Required identity | Purpose |
| --- | --- | --- |
| Desktop application | **VaultKind — Windows Desktop** | Create, connect, unlock, lock, manage, and diagnose real vaults. |
| Website | **VaultKind Web** | Explain the product, teach its concepts, show release status, and link to verified public resources. |
| Documentation | **VaultKind Documentation** | Provide searchable, linkable, printable reference material. |
| Updater or release status | **VaultKind Update** | Report the authentic installed and available versions and guide a supported update. |
| Diagnostics | **Vault Doctor** | Present local, evidence-based, read-only findings and reports. |
| Future assistant | **VaultKind Assistant** | Explain reviewed VaultKind guidance within an explicit privacy boundary. |

Do not label the website “Windows Desktop.” Do not make a web demonstration appear to be a working vault. The shared shell creates familiarity; the surface label preserves honesty.

## 3. Required shell anatomy

### 3.1 Title bar

- Show the approved VaultKind mark, **VaultKind**, and the specific surface identity.
- Keep it visually quiet. It establishes place; it is not an advertising banner.
- Window controls may be real native controls or a clearly decorative web treatment, but must never imply unsupported browser actions.

### 3.2 Persistent navigation

- Use a stable left navigation rail on desktop-sized layouts.
- Place the most important destinations first.
- Group secondary destinations under short plain-language labels.
- Keep the selected destination unmistakable with surface, border, icon, and accessible state—not color alone.
- Reflow navigation for narrow layouts without changing its terminology or order unnecessarily.

### 3.3 Contextual workspace

- Show one primary workspace page at a time.
- Lead with a title and one concise explanation of that page.
- Keep the user oriented without repeating the whole navigation path.
- Use embedded workflows rather than unnecessary popup windows.
- A website route may update the URL for deep linking, but switching shell destinations must not reload the complete page.

### 3.4 Cards and information surfaces

- Cards group related status, explanation, or actions.
- Standard cards use the native shell's card surface, one-pixel border, 11-pixel corner radius, and 18-pixel default padding before responsive adjustments.
- Information, success, caution, and danger cards must retain their semantic meaning across surfaces.
- Avoid decorative card grids that add no information.

### 3.5 Actions

- Use the same visual hierarchy as the desktop app: one clear primary action, supporting secondary actions, and explicit dangerous actions.
- Button wording describes the result: **View Source**, **Run Vault Doctor**, **Save Report**, or **Release Roadmap**.
- Do not present unavailable actions. A Download button must not appear before a real supported download exists.
- External destinations must be visibly and accessibly distinguishable where confusion is possible.

## 4. Core design tokens

The native shell resources are the source of truth. Public surfaces should consume shared tokens where practical and otherwise reproduce these reviewed values exactly.

### Dark shell palette

| Token | Value | Use |
| --- | --- | --- |
| App canvas | `#292D30` | Primary workspace background |
| Sidebar | `#1D2022` | Persistent navigation |
| Deep surface | `#202426` | Title bars and recessed areas |
| Card | `#3B4145` | Standard cards |
| Selected surface | `#3A4248` | Selected navigation and controls |
| Card border | `#535B60` | Card and control boundaries |
| Divider | `#485056` | Structural separators |
| Brand blue | `#4EA1FF` | Selection, links, and key emphasis |
| Primary action | `#287FE5` | Primary buttons |
| Primary text | `#F4F6F8` | Main text |
| Muted text | `#AEB7BE` | Supporting text |
| Information surface | `#293A4A` | Informational callouts |
| Information text | `#AFC8E2` | Informational copy |
| Success | `#49CD70` | Confirmed healthy or complete state |
| Focus primary | `#F2D45C` | Visible keyboard focus |
| Focus secondary | `#171A1C` | Focus separation on light surfaces |

Light and System themes must derive from the approved desktop palette. A public surface must not invent an unrelated theme merely because it uses a different technology.

### Typography

- Use **Segoe UI Variable** or **Segoe UI** wherever the platform supports it.
- Prefer sentence case and plain English.
- Use weight, size, spacing, and hierarchy before adding decorative color.
- Avoid oversized marketing typography that overwhelms the usable workspace.
- Preserve readable line lengths and allow all text to wrap.

### Geometry and spacing

- Base layout rhythm: 4-pixel increments, with 8, 12, 18, 24, and 42 pixels as common working values.
- Standard navigation item: 46 pixels high, 7-pixel corner radius, 14-pixel horizontal padding.
- Standard card: 11-pixel corner radius, one-pixel border, 18-pixel padding.
- Larger workflow card: up to 12-pixel corner radius and 22–30 pixels padding where content needs separation.
- Responsive density may reduce empty space before reducing readable type.

## 5. Motion

Motion exists to preserve orientation, never to decorate.

- Destination changes may use one short Fluent-style horizontal transition.
- Standard web-shell transition: 220 milliseconds entering, with `cubic-bezier(0.1, 0.9, 0.2, 1)` easing; any outgoing treatment must be shorter and quieter.
- Do not animate ordinary text, status, metrics, or cards merely to attract attention.
- Never delay a safety message or real operation for animation.
- Respect the operating system or browser reduced-motion preference by replacing spatial movement with an immediate state change.
- If the desktop reference changes its navigation motion, related public surfaces must be reviewed for alignment.

## 6. Accessibility baseline

Every shell surface must:

- remain usable with keyboard-only navigation;
- show the approved high-visibility focus treatment;
- expose meaningful names, roles, states, and live updates to assistive technology;
- preserve meaning without relying on color alone;
- reflow without overlapping, clipping, or hiding actions when Windows Text Size is 160% and browser zoom remains 100%;
- support narrow windows and high effective DPI;
- respect reduced motion and system contrast preferences;
- keep minimum pointer targets practical without forcing excessive empty space;
- retain deep links, browser history, and document semantics on web surfaces.

Accessibility fixes take precedence over pixel-level similarity. The goal is one understandable system, not a screenshot-perfect copy that becomes unusable.

## 7. Content and voice

VaultKind communicates with calm confidence.

- Explain consequences plainly without fear-based marketing.
- Lead with what the person needs to know or do.
- Keep encryption terminology accurate, then explain it in familiar words.
- Use the same feature names everywhere: **Dashboard**, **Vault Doctor**, **Vault Manager**, **Preferences**, **Learning Center**, and **Roadmap** where applicable.
- Keep safety, recovery, deletion, and integrity language exact and reviewed.
- Maintain the English-only product boundary defined by the product philosophy.
- Make privacy claims only when they describe verifiable behavior.

## 8. Surface-specific rules

### 8.1 Windows desktop application

- It is the functional reference and owns real vault operations.
- Routine workflows remain inside the main window.
- Native Windows behavior, accessibility, and system integration take priority.

### 8.2 VaultKind Web

- It teaches the product and reports genuine public status.
- It may show real application screenshots, sample reports, Learning Center excerpts, the release roadmap, source links, and supported downloads.
- It must not contain fake vaults, fake activity, fake preferences, fake notifications, simulated diagnostics, or controls that appear to change a real vault.
- Project version and status must be truthful. Static status includes a visible update date; automated status must fail safely rather than invent stale success.
- Website navigation stays inside the shell without full-page reloads and preserves browser history.

### 8.3 Documentation

- Branded documentation renderers use the shell's navigation, typography, cards, and status language.
- Source Markdown remains portable, searchable, deep-linkable, and printable; it does not need decorative window chrome when read in a repository.
- Documentation must not duplicate safety-critical instructions without an identified source of truth.

### 8.4 Future updater

- Show the installed version, available version, source/channel, signature or Store status, and actual progress.
- Never show fake progress or claim an update succeeded before Windows confirms it.
- Keep release notes and rollback or recovery guidance inside the shell where supported.

### 8.5 Diagnostics and reports

- Follow the Vault Doctor language: local, read-only, evidence-based, and plain English.
- Separate healthy, informational, caution, and critical findings clearly.
- Reports must state their scope, time, relevant version, and privacy boundary.
- A report viewer may resemble Vault Doctor but cannot claim a live check occurred when it is displaying a sample.

### 8.6 Future assistant

- Live inside the VaultKind shell rather than becoming a separate chat product.
- Explain VaultKind concepts and reviewed diagnostic evidence; do not impersonate a human security professional.
- State whether processing is local or remote before any sensitive data could leave the device.
- Never read or transmit vault contents, passwords, recovery keys, decrypted names, or diagnostic data without a separately reviewed product decision and a specific informed user action.
- Never silently repair, delete, unlock, share, or upload anything.

## 9. Anti-drift rules

Do:

- reuse native names, tokens, components, spacing, and interaction patterns;
- distinguish the surface while preserving the family identity;
- show real information and real links;
- let one design-system change improve every surface;
- test with the same accessibility expectations as the desktop app.

Do not:

- build a generic marketing site around the VaultKind logo;
- create a second application in the browser;
- add fake operational state to make a page feel active;
- invent a web-only component library when the shell already has an equivalent;
- let documentation, updater, diagnostics, or assistant terminology drift from the app;
- prioritize animation, novelty, or visual density over clarity and trust.

## 10. Change control

The desktop implementation and this specification together define the shell. A material public-interface change must answer:

1. Does it remain recognizably VaultKind?
2. Does it reuse an existing shell pattern where one exists?
3. Is the surface identity honest?
4. Does it expose only real state and real actions?
5. Does it remain usable at the supported text, zoom, DPI, keyboard, screen-reader, contrast, and motion settings?
6. Will the same wording and component remain maintainable across surfaces?

When a new pattern is genuinely required, implement and validate it in the appropriate reference surface, update this specification, and audit the other public surfaces for intentional alignment. Do not allow accidental divergence to become a second design system.

## 11. Release acceptance checklist

Before publishing a new or materially changed VaultKind surface, confirm:

- [ ] The title identifies the correct VaultKind surface.
- [ ] Navigation, contextual header, workspace, cards, and actions follow the shell anatomy.
- [ ] Approved tokens, typography, radii, spacing, focus, and semantic colors are used.
- [ ] Only one primary workspace is shown at a time.
- [ ] Navigation does not unexpectedly discard context or reload the whole web shell.
- [ ] Every displayed version, status, finding, progress value, link, and action is truthful.
- [ ] No fake product functionality was introduced.
- [ ] Keyboard, assistive-technology, text-size, zoom, narrow-layout, contrast, and reduced-motion checks pass.
- [ ] Safety-critical content has one identified source of truth.
- [ ] The result feels familiar to an existing VaultKind user and clear to a first-time visitor.

This specification is intentionally stricter than a visual brand guide. It protects a shared product experience.

**One shell. One identity.**
