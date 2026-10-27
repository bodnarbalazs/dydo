---
area: project
type: folder-meta
---

# Future Features

A FutureFeature is an unscheduled strategic possibility, not scheduled work.
A new FutureFeature is a Linear ticket in status `FutureFeature`, never a file in this folder. Only
the user promotes or cancels it, by the paths the
[Linear Workspace Standard](../../reference/linear-workspace-standard.md) defines.

This folder holds legacy records only: FutureFeatures written here before they moved to Linear. Add
none. Each legacy record stays until its migration to Linear is verified: it becomes one Linear
ticket, durable knowledge it uncovered moves to the dydo document that owns it, and only then is its
file removed.

Each legacy record has `area: project`, `type: concept`, and either `status: idea` or terminal
`status: promoted`. Its body contains a non-empty `## Rationale` section and a `## Related` section
with at least one resolving link to durable repository knowledge.

An `idea` needs no `linear-reference`; a `promoted` record requires exactly one URL matching
`https://linear.app/<workspace>/issue/<TEAM>-<number>[/<slug>]`,
`https://linear.app/<workspace>/project/<slug>-<12-lowercase-hex>`, or
`https://linear.app/<workspace>/initiative/<slug>-<12-lowercase-hex>`. Delivery state lives only in
Linear; a promoted record is provenance and is never synchronized with that work item.

Both states prohibit delivery fields: `assigned`, `assignee`, `priority`, `blocked-by`, `blocks`,
`dependency`, `dependencies`, `project`, `initiative`, `cycle`, `milestone`, `sprint`, `campaign`,
`slice`, `task`, `issue`, `ticket`, `workflow`, `state`, `due-date`, `estimate`, `labels`, `parent`, `sub-issue`, `sub-ticket`,
and `team`. Strict content validation begins only after Project 3 normalizes the retained ideas.
