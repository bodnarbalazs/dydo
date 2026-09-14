# Reviewing a Project Plan

Target: one committed Project plan proposed for human approval, or an amendment to it, judged in the
shape the project-planner skill fixes, at the plan's candidate commit. PASS means the plan can reach
human approval and open the first work; it certifies a starting route, not a complete one.

## Method

1. **Check the shape.** Frontmatter carries `title`, `status`, `area`, `type` and `linear-project`;
   the six numbered sections stand in order, with First pickable Issues, Later bearings, exact gates
   and any Not yet specified fog; `dydo check` passes. Done when every required element exists or
   is one finding.
2. **Verify the ground.** Read every cited Decision, path, pattern and specification at the
   candidate commit. Done when every material claim matches its source or is one finding.
3. **Test the destination.** Intent, scope, acceptance and governing design agree, and each
   acceptance criterion names the scenario, command, diff or artifact that proves it at the final
   merge. Done when the human knows what approval fixes.
4. **Inspect the starting route.** Each first Issue is self-contained and vertical, carries outcome,
   owned paths, blockers, gate and base branch, and its gate is copy-pasteable; owned paths isolate
   parallel work, hot paths are serial, and the first merge order is credible. Done when every
   listed Issue is safe for an Issue Captain to claim.
5. **Respect the horizon.** Later bearings orient without speculative precision, and every in-scope
   outcome, the durable knowledge and the integration the destination needs among them, is a first
   Issue, a later bearing, honest fog or a blocking Question Issue. Done when the map starts the
   journey without hiding or inventing what comes later.

**Wayfinding fog is not a gap.** A sharp blocker left after authoritative homework and needing human
judgment is a `Question` Issue in `Waiting for Human` with `HITL`, recording its homework and
blocking every Issue that waits on it. Dimmer uncertainty stays in `## Not yet specified`. A plan
that pretends either is settled FAILs; honest placement passes.
