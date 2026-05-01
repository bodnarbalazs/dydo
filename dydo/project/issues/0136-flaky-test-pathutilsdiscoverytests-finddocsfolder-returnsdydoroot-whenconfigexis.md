---
id: 136
area: general
type: issue
severity: low
status: open
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