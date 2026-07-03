---
area: general
type: decision
status: accepted
date: 2026-07-03
participants: [balazs, Adele]
---

# 027 — Notion Token Storage: Project-Scoped, Local-Only Default + Opt-In Encrypted Vault

The Notion integration token is a per-user bearer secret that Notion displays **once** (lose it →
revoke + regenerate). dydo stores it **project-scoped**, with a `dydo notion connect` command that
handles the plumbing cross-platform. Default storage is **local-only** — a gitignored file protected
by filesystem permissions (`0600`) + OS credential store where one exists — the same honest model
`aws`, `gh`, and `npm` use. An opt-in **encrypted vault** commits the token as authenticated ciphertext
for repo-portability, and is the *only* mode where encryption is meaningful. Guiding principle:
**encryption only helps when the key and the ciphertext live in different trust domains** — a local
key next to local ciphertext is theater. Implements the token half of [Decision 025](./025-notion-sync-architecture.md).

## Context

`dydo notion sync` needs a Notion internal-integration secret. Three forces shape how to store it:

- **It is not re-fetchable.** Notion shows the token once. Losing dydo's copy means revoke + regenerate
  + re-share — disruptive. So dydo holding a recoverable copy (and being able to reveal/migrate it) has
  real value.
- **Multi-project collision.** A user runs many dydo projects concurrently, each on a **separate** Notion
  workspace with its own integration/token. A single global `DYDO_NOTION_TOKEN` collides — confirmed live
  (pointing this project's token at a new workspace clobbered the main project's, and stale process-env
  values had to be `unset`). Config must be per-project.
- **dydo is open-source; its own repo is public.** Committing a secret — even encrypted — into a public
  repo is a permanent, global offline-brute-force target. So committing ciphertext must be opt-in and
  never the default.

## Decision

### 1. Project-scoped config; the storage *mode* is a project-level policy
The token is per-user; **which storage mode** a project uses is set in the project's config
(`dydo.json` / `dydo/_system`), so a team/company can mandate one policy (e.g. "local-only, never commit").
Per-user mode config is intentionally omitted (YAGNI). The parent page lives in `dydo.json`
(`notion.parentPageId`) — not a secret, project-local, already precedent over the env var.

### 2. Resolution order
Token: **local secret store → `DYDO_<projectslug>_NOTION_TOKEN` (namespaced env, for CI) → generic
`DYDO_NOTION_TOKEN` (last-resort fallback)**. Slug derived from `dydo.json`. Parent page:
`dydo.json notion.parentPageId → DYDO_NOTION_PARENT_PAGE`.

### 3. Two storage modes
- **Local-only (default).** A gitignored file at `dydo/_system/.local/`, `0600` (owner-only) on Unix via
  `File.SetUnixFileMode`; **DPAPI** (`System.Security.Cryptography.ProtectedData`, BCL) on Windows — an
  OS-managed key, not a file. macOS Keychain / Linux Secret Service are a future enhancement; the honest
  cross-platform floor is plaintext + `0600`. **No self-rolled encrypt-with-a-local-keyfile** — a key
  co-located with the ciphertext adds complexity and zero security.
- **Vault (opt-in; private repos; portability).** The token is committed as **authenticated ciphertext**,
  key derived from a **typed** passphrase via **Argon2id**. This is the one mode where crypto is real: the
  ciphertext travels in git (repo-leak threat domain), the passphrase does not. A **strong passphrase is
  enforced** (offline brute-force surface); the token is **rotatable** in one command.

### 4. Commands
`dydo notion connect` (guided; paste the token once — it is show-once; pick storage mode per project
policy). `dydo notion reveal-token` (guarded break-glass — genuinely useful given show-once tokens;
mode-dependent: plaintext → copy, DPAPI → OS-decrypt, vault → passphrase; prefer clipboard/masked over
printing to terminal scrollback). `dydo notion sync` (existing).

### 5. Crypto choice
Use **`NSec.Cryptography`** (a managed wrapper over **libsodium**, already a dependency in a sibling
project) for **Argon2id** (memory-hard KDF — defeats offline brute-force on committed ciphertext) + an
AEAD cipher. Never roll our own crypto. The crypto dependency is **vault-only**; local-only needs none.
**Native-AOT is confirmed by a `dotnet publish -r win-x64` smoke as the first build step**; if the native
libsodium does not bundle cleanly, fall back to **BCL PBKDF2 (high-iteration) + AES-256-GCM**.

### 6. One-layer principle
Encrypt only where key and ciphertext are in different trust domains (vault, or OS-managed DPAPI/keychain).
Never nest encryption ("another layer") — it adds failure modes, not security.

## Consequences & open items
- Local-only stays dependency-free and honest; the NSec/Argon2id surface exists only for the opt-in vault.
- Security-sensitive: the vault crypto (nonce handling, KDF params, passphrase strength, no key-in-git)
  goes through the code→review loop with a **dedicated security review**; the AOT-publish smoke gates the
  library choice.
- Deferred: macOS Keychain / Linux Secret Service backends (plaintext+`0600` is the floor until then);
  `reveal-token` clipboard-vs-print UX; reusing the vault passphrase as a general dydo secret store.
- dydo's own (public) repo uses **local-only** — it must never commit ciphertext.

## Status
Accepted. Build order: (1) confirm NSec Native-AOT publish (else PBKDF2 fallback); (2) config resolution
+ project slug + `dydo.json notion` section; (3) local-only store (`0600` / DPAPI) + `connect`/`reveal-token`;
(4) vault (Argon2id + AEAD, strong-passphrase enforcement, rotate); all with a security-focused review.
