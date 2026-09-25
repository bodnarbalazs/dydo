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

  it('never mutes a related link, even from a closed issue', () => {
    const graph = makeGraph(tree, [related('A', 'X')]);
    expect(buildMapModel(graph, { ...expanded, showRelated: true }).edges).toEqual([
      { id: 'related:A->X', type: 'related', source: 'A', target: 'X', muted: false },
    ]);
  });

  it('re-anchors edges of hidden sub-issues to the outermost collapsed plate and merges duplicates', () => {
    const graph = makeGraph(tree, [blocks('X', 'C'), blocks('X', 'A'), blocks('C', 'A')]);
    const model = buildMapModel(graph, { collapsed: new Set(['P', 'B']), showRelated: false });
    expect(model.edges).toEqual([{ id: 'blocks:X->P', type: 'blocks', source: 'X', target: 'P', muted: false }]);
  });

  it.each([
    ['closed first', [blocks('A', 'X'), blocks('C', 'X')]],
    ['open first', [blocks('C', 'X'), blocks('A', 'X')]],
  ])('keeps a merged edge strong when any of its blockers is open (%s)', (_order, relations) => {
    const model = buildMapModel(makeGraph(tree, relations), { collapsed: new Set(['P']), showRelated: false });
    expect(model.edges).toEqual([{ id: 'blocks:P->X', type: 'blocks', source: 'P', target: 'X', muted: false }]);
  });

  it('mutes the edge from a closed external blocker and draws the blocker as a closed external node', () => {
    const outside = makeIssue('E', { type: 'completed', project: null });
    const model = buildMapModel(makeGraph(tree, [blocks('E', 'X')], [outside]), expanded);
    expect(model.edges).toEqual([{ id: 'blocks:E->X', type: 'blocks', source: 'E', target: 'X', muted: true }]);
    expect(model.nodes.find((node) => node.id === 'E')).toEqual({
      id: 'E',
      kind: 'external',
      issue: outside,
      parentId: null,
      pickable: false,
      closed: true,
      collapsed: false,
      descendants: 0,
    });
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
  it('feeds ELK fixed card sizes, plate padding and the tuned layout options', () => {
    const outside = makeIssue('E', { project: null });
    const { elk } = buildMapModel(makeGraph(tree, [blocks('E', 'X')], [outside]), expanded);
    expect(elk.layoutOptions).toEqual({
      'elk.algorithm': 'rectpacking',
      'elk.aspectRatio': '1.7',
      'elk.spacing.nodeNode': '60',
      'elk.json.edgeCoords': 'ROOT',
      'elk.json.shapeCoords': 'PARENT',
    });
    expect(elk.children?.[0]?.layoutOptions).toEqual({
      'elk.algorithm': 'layered',
      'elk.direction': 'RIGHT',
      'elk.hierarchyHandling': 'INCLUDE_CHILDREN',
      'elk.edgeRouting': 'ORTHOGONAL',
      'elk.layered.mergeEdges': 'true',
      'elk.layered.nodePlacement.strategy': 'NETWORK_SIMPLEX',
      'elk.layered.compaction.postCompaction.strategy': 'EDGE_LENGTH',
      'elk.layered.spacing.nodeNodeBetweenLayers': '56',
      'elk.layered.spacing.edgeNodeBetweenLayers': '14',
      'elk.layered.spacing.edgeEdgeBetweenLayers': '6',
      'elk.spacing.nodeNode': '18',
      'elk.spacing.edgeNode': '16',
      'elk.spacing.edgeEdge': '8',
      'elk.padding': '[top=0,left=0,bottom=0,right=0]',
    });
    const shapes = Object.fromEntries(
      (elk.children ?? []).flatMap((group) => group.children ?? []).map(({ id, width, height, layoutOptions }) => [id, { width, height, layoutOptions }]),
    );
    expect(shapes).toEqual({
      X: { width: 280, height: 96, layoutOptions: undefined },
      E: { width: 230, height: 70, layoutOptions: undefined },
      P: { width: undefined, height: undefined, layoutOptions: { 'elk.padding': '[top=130,left=18,bottom=18,right=18]' } },
    });
  });

  it('groups by top-level issue and keeps each group\'s edges with that group, sub-issue edges included', () => {
    const graph = makeGraph(
      [...tree, makeIssue('Y'), makeIssue('Z'), makeIssue('W')],
      [blocks('X', 'Y'), blocks('Y', 'Z'), blocks('C', 'W')],
    );
    const { elk } = buildMapModel(graph, expanded);
    expect(elk.children?.map((group) => group.children?.map((child) => child.id))).toEqual([['X', 'Y', 'Z'], ['P', 'W']]);
    expect(elk.children?.map((group) => group.edges?.map((edge) => edge.id))).toEqual([['blocks:X->Y', 'blocks:Y->Z'], ['blocks:C->W']]);
  });

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

  it.each([
    ['plate to sub-issue', blocks('P', 'C')],
    ['sub-issue to plate', blocks('C', 'P')],
  ])('leaves an edge between a plate and its own sub-issue out of the layout (%s)', (_direction, relation) => {
    const model = buildMapModel(makeGraph(tree, [relation]), expanded);
    expect(model.edges).toHaveLength(1);
    expect(model.elk.children?.flatMap((group) => group.edges ?? [])).toEqual([]);
  });
});

describe('connectedGroups', () => {
  it('joins linked ids and orders groups biggest first', () => {
    expect(connectedGroups(['a', 'b', 'c', 'd', 'e'], [['d', 'e'], ['c', 'd']])).toEqual([['c', 'd', 'e'], ['a'], ['b']]);
    expect(connectedGroups(['a', 'b', 'c'], [['a', 'b'], ['a', 'c']])).toEqual([['a', 'b', 'c']]);
  });
});
