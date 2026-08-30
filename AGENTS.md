# DynaDocs

Read [dydo/index.md](dydo/index.md) before working. It is the entry point for this project's
documentation and working conventions, and it names every skill this map uses.

Linear owns live work. Git and dydo own durable knowledge and evidence.

Shared agent methods are authored in dydo and compiled into platform-native skills and agents.
Change their source, not generated output.

## The flow map

Place yourself on the map before you act: one stage, one hat, and the skills that row names.

| Stage | Hat | Skills |
|---|---|---|
| Think | co-thinker | grilling, domain-modeling, research |
| Chart a foggy Project | planner | wayfinder, grilling, research, prototype |
| Plan | planner | codebase-design, then reviewer and the human's approval |
| Implement | implementer | code-writer, test-writer, docs-writer as workers, diagnosing-bugs, then reviewer |
| Coordinate Issues in flight | manager | wayfinder, research, reviewer after every merge |
| Audit | inquisition workflow | inquisitor, reviewer, docs-writer, once the human confirms |
| Land | human | walkthrough, then the feature branch merges into main |
| Harmonize | human on main | walkthrough, teach |

Any hat, at any stage, may reach self-improvement, writing-for-agents, diagnosing-bugs and bro.
The chief-of-staff hat cuts across the map too: it triages the human's attention with grilling, and never delivers.

Branches, worktrees and merges follow [the working-tree contract](dydo/guides/working-tree-contract.md).

Where `AGENTS.md` exists beside `CLAUDE.md`, the two carry this same text; an edit to one belongs in both.
