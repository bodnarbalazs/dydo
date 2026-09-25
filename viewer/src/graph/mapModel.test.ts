import { describe, expect, it } from 'vitest';
import { buildMapModel, connectedGroups, COMPONENT_PREFIX, type MapModel } from './mapModel';
import { blocks, makeGraph, makeIssue, related } from './testIssues';

const expanded = { collapsed: new Set<string>(), showRelated: false };

// parent P holds A and B; B holds C (a plate inside a plate); X stands alone.
const tree = [
  makeIssue('P', { type: 'started' }),
  makeIssue('A', { parentId: 'P', type: 'completed' }),
  makeIssue('B', { parentId: 'P' }),
  makeIssue('C', { parentId: 'B' }),
  makeIssue('X'),
];

function kinds(model: MapModel): Record<string, string> {
  return Object.fromEntries(model.nodes.map((node) => [node.id, `${node.kind}${node.parentId === null ? '' : `<${node.parentId}`}`]));
}

describe('buildMapModel plates', () => {
  it('makes a parent with sub-issues a plate holding them, nested plates included', () => {
    expect(kinds(buildMapModel(makeGraph(tree), expanded))).toEqual({ P: 'plate', A: 'issue<P', B: 'plate<P', C: 'issue<B', X: 'issue' });
  });

  it('lists every parent before its children', () => {
    const order = buildMapModel(makeGraph([...tree].reverse()), expanded).nodes.map((node) => node.id);
    expect(order.indexOf('P')).toBeLessThan(order.indexOf('B'));
    expect(order.indexOf('B')).toBeLessThan(order.indexOf('C'));
  });

  it('draws an issue whose parent is outside the Project at top level', () => {
    const model = buildMapModel(makeGraph([makeIssue('A', { parentId: 'elsewhere' })]), expanded);
    expect(kinds(model)).toEqual({ A: 'issue' });
  });

  it('counts sub-issues at any depth', () => {
    const model = buildMapModel(makeGraph(tree), expanded);
    expect(model.nodes.find((node) => node.id === 'P')?.descendants).toBe(3);
  });

  it('hides the sub-issues of a collapsed plate and keeps its header', () => {
    const model = buildMapModel(makeGraph(tree), { collapsed: new Set(['P']), showRelated: false });
    expect(kinds(model)).toEqual({ P: 'plate', X: 'issue' });
    expect(model.nodes[0]?.collapsed).toBe(true);
  });

  it('ignores a collapse request for an issue without sub-issues', () => {
    const model = buildMapModel(makeGraph(tree), { collapsed: new Set(['X']), showRelated: false });
    expect(model.nodes.find((node) => node.id === 'X')?.collapsed).toBe(false);
  });

  it('carries the pickable and closed flags', () => {
    const model = buildMapModel(makeGraph(tree), expanded);
    const flags = Object.fromEntries(model.nodes.map((node) => [node.id, [node.pickable, node.closed]]));
    expect(flags).toEqual({ P: [false, false], A: [false, true], B: [true, false], C: [true, false], X: [true, false] });
  });
});

describe('buildMapModel edges', () => {
  it('draws blocks from blocker to blocked, muted when the blocker is closed', () => {
    const graph = makeGraph(tree, [blocks('X', 'C'), blocks('A', 'X')]);
    expect(buildMapModel(graph, expanded).edges).toEqual([
      { id: 'blocks:X->C', type: 'blocks', source: 'X', target: 'C', muted: false },
      { id: 'blocks:A->X', type: 'blocks', source: 'A', target: 'X', muted: true },
    ]);
  });

  it('hides related links until they are asked for', () => {
    const graph = makeGraph(tree, [related('X', 'A')]);
    expect(buildMapModel(graph, expanded).edges).toEqual([]);
    expect(buildMapModel(graph, { ...expanded, showRelated: true }).edges).toEqual([
      { id: 'related:X->A', type: 'related', source: 'X', target: 'A', muted: false },
    ]);
  });

  it('re-anchors edges of hidden sub-issues to the outermost collapsed plate and merges duplicates', () => {
    const graph = makeGraph(tree, [blocks('X', 'C'), blocks('X', 'A'), blocks('C', 'A')]);
    const model = buildMapModel(graph, { collapsed: new Set(['P', 'B']), showRelated: false });
    expect(model.edges).toEqual([{ id: 'blocks:X->P', type: 'blocks', source: 'X', target: 'P', muted: false }]);
  });

  it('keeps a merged edge strong when any of its blockers is open', () => {
    const graph = makeGraph(tree, [blocks('A', 'X'), blocks('C', 'X')]);
    const model = buildMapModel(graph, { collapsed: new Set(['P']), showRelated: false });
    expect(model.edges).toEqual([{ id: 'blocks:P->X', type: 'blocks', source: 'P', target: 'X', muted: false }]);
  });

  it('shows an external issue only while one of its edges is visible', () => {
    const outside = makeIssue('E', { project: null, team: { id: 'team-2', key: 'U' } });
    const graph = makeGraph(tree, [related('E', 'X')], [outside]);
    expect(buildMapModel(graph, expanded).nodes.some((node) => node.id === 'E')).toBe(false);
    const shown = buildMapModel(graph, { ...expanded, showRelated: true }).nodes.find((node) => node.id === 'E');
    expect(shown).toMatchObject({ kind: 'external', parentId: null, pickable: false });
  });
});

describe('buildMapModel ELK input', () => {
  it('lays out each connected group layered to the right with plates holding their children', () => {
    const { elk } = buildMapModel(makeGraph(tree, [blocks('X', 'C')]), expanded);
    expect(elk.layoutOptions?.['elk.algorithm']).toBe('rectpacking');
    const [group] = elk.children ?? [];
    expect(group?.id).toBe(`${COMPONENT_PREFIX}0`);
    expect(group?.layoutOptions).toMatchObject({ 'elk.algorithm': 'layered', 'elk.direction': 'RIGHT', 'elk.hierarchyHandling': 'INCLUDE_CHILDREN' });
    expect(group?.children?.map((child) => child.id)).toEqual(['P', 'X']);
    const plate = group?.children?.[0];
    expect(plate?.children?.map((child) => child.id)).toEqual(['A', 'B']);
    expect(plate?.children?.[1]?.children?.map((child) => child.id)).toEqual(['C']);
    expect(group?.edges).toEqual([{ id: 'blocks:X->C', sources: ['X'], targets: ['C'] }]);
  });

  it('puts unconnected issues in groups of their own', () => {
    const { elk } = buildMapModel(makeGraph(tree), expanded);
    expect(elk.children?.map((group) => group.children?.map((child) => child.id))).toEqual([['P'], ['X']]);
  });

  it('gives a collapsed plate a fixed size and no children', () => {
    const { elk } = buildMapModel(makeGraph(tree), { collapsed: new Set(['P']), showRelated: false });
    const plate = elk.children?.[0]?.children?.[0];
    expect(plate).toMatchObject({ id: 'P', width: 280, height: 96 });
    expect(plate?.children).toBeUndefined();
  });

  it('leaves an edge between a plate and its own sub-issue out of the layout', () => {
    const model = buildMapModel(makeGraph(tree, [blocks('P', 'C')]), expanded);
    expect(model.edges).toHaveLength(1);
    expect(model.elk.children?.flatMap((group) => group.edges ?? [])).toEqual([]);
  });
});

describe('connectedGroups', () => {
  it('joins linked ids and orders groups biggest first', () => {
    expect(connectedGroups(['a', 'b', 'c', 'd', 'e'], [['d', 'e'], ['c', 'd']])).toEqual([['c', 'd', 'e'], ['a'], ['b']]);
  });
});
