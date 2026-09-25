import type { Graph, Issue } from '../api/types';

const CLOSED_TYPES: ReadonlySet<string> = new Set(['completed', 'canceled', 'duplicate']);

export function isClosed(issue: Issue): boolean {
  return CLOSED_TYPES.has(issue.state.type);
}

/** The Project's issues that are `unstarted`, unassigned and have no open blocker (plan §3 "Pickable"). */
export function pickableIds(graph: Graph): Set<string> {
  const byId = new Map([...graph.issues, ...graph.external].map((issue) => [issue.id, issue]));
  const blocked = new Set(
    graph.relations
      .filter((relation) => relation.type === 'blocks')
      .filter((relation) => {
        const blocker = byId.get(relation.from);
        return blocker !== undefined && !isClosed(blocker);
      })
      .map((relation) => relation.to),
  );
  return new Set(
    graph.issues
      .filter((issue) => issue.state.type === 'unstarted' && issue.assignee === null && !blocked.has(issue.id))
      .map((issue) => issue.id),
  );
}
