---
type: config
---

# Files Off-Limits

This file defines paths that are **globally off-limits** to all AI agents, regardless of role.
These patterns are checked BEFORE role-based permissions and block ALL operations (read, write, delete).

## Syntax

- Patterns are listed in the code block below
- Glob patterns supported: `*` matches within directory, `**` matches across directories
- Lines starting with `#` are comments
- Patterns are case-insensitive on Windows, case-sensitive on Unix

## Default Patterns

```
# ============================================================
# DynaDocs System Files
# ============================================================
# These files are managed by dydo commands. Do not edit directly.
# Use the appropriate dydo command instead (listed below).

# Agent workspace system files
# Edit via: dydo init, dydo agent rename
dydo/agents/*/workflow.md
dydo/agents/*/modes/**

# Agent session state
# Edit via: dydo agent claim, dydo agent release, dydo agent role
dydo/agents/*/state.md
dydo/agents/*/.session

# Agent registry (all agents overview)
# Edit via: dydo agent commands
dydo/agents/agent-states.md

# DynaDocs entry point
# Edit via: dydo init
dydo/index.md

# This security config file
# Edit manually with care - protects sensitive files
dydo/files-off-limits.md

# ============================================================
# Secrets and Credentials
# ============================================================

# Environment and secrets
.env
.env.*
.env.local
.env.development
.env.production
.env.test
secrets.json
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
- Configure in this file, not in role permissions
