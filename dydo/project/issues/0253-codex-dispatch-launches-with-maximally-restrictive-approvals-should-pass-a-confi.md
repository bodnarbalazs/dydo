---
title: Codex dispatch launches with maximally-restrictive approvals - should pass a configured auto-approval posture (not yolo)
id: 253
area: backend
type: issue
severity: medium
status: open
found-by: manual
found-by-agent: Adele
found-by-vendor: claude
found-by-model: unknown
date: 2026-07-09
---

# Codex dispatch launches with maximally-restrictive approvals - should pass a configured auto-approval posture (not yolo)

First live codex dispatch smoke (2026-07-09, v2.0.6): the launched codex session required manual human approval for every action including simple reads - balazs had to handhold each permission click, unusable for autonomous dispatch. The launcher emits bare 'codex <prompt>' with no approval/sandbox posture, so codex defaults to its most restrictive interactive mode. Codex CLI supports configured postures (approval policy with a trusted-command classifier + sandbox modes like workspace-write) - the same auto-approve-safe-classify-the-rest model the Codex GUI and Claude Code sessions use. Fix: dispatch should launch codex with a dydo-configured approval+sandbox posture (explicitly NOT the dangerously-bypass-everything flag; balazs: 'not yolo mode, but having a classifier'), verified against the official codex CLI reference like the 0231 resume fix was; posture should be config-surfaced, not hardcoded. Route: sprint C1 (codex adoption). Defense-in-depth note: the dydo guard hook remains the project-boundary enforcement regardless of codex's own sandbox posture.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)