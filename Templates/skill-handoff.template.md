---
name: handoff
description: Compact the current conversation into a handoff document for another agent to pick up.
emit: skill
invocation: explicit
---

<!-- Adapted from mattpocock/skills handoff at 6654f6b60cd9d5be8b54c6fafe44346dabeb3b76 (MIT). -->

# Handoff

Write a handoff document summarising the current conversation so a fresh agent can continue the
work. Save to the session's scratch directory when the host names one, otherwise to the temporary
directory of the human's OS - not the current workspace - and report its absolute path.

Include a "suggested skills" section in the document, naming which skills the next agent should call
the Skill tool for.

Do not duplicate content already captured in other artifacts (Linear Issues and Projects, plans,
Decision Records, commits, diffs). Reference them by key, path or URL instead.

Redact any sensitive information, such as API keys, passwords, or personally identifiable information.

If the human passed arguments, treat them as a description of what the next session will focus on and
tailor the doc accordingly.
