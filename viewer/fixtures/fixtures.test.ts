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
 *
 * graph-scenario-refreshed.json is what a Refresh of graph-scenario.json finds (DYD-289): DYD-263,
 * DYD-264 and DYD-265 move on a status, DYD-268 is taken (In Progress, assigned, no longer pickable),
 * DYD-266 is renamed, DYD-262 and DYD-9001 are gone, DYD-290 and DYD-291 (under DYD-265) are new with
 * UUIDv5 ids of `dydo-map-fixture/issue/<identifier>`, DYD-264 blocks DYD-290 which blocks DYD-291, and
 * DYD-270 no longer blocks DYD-266.
 *
 * `labels` (DYD-303) are each issue's real labels, captured 2026-10-02 through the Linear MCP
 * (list_issues with labels, in Linear's order), coloured as list_issue_labels gives the Dydo workspace's
 * labels: AFK #30A46C, HITL #F76B15, Feature #BB87FC, Bug #EB5757, Question #F2C94C, Research #95A2B3,
 * Merge #4EA7FC, Enablement #26B5CE, Inquisition #5E6AD2, Walkthrough #C69C6D, Grilling #D4A017 and the
 * retired Needs human #F5A623. The scenario files hand-build the card cases: DYD-9001 has none, DYD-269
 * three whose last (Needs human) does not fit, DYD-270 three short ones that fit side by side, DYD-271
 * five whose first three by name fit, and DYD-272 the long hand-made label "Blocked upstream on Linear
 * archived-relation semantics" (#D4A017) beside Merge; the refresh gives DYD-266 Needs human.
 *
 * projects-dydo.json holds the team's Projects captured 2026-09-27 through the Linear MCP
 * (list_projects with targetDate, completedAt and canceledAt), plus hand-built Projects whose URLs end
 * in `-fixture` and whose ids are UUIDv5 (URL namespace) of `dydo-map-fixture/project/<name>`. They
 * give the picker every case: each open status type with past, future and undated target dates
 * (relative to the e2e clock, 2026-09-27), more than five completed Projects and a target in another year.
 */
import { readdirSync, readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';
import type { Graph, Issue, Project } from '../src/api/types';
import { describeDiff, diffGraphs } from '../src/graph/graphDiff';
import { pickableIds } from '../src/graph/rules';

const load = <T>(name: string): T => JSON.parse(readFileSync(new URL(name, import.meta.url), 'utf-8')) as T;
const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/;
const STATE_TYPES = ['triage', 'backlog', 'unstarted', 'started', 'completed', 'canceled', 'duplicate'];
const graphs = readdirSync(new URL('.', import.meta.url)).filter((name) => name.startsWith('graph-'));

function expectContractIssue(issue: Issue): void {
  expect(Object.keys(issue).sort()).toEqual(['assignee', 'id', 'identifier', 'labels', 'parentId', 'project', 'state', 'team', 'title', 'url']);
  expect(issue.id).toMatch(UUID);
  expect(STATE_TYPES).toContain(issue.state.type);
  expect(issue.state.color).toMatch(/^#[0-9a-f]{6}$/);
  expect(issue.url).toMatch(/^https:\/\/linear\.app\//);
  issue.labels.forEach((label) => {
    expect(Object.keys(label).sort()).toEqual(['color', 'name']);
    expect(label.color).toMatch(/^#[0-9A-F]{6}$/);
  });
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

  it('refreshes into several changed, added and removed issues and a changed blocking link', () => {
    const refreshed = load<Graph>('graph-scenario-refreshed.json');
    const diff = diffGraphs(scenario, refreshed);
    const identifiers = (graph: Graph, ids: Iterable<string>) => [...ids].map((id) => [...graph.issues, ...graph.external].find((issue) => issue.id === id)?.identifier).sort();
    expect(Object.fromEntries([...diff.changed].map(([id, fields]) => [identifiers(scenario, [id])[0], fields]))).toEqual({
      'DYD-263': ['state'],
      'DYD-264': ['state'],
      'DYD-265': ['state'],
      'DYD-266': ['title', 'labels'],
      'DYD-268': ['state', 'assignee', 'pickable'],
    });
    expect(identifiers(refreshed, diff.added)).toEqual(['DYD-290', 'DYD-291']);
    expect(identifiers(scenario, diff.removed)).toEqual(['DYD-262', 'DYD-9001']);
    expect(describeDiff(diff)).toBe('5 changed · 2 new · 2 removed · 2 links added · 1 link removed');
  });

  it('holds cards with none, one, two, three and five labels, a chip too wide to fit, and one long label', () => {
    const counts = new Set(scenario.issues.map((issue) => issue.labels.length));
    expect([...counts].sort((a, b) => a - b)).toEqual([0, 1, 2, 3, 5]);
    const names = (identifier: string) => byIdentifier(scenario, identifier).labels.map((label) => label.name);
    expect(names('DYD-269')).toEqual(['Merge', 'AFK', 'Needs human']);
    expect(names('DYD-270')).toEqual(['Merge', 'AFK', 'HITL']);
    expect(names('DYD-271')).toEqual(['Merge', 'AFK', 'HITL', 'Bug', 'Walkthrough']);
    expect(byIdentifier(scenario, 'DYD-272').labels.map((label) => label.name.length > 40)).toEqual([false, true]);
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

  it('holds Projects in the contract shape covering every picker rule', () => {
    const projects = load<{ projects: Project[] }>('projects-dydo.json').projects;
    const today = '2026-09-27';
    const ofType = (type: string) => projects.filter((project) => project.status.type === type);
    projects.forEach((project) => {
      expect(Object.keys(project).sort()).toEqual(['canceledAt', 'completedAt', 'id', 'name', 'status', 'targetDate', 'url']);
      expect(project.id).toMatch(UUID);
      expect(project.targetDate ?? '2026-01-01').toMatch(/^\d{4}-\d{2}-\d{2}$/);
      expect(project.completedAt !== null).toBe(project.status.type === 'completed');
      expect(project.canceledAt !== null).toBe(project.status.type === 'canceled');
    });
    for (const type of ['started', 'planned', 'paused', 'backlog']) expect(ofType(type).length).toBeGreaterThan(0);
    const open = projects.filter((project) => !['completed', 'canceled'].includes(project.status.type));
    expect(open.some((project) => project.targetDate !== null && project.targetDate < today)).toBe(true);
    expect(open.some((project) => project.targetDate !== null && project.targetDate > today)).toBe(true);
    expect(open.some((project) => project.targetDate?.startsWith('2027'))).toBe(true);
    expect(open.some((project) => project.targetDate === null)).toBe(true);
    expect(ofType('completed').length).toBeGreaterThan(5);
    expect(ofType('canceled').length).toBeGreaterThanOrEqual(1);
  });

  it('holds an error envelope', () => {
    expect(load<{ error: { code: string } }>('error-linear-auth.json').error.code).toBe('linear_auth');
  });
});
