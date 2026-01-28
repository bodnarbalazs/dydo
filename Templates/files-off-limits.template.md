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

## Notes

- The `dydo check` command validates that literal paths (without wildcards) exist
- Add project-specific sensitive files below the default patterns
- These restrictions apply to ALL agents, including code-writers
- Configure in this file, not in role permissions
