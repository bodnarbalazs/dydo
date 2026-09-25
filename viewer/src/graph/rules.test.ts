import { describe, expect, it } from 'vitest';
import { isClosed, pickableIds } from './rules';
import { blocks, makeGraph, makeIssue, related } from './testIssues';

describe('isClosed', () => {
  it.each([
    ['completed', true],
    ['canceled', true],
    ['duplicate', true],
    ['started', false],
    ['unstarted', false],
    ['backlog', false],
    ['triage', false],
  ] as const)('%s is closed: %s', (type, closed) => {
    expect(isClosed(makeIssue('a', { type }))).toBe(closed);
  });
});

describe('pickableIds', () => {
  it('marks an unstarted, unassigned issue without blockers', () => {
    expect(pickableIds(makeGraph([makeIssue('a')]))).toEqual(new Set(['a']));
  });

  it('drops the marker when the issue has an assignee', () => {
    expect(pickableIds(makeGraph([makeIssue('a', { assignee: 'Ada' })]))).toEqual(new Set());
  });

  it('drops the marker when an open blocker points at the issue', () => {
    const graph = makeGraph([makeIssue('a'), makeIssue('b', { type: 'started' })], [blocks('b', 'a')]);
    expect(pickableIds(graph)).toEqual(new Set());
  });

  it.each(['completed', 'canceled', 'duplicate'] as const)('keeps the marker when the only blocker is %s', (type) => {
    const graph = makeGraph([makeIssue('a'), makeIssue('b', { type })], [blocks('b', 'a')]);
    expect(pickableIds(graph)).toEqual(new Set(['a']));
  });

  it('counts an open external blocker', () => {
    const graph = makeGraph([makeIssue('a')], [blocks('x', 'a')], [makeIssue('x', { type: 'backlog', project: null })]);
    expect(pickableIds(graph)).toEqual(new Set());
  });

  it('ignores the direction: the blocker itself stays pickable', () => {
    const graph = makeGraph([makeIssue('a'), makeIssue('b')], [blocks('a', 'b')]);
    expect(pickableIds(graph)).toEqual(new Set(['a']));
  });

  it('ignores related links', () => {
    const graph = makeGraph([makeIssue('a'), makeIssue('b', { type: 'started' })], [related('b', 'a')]);
    expect(pickableIds(graph)).toEqual(new Set(['a']));
  });

  it.each(['started', 'backlog', 'triage', 'completed'] as const)('never marks a %s issue', (type) => {
    expect(pickableIds(makeGraph([makeIssue('a', { type })]))).toEqual(new Set());
  });

  it('never marks an external issue', () => {
    expect(pickableIds(makeGraph([], [], [makeIssue('x')]))).toEqual(new Set());
  });
});
