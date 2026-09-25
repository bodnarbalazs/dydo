import { MarkerType } from '@xyflow/react';
import { describe, expect, it } from 'vitest';
import { buildMapModel, COMPONENT_PREFIX } from '../graph/mapModel';
import { blocks, makeGraph, makeIssue, related } from '../graph/testIssues';
import { EDGE_COLORS, toFlow } from './toFlow';

const graph = makeGraph(
  [makeIssue('P'), makeIssue('A', { parentId: 'P' }), makeIssue('X', { type: 'completed' })],
  [blocks('X', 'A'), related('X', 'P')],
);
const model = buildMapModel(graph, { collapsed: new Set(), showRelated: true });
const laidOut = {
  id: 'root',
  children: [
    {
      id: `${COMPONENT_PREFIX}0`,
      x: 100,
      y: 50,
      children: [
        { id: 'P', x: 0, y: 0, width: 300, height: 260, children: [{ id: 'A', x: 10, y: 140, width: 280, height: 96 }] },
        { id: 'X', x: 400, y: 0, width: 280, height: 96 },
      ],
      edges: [{ id: 'blocks:X->A', sources: ['X'], targets: ['A'], sections: [{ id: 's', startPoint: { x: 1, y: 2 }, bendPoints: [{ x: 3, y: 2 }], endPoint: { x: 3, y: 4 } }] }],
    },
  ],
};

describe('toFlow', () => {
  const flow = toFlow(model, laidOut);

  it('shifts top-level nodes by their group offset and keeps children relative to the plate', () => {
    const place = Object.fromEntries(flow.nodes.map((node) => [node.id, [node.position.x, node.position.y, node.parentId ?? null]]));
    expect(place).toEqual({ P: [100, 50, null], A: [10, 140, 'P'], X: [500, 50, null] });
  });

  it('gives each node its ELK size as already measured, not draggable', () => {
    const plate = flow.nodes.find((node) => node.id === 'P');
    expect(plate).toMatchObject({ type: 'plate', width: 300, height: 260, measured: { width: 300, height: 260 }, draggable: false });
  });

  it('hands React Flow a fixed, unconnectable card', () => {
    expect(flow.nodes.find((node) => node.id === 'X')).toEqual({
      id: 'X',
      type: 'issue',
      position: { x: 500, y: 50 },
      width: 280,
      height: 96,
      measured: { width: 280, height: 96 },
      data: { node: model.nodes.find((node) => node.id === 'X') },
      draggable: false,
      connectable: false,
    });
  });

  it('hands React Flow a routed, unfocusable edge above the plates, arrowed from blocker to blocked', () => {
    expect(flow.edges[0]).toEqual({
      id: 'blocks:X->A',
      type: 'routed',
      source: 'X',
      target: 'A',
      data: { edge: model.edges[0], points: [{ x: 1, y: 2 }, { x: 3, y: 2 }, { x: 3, y: 4 }] },
      focusable: false,
      markerEnd: { type: MarkerType.ArrowClosed, width: 16, height: 16, color: '#b4bcc8' },
      zIndex: 1,
    });
  });

  it('turns ELK sections into route points', () => {
    expect(flow.edges.find((edge) => edge.id === 'blocks:X->A')?.data?.points).toEqual([
      { x: 1, y: 2 },
      { x: 3, y: 2 },
      { x: 3, y: 4 },
    ]);
  });

  it('puts a muted arrowhead on a resolved blocks edge and none on related', () => {
    const [blocking, relatedEdge] = flow.edges;
    expect(blocking?.markerEnd).toMatchObject({ color: EDGE_COLORS.muted });
    expect(relatedEdge?.markerEnd).toBeUndefined();
    expect(relatedEdge?.data?.points).toBeNull();
  });

  it.each([
    ['empty sections', { sections: [] }],
    ['no sections', {}],
  ])('leaves an edge ELK returned with %s unrouted', (_case, sections) => {
    const unrouted = { id: 'root', edges: [{ id: 'blocks:X->A', sources: ['X'], targets: ['A'], ...sections }] };
    expect(toFlow(model, unrouted).edges[0]?.data?.points).toBeNull();
  });

  it('places a node ELK left unpositioned at its group offset', () => {
    const unplaced = { id: 'root', children: [{ id: `${COMPONENT_PREFIX}0`, x: 100, y: 50, children: [{ id: 'X', width: 280, height: 96 }] }] };
    expect(toFlow(model, unplaced).nodes.find((node) => node.id === 'X')?.position).toEqual({ x: 100, y: 50 });
  });

  it('places a node ELK did not return at the origin', () => {
    const empty = toFlow(model, { id: 'root' });
    expect(empty.nodes[0]).toMatchObject({ position: { x: 0, y: 0 }, width: 0, height: 0 });
  });

  it('draws an open blocker with the strong arrowhead', () => {
    const open = buildMapModel(makeGraph([makeIssue('a'), makeIssue('b')], [blocks('a', 'b')]), { collapsed: new Set(), showRelated: false });
    expect(toFlow(open, { id: 'root' }).edges[0]?.markerEnd).toMatchObject({ color: '#334155' });
  });
});
