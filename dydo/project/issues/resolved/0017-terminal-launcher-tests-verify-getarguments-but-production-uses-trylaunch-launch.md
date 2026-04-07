---
id: 17
area: backend
type: issue
severity: high
status: resolved
found-by: inquisition
date: 2026-04-07
resolved-date: 2026-04-07
---

# Terminal launcher tests verify GetArguments but production uses TryLaunch/Launch

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Fixed: tests now exercise production paths via instance methods, and shared logic (ApplyOverrides/BuildShellComponents) means both paths test the same core code