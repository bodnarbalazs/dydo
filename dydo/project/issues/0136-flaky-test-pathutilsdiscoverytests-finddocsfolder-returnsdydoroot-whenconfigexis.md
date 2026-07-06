---
title: Flaky test: PathUtilsDiscoveryTests.FindDocsFolder_ReturnsDydoRoot_WhenConfigExists Dispose DirectoryNotFoundException
area: general
fix-release: 
needs-human: false
resolution: 
severity: low
status: open
work-type: 
id: 136
type: issue
found-by: review
date: 2026-04-30
---

# Flaky test: PathUtilsDiscoveryTests.FindDocsFolder_ReturnsDydoRoot_WhenConfigExists Dispose DirectoryNotFoundException
Open low-severity flake report: `PathUtilsDiscoveryTests.FindDocsFolder_ReturnsDydoRoot_WhenConfigExists` intermittently throws `DirectoryNotFoundException` at `Dispose`, indicating the test's temp directory was already removed by another test or a parallel cleanup. Awaiting investigation into the cleanup ordering.
## Description
(Describe the issue)
## Reproduction
(Steps to reproduce, if applicable)
## Resolution
(Filled when resolved)