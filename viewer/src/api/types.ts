// The HTTP API contract shared with `dydo map` (plan §3, "HTTP API contract").

export interface Team {
  id: string;
  key: string;
  name: string;
}

export interface Project {
  id: string;
  name: string;
  url: string;
  status: { name: string; type: string };
  /** Linear's timeless date, `yyyy-mm-dd`. */
  targetDate: string | null;
  /** ISO timestamps. */
  completedAt: string | null;
  canceledAt: string | null;
}

export type StateType = 'triage' | 'backlog' | 'unstarted' | 'started' | 'completed' | 'canceled' | 'duplicate';

export interface Issue {
  id: string;
  identifier: string;
  title: string;
  url: string;
  state: { name: string; type: StateType; color: string };
  assignee: string | null;
  parentId: string | null;
  team: { id: string; key: string };
  project: { id: string; name: string } | null;
  /** In Linear's order; empty when the issue has none. `color` is Linear's hex. */
  labels: { name: string; color: string }[];
}

export interface Relation {
  id: string;
  type: 'blocks' | 'related';
  from: string;
  to: string;
}

export interface Graph {
  project: { id: string; name: string; url: string };
  issues: Issue[];
  external: Issue[];
  relations: Relation[];
}
