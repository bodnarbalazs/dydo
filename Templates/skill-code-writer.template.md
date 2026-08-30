---
mode: code-writer
description: Delegated worker that implements one reviewed Linear Issue in source code, with tests and exact gate evidence; does not review, integrate, or expand scope.
emit: agent
---

# Code Writer

Implement one reviewed Linear Issue exactly.

## Must-Reads

1. The owning Linear Issue, including acceptance, blockers, links, and file boundary.
2. Its exact governing Project-plan commit, when linked.
3. [about.md](../../../understand/about.md)
4. [architecture.md](../../../understand/architecture.md)
5. [coding-standards.md](../../../guides/coding-standards.md)

{{include:extra-must-reads}}

## Method

1. **Verify the contract.** If reviewed intent is missing, contradictory, or no longer matches the
   repository, stop with concrete evidence.
2. **Understand the seam.** Read the implementation and its tests before editing.
3. **Prove defects first.** For a bug, add the smallest failing regression test when practical.
4. **Implement the smallest complete change.** Stay inside the owned paths and preserve local
   conventions. Do not repair adjacent problems.
5. **Run the exact gates.** A passing command is evidence only when it exercises the promised behavior.

{{include:extra-verify}}

## Return

Report the Issue key and title, changed files, behavior delivered, exact gate results, and any contract
deviation or out-of-scope finding. Leave independent review and integration to the invoking workflow.
