import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { Graph } from '../api/types';
import { makeGraph, makeIssue } from '../graph/testIssues';
import { failure, failureOf, NO_GRAPH, ProjectVisit, valueOf, type VisitState } from './ProjectVisit';

const old = makeGraph([makeIssue('old')]);
const fresh = makeGraph([makeIssue('new')]);
const stamp = '2026-10-04T12:00:00Z';
function pending<T>() {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>((done) => { resolve = done; });
  return { promise, resolve };
}
const json = (body: unknown, status = 200) => new Response(JSON.stringify(body), { status });
let saved: ReturnType<typeof pending<Response>>;
let fetched: ReturnType<typeof pending<Response>>;
let state: VisitState;
let visit: ProjectVisit;
const flush = async () => { await vi.advanceTimersByTimeAsync(0); };
async function save(graph: Graph = old) { saved.resolve(json({ snapshot: { graph, fetchedAt: stamp } })); await flush(); }
async function answer(graph: Graph = fresh) { fetched.resolve(json(graph)); await flush(); }
const shown = () => valueOf(state.graph)!;

beforeEach(() => {
  vi.useFakeTimers();
  saved = pending<Response>(); fetched = pending<Response>(); state = NO_GRAPH;
  vi.stubGlobal('fetch', vi.fn((path: string) => path.startsWith('/api/saved') ? saved.promise : fetched.promise));
  visit = new ProjectVisit('project-1', (next) => { state = next; });
});
afterEach(() => { visit.dispose(); vi.useRealTimers(); vi.unstubAllGlobals(); });

describe('one project visit', () => {
  it('starts independent saved and fresh requests, and a first visit applies fresh immediately', async () => {
    expect(vi.mocked(fetch).mock.calls.map(([path]) => path)).toEqual(['/api/saved?project=project-1', '/api/graph?project=project-1']);
    saved.resolve(json({ snapshot: null })); await answer();
    expect(shown()).toEqual(fresh); expect(state.refresh).toBeNull(); expect(state.refreshing).toBe(false);
  });
  it('ignores a late saved answer once fresh is accepted', async () => {
    await answer(); await save(); expect(shown()).toEqual(fresh); expect(state.savedAt).toBeNull();
  });
  it('a saved lookup failure cannot suppress fresh success', async () => {
    saved.resolve(json({ error: { code: 'broken', message: 'broken' } }, 500)); await answer(); expect(shown()).toEqual(fresh);
  });
  it('holds until ready, then through 1999ms and applies at 2000ms with the existing diff', async () => {
    await save(); await answer(); await vi.advanceTimersByTimeAsync(9000);
    expect(shown()).toEqual(old); expect(state.savedAt).toBe(stamp); expect(state.refreshing).toBe(true);
    visit.ready(shown()); await vi.advanceTimersByTimeAsync(1999); expect(shown()).toEqual(old); expect(state.refresh).toBeNull();
    await vi.advanceTimersByTimeAsync(1); expect(shown()).toEqual(fresh); expect(state.refresh?.from).toEqual(old); expect(state.savedAt).toBeNull();
  });
  it('a readiness acknowledgement for another graph does not start the hold', async () => {
    await save(); visit.ready(fresh); await answer(); await vi.advanceTimersByTimeAsync(2000); expect(shown()).toEqual(old);
  });
  it('repeated ready acknowledgements do not extend the viewing window', async () => {
    await save(); visit.ready(shown()); await vi.advanceTimersByTimeAsync(1000); visit.ready(shown()); await answer();
    await vi.advanceTimersByTimeAsync(1000); expect(shown()).toEqual(fresh);
  });
  it('a slow fresh fetch applies immediately once the visible viewing window has elapsed', async () => {
    await save(); visit.ready(shown()); await vi.advanceTimersByTimeAsync(2000); await answer(); expect(shown()).toEqual(fresh);
  });
  it('an unchanged revisit produces an empty diff', async () => {
    await save(); visit.ready(shown()); await answer(old); await vi.advanceTimersByTimeAsync(2000);
    expect(state.refresh?.diff).toEqual(expect.objectContaining({ added: new Set(), removed: new Set(), changed: new Map() }));
  });
  it('failed fetch keeps saved graph and timestamp, then Refresh recovers', async () => {
    await save(); visit.ready(shown()); fetched.resolve(json({ error: { code: 'linear_error', message: 'offline' } }, 502)); await flush();
    expect(shown()).toEqual(old); expect(state.savedAt).toBe(stamp); expect(state.refreshing).toBe(false); expect(state.refreshFailure?.message).toBe('offline');
    fetched = pending<Response>(); visit.refresh(); await answer(); await vi.advanceTimersByTimeAsync(2000);
    expect(shown()).toEqual(fresh); expect(state.refreshFailure).toBeNull();
  });
  it('late saved data can rescue a failed fresh fetch', async () => {
    fetched.resolve(json({ error: { code: 'linear_error', message: 'offline' } }, 502)); await flush(); await save();
    expect(shown()).toEqual(old); expect(state.refreshFailure?.message).toBe('offline'); expect(state.refreshing).toBe(false);
  });
  it('a saved layout failure falls back to the ordinary pending fresh graph', async () => {
    await save(); await answer(); visit.layoutFailed(shown()); expect(shown()).toEqual(fresh); expect(state.refresh).toBeNull();
  });
  it('a saved layout failure before fresh finishes leaves normal loading', async () => {
    await save(); visit.layoutFailed(fresh); expect(shown()).toEqual(old);
    visit.layoutFailed(shown()); expect(state.graph).toBeNull(); await answer(); expect(shown()).toEqual(fresh);
  });
  it.each(['fetch', 'hold', 'layout'])('leaving during %s cancels this visit, including when the next visit is A again', async (stage) => {
    await save();
    if (stage !== 'fetch') await answer();
    if (stage === 'hold') visit.ready(shown());
    visit.dispose(); const before = state;
    await answer(); visit.ready(shown()); visit.layoutFailed(shown()); visit.refresh(); await vi.advanceTimersByTimeAsync(3000);
    expect(state).toBe(before);
    expect(vi.mocked(fetch).mock.calls.every(([, options]) => options?.signal?.aborted)).toBe(true);
  });
  it('a failure after disposal is ignored', async () => {
    visit.dispose(); const before = state;
    fetched.resolve(json({ error: { code: 'linear_error', message: 'offline' } }, 502)); await flush(); expect(state).toBe(before);
  });
  it('ordinary manual Refresh diffs immediately without imposing a saved-map hold', async () => {
    await answer(old); fetched = pending<Response>(); visit.refresh(); await answer(); expect(shown()).toEqual(fresh); expect(state.refresh?.from).toEqual(old);
  });
  it('normalizes viewer failures and reads loaded values', () => {
    expect(failure('oops')).toEqual({ code: 'viewer_error', message: 'oops' });
    expect(failure(new Error('layout'))).toEqual({ code: 'viewer_error', message: 'layout' });
    expect(valueOf(null)).toBeNull(); expect(failureOf(null)).toBeNull();
  });
});
