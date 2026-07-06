---
title: Flaky test: WorktreeCommandTests.InitSettings_CopiesSettingsWithReadPermission File.Exists assertion fails
area: general
fix-release: 
needs-human: false
resolution: 
severity: low
status: open
work-type: 
id: 137
type: issue
found-by: review
date: 2026-04-30
---

# Flaky test: WorktreeCommandTests.InitSettings_CopiesSettingsWithReadPermission File.Exists assertion fails
Open low-severity flake report: `WorktreeCommandTests.InitSettings_CopiesSettingsWithReadPermission` intermittently fails its `File.Exists` assertion, suggesting the settings copy hasn't completed (or was undone) by the time the assertion runs. Awaiting investigation into the timing or cleanup interaction.
## Description
(Describe the issue)
## Reproduction
(Steps to reproduce, if applicable)
## Resolution
(Filled when resolved)