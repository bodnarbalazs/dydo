---
area: project
type: folder-meta
---

# Future Features

FutureFeatures are repo-native ideas, not scheduled work. They stay in this folder and stay out of
Linear until the human decides that an idea is ready for promotion.

Each FutureFeature has `area: project`, `type: concept`, and either `status: idea` or terminal
`status: promoted`. Its body contains a non-empty `## Rationale` section and a `## Related` section
with at least one resolving link to durable repository knowledge.

Human promotion creates exactly one appropriately shaped Linear Initiative, Project, or Issue, records
its stable URL in `linear-reference`, and changes the status to `promoted`. Delivery state then lives
only in Linear; the FutureFeature remains provenance and is never synchronized with that work item.

An `idea` needs no `linear-reference`; a `promoted` idea requires exactly one URL matching
`https://linear.app/<workspace>/issue/<TEAM>-<number>[/<slug>]`,
`https://linear.app/<workspace>/project/<slug>-<12-lowercase-hex>`, or
`https://linear.app/<workspace>/initiative/<slug>-<12-lowercase-hex>`.

Both states prohibit delivery fields: `assigned`, `assignee`, `priority`, `blocked-by`, `blocks`,
`dependency`, `dependencies`, `project`, `initiative`, `cycle`, `milestone`, `sprint`, `campaign`,
`slice`, `task`, `issue`, `workflow`, `state`, `due-date`, `estimate`, `labels`, `parent`, `sub-issue`,
and `team`. Strict content validation begins only after Project 3 normalizes the retained ideas.
