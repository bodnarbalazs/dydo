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
