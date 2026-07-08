---
title: dydo msg/dispatch metacharacter rejection should point at --body-file/--brief-file in the error
id: 243
area: backend
type: issue
severity: low
status: open
found-by: manual
found-by-agent: Adele
found-by-vendor: claude
found-by-model: unknown
date: 2026-07-08
---

# dydo msg/dispatch metacharacter rejection should point at --body-file/--brief-file in the error

dydo msg --body and dispatch --brief reject backticks and shell metacharacters, but the error does not tell the caller that --body-file/--brief-file is the supported path for content with code fences - agents rediscover it every time. Add the suggestion to the rejection message. Routed from auto-memory per DR 038 initial sweep.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)