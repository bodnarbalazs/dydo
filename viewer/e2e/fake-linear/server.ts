import { createServer, type IncomingMessage, type ServerResponse } from 'node:http';
import type { AddressInfo } from 'node:net';
import { createWorkspace, type FakeIssue, type Workspace } from './workspace';

export interface FakeLinear {
  /** The GraphQL endpoint to hand `dydo map` as `DYDO_LINEAR_ENDPOINT`. */
  url: string;
  /** Read on every request, so an edit changes the next answer. */
  workspace: Workspace;
  close(): Promise<void>;
}

interface GraphQLRequest {
  query: string;
  variables: Record<string, string | null>;
}

/** The connection shape of every list `dydo map` reads: one page, nothing after it. */
function connection<T>(nodes: T[]) {
  return { nodes, pageInfo: { hasNextPage: false, endCursor: null } };
}

function find<T extends { id: string }>(items: T[], id: string | null | undefined): T {
  const item = items.find((candidate) => candidate.id === id);
  if (item === undefined) throw new Error(`the fake workspace has no ${String(id)}`);
  return item;
}

function issueFields(workspace: Workspace, issue: FakeIssue) {
  const project = issue.projectId === null ? null : find(workspace.projects, issue.projectId);
  return {
    id: issue.id,
    identifier: issue.identifier,
    title: issue.title,
    url: issue.url,
    archivedAt: null,
    state: issue.state,
    assignee: issue.assignee === null ? null : { name: issue.assignee },
    parent: null,
    team: { id: issue.teamId, key: find(workspace.teams, issue.teamId).key },
    project: project === null ? null : { id: project.id, name: project.name },
  };
}

function projectIssue(workspace: Workspace, issue: FakeIssue) {
  const fields = (id: string) => issueFields(workspace, find(workspace.issues, id));
  return {
    ...issueFields(workspace, issue),
    relations: connection(workspace.relations
      .filter((relation) => relation.from === issue.id)
      .map((relation) => ({ id: relation.id, type: relation.type, relatedIssue: fields(relation.to) }))),
    inverseRelations: connection(workspace.relations
      .filter((relation) => relation.to === issue.id)
      .map((relation) => ({ id: relation.id, type: relation.type, issue: fields(relation.from) }))),
  };
}

/** Linear's `data` for the queries `dydo map` sends, keyed by their operation names. */
function answer(workspace: Workspace, { query, variables }: GraphQLRequest): unknown {
  const operation = /query (\w+)/.exec(query)?.[1];
  switch (operation) {
    case 'Teams':
      return { teams: connection(workspace.teams) };
    case 'TeamProjects':
      return {
        team: {
          projects: connection(workspace.projects
            .filter((project) => project.teamId === variables['teamId'])
            .map(({ id, name, url, status }) => ({ id, name, url, status }))),
        },
      };
    case 'ProjectIssues': {
      const project = find(workspace.projects, variables['projectId']);
      const issues = workspace.issues.filter((issue) => issue.projectId === project.id);
      return {
        project: {
          id: project.id,
          name: project.name,
          url: project.url,
          issues: connection(issues.map((issue) => projectIssue(workspace, issue))),
        },
      };
    }
    default:
      throw new Error(`the fake Linear does not answer ${String(operation)}`);
  }
}

async function readJson(request: IncomingMessage): Promise<GraphQLRequest> {
  let body = '';
  for await (const chunk of request) body += String(chunk);
  return JSON.parse(body) as GraphQLRequest;
}

function reply(response: ServerResponse, status: number, body: unknown) {
  response.writeHead(status, { 'content-type': 'application/json; charset=utf-8' });
  response.end(JSON.stringify(body));
}

/** A Linear GraphQL API on a free loopback port that accepts only `apiKey`, sent bare. */
export async function startFakeLinear(apiKey: string): Promise<FakeLinear> {
  const server = createServer((request, response) => {
    void (async () => {
      if (request.headers.authorization !== apiKey) {
        reply(response, 400, { errors: [{ message: 'Authentication required, not authenticated.', extensions: { type: 'authentication error' } }] });
        return;
      }
      try {
        reply(response, 200, { data: answer(fake.workspace, await readJson(request)) });
      } catch (error) {
        reply(response, 400, { errors: [{ message: String(error) }] });
      }
    })();
  });
  await new Promise<void>((resolve) => server.listen(0, '127.0.0.1', resolve));
  const { port } = server.address() as AddressInfo;
  const fake: FakeLinear = {
    url: `http://127.0.0.1:${String(port)}/graphql`,
    workspace: createWorkspace(),
    close: () => new Promise((resolve, reject) => {
      server.close((error) => (error ? reject(error) : resolve()));
      server.closeAllConnections();
    }),
  };
  return fake;
}
