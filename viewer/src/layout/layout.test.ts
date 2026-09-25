import ELK from 'elkjs/lib/elk.bundled.js';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { buildMapModel } from '../graph/mapModel';
import { blocks, makeGraph, makeIssue } from '../graph/testIssues';
import { createElk, layoutMap } from './layout';

const view = { collapsed: new Set<string>(), showRelated: false };
const graph = makeGraph(
  [makeIssue('P'), makeIssue('A', { parentId: 'P' }), makeIssue('B', { parentId: 'P' }), makeIssue('X'), makeIssue('Y')],
  [blocks('X', 'A'), blocks('A', 'B'), blocks('P', 'A')],
);

describe('layoutMap with ELK', () => {
  it('places blockers left of the issues they block', async () => {
    const flow = await layoutMap(new ELK(), buildMapModel(graph, view));
    const absolute = (id: string) => {
      const node = flow.nodes.find((candidate) => candidate.id === id);
      const parent = flow.nodes.find((candidate) => candidate.id === node?.parentId);
      return (node?.position.x ?? NaN) + (parent?.position.x ?? 0);
    };
    expect(absolute('X')).toBeLessThan(absolute('A'));
    expect(absolute('A')).toBeLessThan(absolute('B'));
  });

  it('keeps sub-issues inside their plate', async () => {
    const flow = await layoutMap(new ELK(), buildMapModel(graph, view));
    const plate = flow.nodes.find((node) => node.id === 'P');
    for (const child of flow.nodes.filter((node) => node.parentId === 'P')) {
      expect(child.position.x).toBeGreaterThanOrEqual(0);
      expect(child.position.y).toBeGreaterThan(96);
      expect(child.position.x + (child.width ?? 0)).toBeLessThanOrEqual(plate?.width ?? 0);
      expect(child.position.y + (child.height ?? 0)).toBeLessThanOrEqual(plate?.height ?? 0);
    }
  });

  it('routes laid-out edges orthogonally and leaves plate-to-own-child edges unrouted', async () => {
    const flow = await layoutMap(new ELK(), buildMapModel(graph, view));
    const route = flow.edges.find((edge) => edge.id === 'blocks:X->A')?.data?.points;
    expect(route?.length).toBeGreaterThanOrEqual(2);
    route?.slice(1).forEach((point, index) => {
      const previous = route[index];
      expect(point.x === previous?.x || point.y === previous?.y).toBe(true);
    });
    expect(flow.edges.find((edge) => edge.id === 'blocks:P->A')?.data?.points).toBeNull();
  });

  it('packs unconnected groups side by side instead of one column', async () => {
    const many = makeGraph(Array.from({ length: 30 }, (_, index) => makeIssue(`i${String(index)}`)));
    const flow = await layoutMap(new ELK(), buildMapModel(many, view));
    const xs = new Set(flow.nodes.map((node) => node.position.x));
    expect(xs.size).toBeGreaterThan(3);
  });
});

describe('createElk', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('starts ELK in a Web Worker', () => {
    const started: unknown[] = [];
    vi.stubGlobal(
      'Worker',
      class {
        constructor(url: unknown) {
          started.push(url);
        }
        postMessage(): void {}
        terminate(): void {}
      },
    );
    createElk();
    expect(started).toHaveLength(1);
    expect(String(started[0])).toContain('elk-worker');
  });
});
