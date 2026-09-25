// @vitest-environment node
/*
 * Fixtures in the `dydo map` HTTP API contract shape (plan §3), captured 2026-09-25 through the Linear
 * MCP (list_issues per Project and team, get_issue with relations per Project issue), keeping only
 * contract fields; the assignee is the display name.
 *
 * Fields the MCP does not expose were derived deterministically:
 * - `state.color`: the MCP lists statuses without colours, so each Dydo status name maps to one fixed
 *   colour from Linear's palette (Backlog #bec2c8, FutureFeature #a3a7ae, Todo #e2e2e2,
 *   In Progress #f2c94c, Implementing #f2994a, In Review #26b5ce, Ready to Merge #4cb782,
 *   Done #5e6ad2, Canceled and Duplicate #95a2b3). `state.type` is the MCP's statusType.
 * - Relation `id`: the MCP gives no relation id, so each is a UUIDv5 (URL namespace) of
 *   `dydo-map-fixture/<type>/<from>/<to>`; a related pair is ordered by issue id. `blocks` runs from
 *   the blocker to the blocked issue; duplicate and similar relations are not captured.
 * - `team.key` comes from the identifier prefix; `external` holds the non-archived far ends outside the
 *   Project, looked up in the team's issue list.
 *
 * graph-scenario.json is P-DYD-20 plus hand-built additions that make every AC2-AC4 case certain:
 * DYD-9001 (an assigned Todo), DYD-261 (Done) blocks DYD-267 (a closed blocker), external DYD-217
 * (In Progress, with a Project) blocks DYD-271, and external DYD-198 (Canceled, no Project) blocks
 * DYD-272. DYD-268 is the pickable issue; graph-scenario-assigned.json gives it an assignee and
 * graph-scenario-blocked.json adds its open blocker DYD-263.
 */
import { readdirSync, readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';
import type { Graph, Issue } from '../src/api/types';
import { pickableIds } from '../src/graph/rules';

const load = <T>(name: string): T => JSON.parse(readFileSync(new URL(name, import.meta.url), 'utf-8')) as T;
const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/;
const STATE_TYPES = ['triage', 'backlog', 'unstarted', 'started', 'completed', 'canceled', 'duplicate'];
const graphs = readdirSync(new URL('.', import.meta.url)).filter((name) => name.startsWith('graph-'));

function expectContractIssue(issue: Issue): void {
  expect(Object.keys(issue).sort()).toEqual(['assignee', 'id', 'identifier', 'parentId', 'project', 'state', 'team', 'title', 'url']);
  expect(issue.id).toMatch(UUID);
  expect(STATE_TYPES).toContain(issue.state.type);
  expect(issue.state.color).toMatch(/^#[0-9a-f]{6}$/);
  expect(issue.url).toMatch(/^https:\/\/linear\.app\//);
}

describe.each(graphs)('%s', (name) => {
  const graph = load<Graph>(name);
  const ids = new Set([...graph.issues, ...graph.external].map((issue) => issue.id));

  it('holds contract-shaped issues only', () => {
    expect(Object.keys(graph).sort()).toEqual(['external', 'issues', 'project', 'relations']);
    [...graph.issues, ...graph.external].forEach(expectContractIssue);
    graph.issues.forEach((issue) => expect(issue.project?.id).toBe(graph.project.id));
    graph.external.forEach((issue) => expect(issue.project?.id).not.toBe(graph.project.id));
  });

  it('keeps unique relations whose ends are all present', () => {
    expect(new Set(graph.relations.map((relation) => relation.id)).size).toBe(graph.relations.length);
    graph.relations.forEach((relation) => {
      expect(['blocks', 'related']).toContain(relation.type);
      expect(ids.has(relation.from) && ids.has(relation.to)).toBe(true);
    });
  });
});

describe('large fixture', () => {
  it('has the 150+ issues, plates and blockers acceptance criterion 1 needs', () => {
    const graph = load<Graph>('graph-large.json');
    expect(graph.issues.length).toBeGreaterThanOrEqual(150);
    expect(graph.issues.some((issue) => issue.parentId !== null)).toBe(true);
    expect(graph.relations.some((relation) => relation.type === 'blocks')).toBe(true);
  });
});

describe('scenario fixtures', () => {
  const scenario = load<Graph>('graph-scenario.json');
  const byIdentifier = (graph: Graph, identifier: string) =>
    [...graph.issues, ...graph.external].find((issue) => issue.identifier === identifier) as Issue;
  const pickable = (graph: Graph) => [...pickableIds(graph)].map((id) => graph.issues.find((issue) => issue.id === id)?.identifier);

  it('holds every case acceptance criteria 2 to 4 need', () => {
    expect(pickable(scenario)).toEqual(['DYD-268']);
    expect(byIdentifier(scenario, 'DYD-9001')).toMatchObject({ assignee: 'Balazs', state: { type: 'unstarted' } });
    const blockers = (identifier: string) =>
      scenario.relations.filter((relation) => relation.type === 'blocks' && relation.to === byIdentifier(scenario, identifier).id).map((relation) => relation.from);
    expect(blockers('DYD-267')).toContain(byIdentifier(scenario, 'DYD-261').id);
    expect(byIdentifier(scenario, 'DYD-261').state.type).toBe('completed');
    expect(blockers('DYD-271')).toContain(byIdentifier(scenario, 'DYD-217').id);
    expect(byIdentifier(scenario, 'DYD-217').project).not.toBeNull();
    expect(blockers('DYD-272')).toContain(byIdentifier(scenario, 'DYD-198').id);
    expect(byIdentifier(scenario, 'DYD-198').project).toBeNull();
    expect(scenario.relations.some((relation) => relation.type === 'related')).toBe(true);
  });

  it.each(['graph-scenario-assigned.json', 'graph-scenario-blocked.json'])('%s removes the pickable marker', (name) => {
    expect(pickable(load<Graph>(name))).toEqual([]);
  });
});

describe('selector and error fixtures', () => {
  it('lists teams and Projects sorted by name', () => {
    const names = (items: { name: string }[]) => items.map((item) => item.name);
    const teams = load<{ teams: { name: string }[] }>('teams.json').teams;
    const projects = load<{ projects: { name: string }[] }>('projects-dydo.json').projects;
    expect(names(teams)).toEqual([...names(teams)].sort());
    expect(names(projects)).toEqual([...names(projects)].sort());
  });

  it('holds an error envelope', () => {
    expect(load<{ error: { code: string } }>('error-linear-auth.json').error.code).toBe('linear_auth');
  });
});
