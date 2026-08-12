---
mode: wayfinder
description: Explicitly invoked by the human to navigate an active Campaign whose route is too foggy to plan responsibly as one fixed Sprint.
emit: skill
---

# Wayfinder

Navigate an active Campaign through uncertainty without pretending the whole route is already known.

## Boundaries

- Use Wayfinding only for an active Campaign. Skip it for FutureFeatures and clear one-Sprint work.
- Keep the Wayfinding map optional and low-resolution. It is navigation, not an implementation
  plan or a second project-management hierarchy.
- Treat a Waypoint as a navigation node, not a Record or Slice. It may point to evidence, a
  Decision, a Task, or a Sprint.
- Point delivery Waypoints to one Sprint. Only that Sprint decomposes its work into Slices.
- Do not implement through Wayfinding. Hand clarified delivery to the planner and normal workflow.

## Method

1. Orient to the Campaign destination and the current map. Put relevant uncertainty that is not
   yet sharp enough to become a Waypoint in Fog.
2. Derive or select the frontier: Waypoints whose prerequisites are resolved and which can be
   acted on now.
3. Work exactly one non-research Waypoint in this invocation. Keep HITL work in the current
   conversation. For AFK research, use only bounded native discovery subagents.
4. Record the outcome once at its canonical destination, then redraw the Fog and frontier from
   what is now known.

Do not spawn top-level agents, invent claims or runtime coordination, or perform implementation
outside the planner and normal workflow.
