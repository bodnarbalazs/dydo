import type { Graph, Issue, Relation, StateType } from '../api/types';

const STATE_NAMES: Record<StateType, string> = {
  triage: 'Triage',
  backlog: 'Backlog',
  unstarted: 'Todo',
  started: 'In Progress',
  completed: 'Done',
  canceled: 'Canceled',
  duplicate: 'Duplicate',
};

/** Builds a contract Issue for tests; `id` doubles as identifier suffix. */
export function makeIssue(id: string, overrides: Partial<Issue> & { type?: StateType } = {}): Issue {
  const { type = 'unstarted', ...rest } = overrides;
  return {
    id,
    identifier: `T-${id}`,
    title: `Issue ${id}`,
    url: `https://linear.app/t/issue/T-${id}`,
    state: { name: STATE_NAMES[type], type, color: '#e2e2e2' },
    assignee: null,
    parentId: null,
    team: { id: 'team-1', key: 'T' },
    project: { id: 'project-1', name: 'Project One' },
    ...rest,
  };
}

export function blocks(from: string, to: string): Relation {
  return { id: `blocks-${from}-${to}`, type: 'blocks', from, to };
}

export function related(from: string, to: string): Relation {
  return { id: `related-${from}-${to}`, type: 'related', from, to };
}

export function makeGraph(issues: Issue[], relations: Relation[] = [], external: Issue[] = []): Graph {
  return { project: { id: 'project-1', name: 'Project One', url: 'https://linear.app/t/project/one' }, issues, external, relations };
}
