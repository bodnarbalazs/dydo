import { describe, expect, it } from 'vitest';
import type { Graph } from '../api/types';
import { describeDiff, diffGraphs, type GraphDiff } from './graphDiff';
import { blocks, makeGraph, makeIssue, related } from './testIssues';

const before: Graph = makeGraph(
  [makeIssue('A'), makeIssue('B', { assignee: 'Ada' }), makeIssue('C'), makeIssue('D', { parentId: 'C' }), makeIssue('Gone')],
  [blocks('B', 'C'), related('A', 'B')],
  [makeIssue('X', { project: null })],
);

describe('diffGraphs', () => {
  it('finds nothing between a graph and a fresh copy of it', () => {
    const diff = diffGraphs(before, structuredClone(before));
    expect(diff).toEqual({ changed: new Map(), added: new Set(), removed: new Set(), addedBlocks: [], removedBlocks: [] });
    expect(describeDiff(diff)).toBe('No changes');
  });

  it('names each changed field of an issue on both graphs', () => {
    const after = makeGraph(
      [
        makeIssue('A', { type: 'started' }),
        makeIssue('B', { assignee: 'Bo' }),
        makeIssue('C', { title: 'Renamed' }),
        makeIssue('D', { parentId: null }),
        makeIssue('Gone'),
      ],
      before.relations,
      [makeIssue('X', { project: null, state: { name: 'Todo', type: 'unstarted', color: '#000000' } })],
    );
    expect(diffGraphs(before, after).changed).toEqual(
      new Map([
        ['A', ['state', 'pickable']],
        ['B', ['assignee']],
        ['C', ['title']],
        ['D', ['parent']],
        ['X', ['state']],
      ]),
    );
  });

  it('counts a changed label set as a labels change, by name and colour, in any order', () => {
    const afk = { name: 'AFK', color: '#30A46C' };
    const feature = { name: 'Feature', color: '#BB87FC' };
    const labelled = makeGraph([makeIssue('A', { labels: [afk, feature] }), makeIssue('B', { labels: [afk] }), makeIssue('C', { labels: [afk] })]);
    const after = makeGraph([
      makeIssue('A', { labels: [feature, afk] }),
      makeIssue('B', { labels: [afk, feature] }),
      makeIssue('C', { labels: [{ ...afk, color: '#000000' }] }),
    ]);
    const diff = diffGraphs(labelled, after);
    expect(diff.changed).toEqual(new Map([['B', ['labels']], ['C', ['labels']]]));
    expect(describeDiff(diff)).toBe('2 changed');
  });

  it('counts a renamed status as a state change', () => {
    const after = makeGraph([makeIssue('A', { state: { name: 'Ready', type: 'unstarted', color: '#e2e2e2' } })]);
    expect(diffGraphs(makeGraph([makeIssue('A')]), after).changed).toEqual(new Map([['A', ['state']]]));
  });

  it('marks an issue whose pickability a new blocker took away', () => {
    const after = makeGraph(before.issues, [...before.relations, blocks('B', 'A')], before.external);
    const diff = diffGraphs(before, after);
    expect(diff.changed).toEqual(new Map([['A', ['pickable']]]));
    expect(diff.addedBlocks).toEqual([{ from: 'B', to: 'A' }]);
  });

  it('finds added and removed issues and blocking links, ignoring related links', () => {
    const after = makeGraph(
      [...before.issues.filter((issue) => issue.id !== 'Gone'), makeIssue('New', { assignee: 'Ada' })],
      [blocks('New', 'C'), related('A', 'C')],
      before.external,
    );
    const diff = diffGraphs(before, after);
    expect([...diff.added]).toEqual(['New']);
    expect([...diff.removed]).toEqual(['Gone']);
    expect(diff.addedBlocks).toEqual([{ from: 'New', to: 'C' }]);
    expect(diff.removedBlocks).toEqual([{ from: 'B', to: 'C' }]);
    expect(diff.changed).toEqual(new Map());
  });
});

describe('describeDiff', () => {
  const diff = (counts: { changed?: number; added?: number; removed?: number; addedBlocks?: number; removedBlocks?: number }): GraphDiff => {
    const ids = (count = 0) => Array.from({ length: count }, (_, index) => String(index));
    const pairs = (count = 0) => ids(count).map((id) => ({ from: id, to: id }));
    return {
      changed: new Map(ids(counts.changed).map((id) => [id, ['title']])),
      added: new Set(ids(counts.added)),
      removed: new Set(ids(counts.removed)),
      addedBlocks: pairs(counts.addedBlocks),
      removedBlocks: pairs(counts.removedBlocks),
    };
  };

  it.each([
    [{ changed: 3, added: 1, removed: 1 }, '3 changed · 1 new · 1 removed'],
    [{ added: 2 }, '2 new'],
    [{ addedBlocks: 1, removedBlocks: 2 }, '1 link added · 2 links removed'],
    [{ changed: 1, addedBlocks: 2, removedBlocks: 1 }, '1 changed · 2 links added · 1 link removed'],
    [{}, 'No changes'],
  ])('reads %o as "%s"', (counts, text) => {
    expect(describeDiff(diff(counts))).toBe(text);
  });
});
