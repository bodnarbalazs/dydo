---
title: Auto-resume opens in new window — should reuse the original window as a new tab where applicable
area: backend
fix-release: 
needs-human: false
resolution: 
severity: low
status: open
work-type: 
id: 144
type: issue
found-by: manual
date: 2026-05-01
---

# Auto-resume opens in new window — should reuse the original window as a new tab where applicable
Open low-severity polish item: the auto-resume launcher always opens a fresh window, even when the original launcher placed the agent in a tab inside an existing window. Where the platform supports it (Windows Terminal tab grouping, etc.), the resume should reuse the original window as a new tab so the visual layout the user set up doesn't fragment after each crash.
## Description
(Describe the issue)
## Reproduction
(Steps to reproduce, if applicable)
## Resolution
(Filled when resolved)