---
id: 138
area: backend
type: issue
severity: high
status: open
found-by: manual
date: 2026-04-30
---

# Auto-resume launches terminal in user home dir, not project root

Open high-severity bug: when the watchdog auto-resumes a crashed agent (per decision 022), the new terminal launches in the user's home directory instead of the project root. The resumed claude can't find the dydo workspace and the resume effectively fails. Awaiting fix to inherit the project root as the launcher's working directory.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)