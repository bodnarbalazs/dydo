---
type: config
---

# Files Off-Limits

This file defines two global path tiers for all AI agents, regardless of role: **off-limits**
paths, which block ALL operations (read, write, delete), and **protected** paths, which every
agent may read but none may write or delete. Both bind on every caller, Claude and Codex alike.

## Syntax

- Patterns are listed in the code blocks below
- Glob patterns supported: `*` matches within directory, `**` matches across directories
- Lines starting with `#` are comments
- Patterns are case-insensitive on every platform

## Default Patterns

```
# ============================================================
# Secrets and Credentials
# ============================================================

# Environment and secrets
# .env* matches .env, .env.local, .env.development, .env.production, .env.test, etc.
.env*
**/secrets.json
**/secret.json
**/secrets.yaml
**/secrets.yml

# Credentials and keys
**/credentials.*
**/credential.*
**/*.pem
**/*.key
**/*.pfx
**/*.p12
**/*.jks
**/*.keystore
**/id_rsa
**/id_rsa.pub
**/id_ed25519
**/id_ed25519.pub
**/id_ecdsa
**/id_dsa
**/.ssh/config
**/.ssh/known_hosts
**/.ssh/authorized_keys

# API keys and tokens
**/api-key*
**/apikey*
**/*.secret
**/token.json
**/tokens.json
**/.token
**/.tokens

# Database credentials
**/database.yml
**/database.yaml
**/db.json
**/db-config.*
**/.pgpass
**/.my.cnf

# Cloud provider configs
**/.aws/credentials
**/.aws/config
**/.azure/**
**/.gcloud/**
**/.config/gcloud/**
**/service-account*.json
**/serviceaccount*.json

# CI/CD secrets
**/*secret*.env
**/secrets/**
**/.secrets/**

# Package manager tokens and configs
**/.npmrc
**/.yarnrc
**/.pypirc
**/pip.conf
**/.nuget/NuGet.Config
**/.gem/credentials
**/.composer/auth.json
**/gradle.properties

# Docker secrets
**/.docker/config.json
**/docker-compose*.secrets.*

# Kubernetes secrets
**/*-secret.yaml
**/*-secret.yml
**/kubeconfig
**/.kube/config

# Terraform state (may contain secrets)
**/*.tfstate
**/*.tfstate.backup
**/.terraform/**

# IDE and editor secrets
**/.idea/dataSources.xml
**/.vscode/settings.json

# Application-specific
**/config/master.key
**/config/credentials.yml.enc
**/.master_key
```

---

## Protected Patterns

Paths listed here are **readable by every agent and writable by none**. `Edit`, `Write`,
`NotebookEdit` and every shell operation that changes them — write, delete, move, copy,
permission change — is blocked; read them with `Read`, `cat` or `head`. These are dydo's
own system files: agents read them to orient themselves and never write them directly, and a
human owns their content. dydo's own commands still rewrite what they manage — `dydo index`
regenerates `dydo/index.md`, and `dydo fix`, `dydo template update` write
`dydo.json` — so the tier stops hand edits, not dydo's tooling. Whitelist entries do not apply
to this section.

```
# DynaDocs entry point - every entry prompt tells agents to read it
dydo/index.md

# The guard's own configuration - this file
dydo/files-off-limits.md

# Project configuration, including the guard's own nudges
dydo.json
```

---

## Whitelist

Paths listed here are **exceptions** to the off-limits patterns above.
Use this for safe template files or test fixtures.

```
# Example: .env.example is a template, not real secrets
.env.example

# Test fixtures that look like secrets but aren't
# tests/fixtures/secrets.json
```

> **Warning:** Be careful with wildcards in whitelist patterns.
> A pattern like `tests/**/*.example` is fine, but `**/secrets.*` would defeat the purpose.

---

## Notes

- The `dydo check` command validates that literal paths (without wildcards) exist
- Add project-specific sensitive files below the default patterns
- These restrictions apply to ALL agents, including code-writers
- Configure in this file; the tiers apply to every caller
