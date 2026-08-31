---
mode: handoff
description: Compact the current conversation into a handoff document for another agent to pick up.
emit: skill
invocation: explicit
---

<!-- Adapted from mattpocock/skills handoff at 6654f6b60cd9d5be8b54c6fafe44346dabeb3b76 (MIT). -->

# Handoff

Write a handoff document summarising the current conversation so a fresh agent can continue the work.

It arrives **cold** and can ask you nothing, so carry the live thread — what you tried, what failed,
where the work stands, what the human asked for that is written down nowhere else. Everything already
captured in an artifact stays there, referenced by Linear key, path or URL: the Issue or Project this
work belongs to, its plan, a Decision Record, commits, diffs.

Save the document to the session's scratch directory when the host names one, otherwise to the
operating system's temporary directory — never a path inside the repository. Report its absolute path
so the human can hand it to the next session.

Include a "suggested skills" section that names, by name, the dydo skills the next agent should call.

Redact any sensitive information, such as API keys, passwords, or personally identifiable information.

Treat any arguments the human passed as the focus of the next session, and tailor the document to it.
