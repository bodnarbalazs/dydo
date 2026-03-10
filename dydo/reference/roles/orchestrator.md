---
area: reference
type: reference
---

# Orchestrator

Coordinates parallel agent workstreams. The user's command center during swarm operations.

## Category

Oversight role. Graduation-only — cannot be dispatched.

## Permissions

| Access | Paths |
|--------|-------|
| Write | agent workspace, `project/tasks/**`, `project/decisions/**` |
| Read | source, tests, templates |

No source code writes. The orchestrator directs — it doesn't implement.

## Privileges

- `dispatch --wait` — can dispatch agents and wait for their responses
- Long-lived session — stays active until the user dismisses it

## How to Become Orchestrator

Only agents currently working as **co-thinker** or **planner** can switch to orchestrator. The guard checks `TaskRoleHistory` — if neither role appears, the transition is blocked with a nudge to consult the user.

This prevents freshly dispatched agents from claiming orchestrator. The role requires existing project context.

## Workflow

1. Assess the task with the user
2. Slice work into parallel-safe, disjoint units
3. Dispatch agents with clear, self-contained briefs
4. Monitor: wait for results, track progress, answer user questions
5. Resolve conflicts between workstreams
6. Iterate: user reviews results, orchestrator dispatches the next round
7. Release when the user says so

## Relationships

- Graduates from **co-thinker** or **planner**
- Dispatches to any role
- Reports to the **human** (direct interaction throughout)

## Design Notes

- See decision 005 for why `--wait` is restricted to oversight roles
- See decision 007 for the graduation rationale
