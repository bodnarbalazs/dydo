export interface FakeTeam {
  id: string;
  key: string;
  name: string;
}

export interface FakeProject {
  id: string;
  name: string;
  url: string;
  status: { name: string; type: string };
  teamId: string;
}

export interface FakeIssue {
  id: string;
  identifier: string;
  title: string;
  url: string;
  state: { name: string; type: string; color: string };
  assignee: string | null;
  teamId: string;
  projectId: string | null;
}

/** `from` blocks, or relates to, `to`. */
export interface FakeRelation {
  id: string;
  type: 'blocks' | 'related';
  from: string;
  to: string;
}

/** What the fake Linear answers from; a test edits it between reloads to change Linear's answer. */
export interface Workspace {
  teams: FakeTeam[];
  projects: FakeProject[];
  issues: FakeIssue[];
  relations: FakeRelation[];
}

const todo = { name: 'Todo', type: 'unstarted', color: '#e2e2e2' };
const inProgress = { name: 'In Progress', type: 'started', color: '#f2c94c' };

function issue(identifier: string, title: string, projectId: string, rest: Partial<FakeIssue> = {}): FakeIssue {
  return {
    id: `issue-${identifier}`,
    identifier,
    title,
    url: `https://linear.app/fake/issue/${identifier}`,
    state: todo,
    assignee: null,
    teamId: 'team-dyd',
    projectId,
    ...rest,
  };
}

/**
 * Two teams; Dydo holds two Projects. In "Project map", DYD-1 blocks DYD-3, and DYD-2 of "Release"
 * blocks DYD-1, so the map shows an external blocker in another Project.
 */
export function createWorkspace(): Workspace {
  return {
    teams: [
      { id: 'team-ops', key: 'OPS', name: 'Ops' },
      { id: 'team-dyd', key: 'DYD', name: 'Dydo' },
    ],
    projects: [
      { id: 'project-map', name: 'Project map', url: 'https://linear.app/fake/project/map', status: { name: 'In Progress', type: 'started' }, teamId: 'team-dyd' },
      { id: 'project-release', name: 'Release', url: 'https://linear.app/fake/project/release', status: { name: 'Planned', type: 'planned' }, teamId: 'team-dyd' },
    ],
    issues: [
      issue('DYD-1', 'Draw the map', 'project-map'),
      issue('DYD-2', 'Ship the CLI', 'project-release', { state: inProgress, assignee: 'Ada' }),
      issue('DYD-3', 'Render the graph', 'project-map'),
    ],
    relations: [
      { id: 'relation-1-3', type: 'blocks', from: 'issue-DYD-1', to: 'issue-DYD-3' },
      { id: 'relation-2-1', type: 'blocks', from: 'issue-DYD-2', to: 'issue-DYD-1' },
    ],
  };
}
