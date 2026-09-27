import { describe, expect, it } from 'vitest';
import type { GraphDiff } from '../graph/graphDiff';
import type { MapNode } from '../graph/mapModel';
import { makeIssue } from '../graph/testIssues';
import type { MapFlow, MapFlowNode } from '../layout/toFlow';
import { exitCascade, exitHold, planCascade, readingOrder, TIMING } from './cascade';

function flowNode(id: string, x: number, y: number, extra: { parentId?: string; title?: string; width?: number } = {}): MapFlowNode {
  const node: MapNode = { id, kind: 'issue', issue: makeIssue(id, extra.title === undefined ? {} : { title: extra.title }), parentId: extra.parentId ?? null, pickable: false, closed: false, collapsed: false, descendants: 0 };
  return { id, type: 'issue', position: { x, y }, width: extra.width ?? 280, height: 96, data: { node }, ...(extra.parentId === undefined ? {} : { parentId: extra.parentId }) };
}

const flow = (...nodes: MapFlowNode[]): MapFlow => ({ nodes, edges: [] });

function diff(parts: Partial<GraphDiff>): GraphDiff {
  return { changed: new Map(), added: new Set(), removed: new Set(), addedBlocks: [], removedBlocks: [], ...parts };
}

describe('refresh timing', () => {
  it('staggers 120 to 180 ms apart, cross-fades and glides in about 400 ms and pulses for about 3 s', () => {
    expect(TIMING.stagger).toBeGreaterThanOrEqual(120);
    expect(TIMING.stagger).toBeLessThanOrEqual(180);
    expect(TIMING).toMatchObject({ crossFade: 400, glide: 400, pulse: 3000 });
    expect(TIMING.exit).toBeLessThan(TIMING.glide);
  });
});

describe('readingOrder', () => {
  it('reads the top row left to right, then the next row', () => {
    const order = readingOrder([
      { id: 'lower-left', x: 0, y: 300 },
      { id: 'top-right', x: 900, y: 10 },
      { id: 'top-left', x: 0, y: 40 },
      { id: 'lower-right', x: 600, y: 260 },
    ]);
    expect(order).toEqual(['top-left', 'top-right', 'lower-left', 'lower-right']);
  });

  it('starts a new row once a top lies more than half a card below the row\'s first', () => {
    expect(readingOrder([{ id: 'b', x: 0, y: 49 }, { id: 'a', x: 100, y: 0 }])).toEqual(['a', 'b']);
    expect(readingOrder([{ id: 'b', x: 0, y: 48 }, { id: 'a', x: 100, y: 0 }])).toEqual(['b', 'a']);
  });
});

describe('exit', () => {
  it('holds the old map while removals fade, unless motion is reduced or nothing leaves', () => {
    expect(exitHold(diff({ removed: new Set(['a']) }), false)).toBe(TIMING.exit);
    expect(exitHold(diff({ removedBlocks: [{ from: 'a', to: 'b' }] }), false)).toBe(TIMING.exit);
    expect(exitHold(diff({ removed: new Set(['a']) }), true)).toBe(0);
    expect(exitHold(diff({ added: new Set(['a']) }), false)).toBe(0);
  });

  it('marks the removed issues and blocking links on the old map', () => {
    const cascade = exitCascade(diff({ removed: new Set(['a']), removedBlocks: [{ from: 'b', to: 'c' }] }));
    expect(cascade).toEqual({ nodes: new Map([['a', { kind: 'removed' }]]), edges: new Map([['blocks:b->c', { kind: 'removed' }]]), glide: false });
  });
});

describe('planCascade', () => {
  const before = flow(flowNode('a', 0, 0), flowNode('b', 400, 0, { title: 'Old b' }), flowNode('c', 0, 200));

  it('marks nothing and glides nowhere when nothing changed', () => {
    expect(planCascade(diff({}), before, before, false)).toEqual({ nodes: new Map(), edges: new Map(), glide: false });
  });

  it('turns changed and added issues one after another in reading order, each changed one keeping its old look', () => {
    const after = flow(flowNode('a', 0, 0), flowNode('b', 400, 0), flowNode('c', 0, 200), flowNode('n', 800, 0));
    const cascade = planCascade(diff({ changed: new Map([['b', ['title']], ['c', ['state']]]), added: new Set(['n']) }), before, after, false);
    expect(cascade.glide).toBe(false);
    expect(cascade.nodes).toEqual(
      new Map([
        ['b', { kind: 'changed', delay: 0, previous: before.nodes[1]?.data.node }],
        ['n', { kind: 'added', delay: TIMING.stagger }],
        ['c', { kind: 'changed', delay: 2 * TIMING.stagger, previous: before.nodes[2]?.data.node }],
      ]),
    );
  });

  it('glides when any node moves or resizes, and starts the cascade once the glide ends', () => {
    const moved = flow(flowNode('a', 0, 0), flowNode('b', 400, 0), flowNode('c', 0, 250));
    const resized = flow(flowNode('a', 0, 0, { width: 300 }), flowNode('b', 400, 0), flowNode('c', 0, 200));
    const changedB = diff({ changed: new Map([['b', ['title']]]) });
    expect(planCascade(changedB, before, moved, false)).toMatchObject({ glide: true, nodes: new Map([['b', { kind: 'changed', delay: TIMING.glide, previous: before.nodes[1]?.data.node }]]) });
    expect(planCascade(changedB, before, resized, false).glide).toBe(true);
  });

  it('places a sub-issue by its plate, so a moved plate moves it and it reads at its place on the canvas', () => {
    const nested = flow(flowNode('p', 0, 300), flowNode('s', 10, 10, { parentId: 'p' }), flowNode('t', 500, 100));
    const platesMoved = flow(flowNode('p', 0, 200), flowNode('s', 10, 10, { parentId: 'p' }), flowNode('t', 500, 100));
    const cascade = planCascade(diff({ added: new Set(['s', 't']) }), nested, platesMoved, false);
    expect(cascade.glide).toBe(true);
    expect([...cascade.nodes.keys()]).toEqual(['t', 's']);
  });

  it('fades each gained blocking link in at its blocked issue\'s turn, or at the start', () => {
    const after = flow(flowNode('a', 0, 0), flowNode('b', 400, 0), flowNode('c', 0, 200), flowNode('n', 0, 400));
    const cascade = planCascade(
      diff({ added: new Set(['n']), changed: new Map([['b', ['pickable']]]), addedBlocks: [{ from: 'a', to: 'n' }, { from: 'c', to: 'a' }] }),
      before,
      after,
      false,
    );
    expect(cascade.edges).toEqual(
      new Map([
        ['blocks:a->n', { kind: 'added', delay: TIMING.stagger }],
        ['blocks:c->a', { kind: 'added', delay: 0 }],
      ]),
    );
  });

  it('makes every change at once, without old looks or a glide, when motion is reduced', () => {
    const after = flow(flowNode('a', 0, 50), flowNode('b', 400, 0), flowNode('c', 0, 200), flowNode('n', 800, 0));
    const cascade = planCascade(diff({ changed: new Map([['b', ['title']], ['c', ['state']]]), added: new Set(['n']) }), before, after, true);
    expect(cascade).toEqual({
      nodes: new Map([
        ['b', { kind: 'changed', delay: 0, previous: null }],
        ['n', { kind: 'added', delay: 0 }],
        ['c', { kind: 'changed', delay: 0, previous: null }],
      ]),
      edges: new Map(),
      glide: false,
    });
  });
});
