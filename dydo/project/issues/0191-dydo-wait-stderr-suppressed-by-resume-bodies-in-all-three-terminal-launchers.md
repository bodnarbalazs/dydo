---
id: 191
area: backend
type: issue
severity: medium
status: open
found-by: inquisition
date: 2026-05-19
---

# dydo wait stderr suppressed by resume bodies in all three terminal launchers

When a duplicate-wait refusal or any other ExitCodes.ToolError path fires inside dydo wait, its stderr explanation is dropped because the auto-resume bodies redirect stderr (Linux/Mac: '>/dev/null 2>&1', Windows: '| Out-Null'). The operator sees only the exit code with no diagnostic, which is exactly the 'exit code 2, no useful stderr' symptom in issue #0183.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)