import { act, render, screen } from '@testing-library/react';
import ELK from 'elkjs/lib/elk.bundled.js';
import { afterEach, expect, it, vi } from 'vitest';
import { makeGraph, makeIssue } from '../graph/testIssues';
import { App } from './App';

afterEach(() => { vi.useRealTimers(); vi.unstubAllGlobals(); window.history.replaceState(null, '', '/'); });

it('a saved revisit renders the saved map and timestamp while fresh Linear is blocked', async () => {
  const graph = makeGraph([makeIssue('saved')]);
  vi.stubGlobal('fetch', vi.fn((input: string) => {
    const path = new URL(input, 'http://localhost').pathname;
    if (path === '/api/graph') return new Promise<Response>(() => undefined);
    const body = path === '/api/saved' ? { snapshot: { graph, fetchedAt: '2026-10-04T12:00:00Z' } } : { teams: [], projects: [] };
    return Promise.resolve(new Response(JSON.stringify(body)));
  }));
  window.history.replaceState(null, '', '/?team=t&project=project-1');
  act(() => { render(<App elk={new ELK()} />); });
  expect(await screen.findByText(/Saved map/)).toBeTruthy();
  expect(await screen.findByText('Issue saved')).toBeTruthy();
});

function deferred<T>() {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>((done) => { resolve = done; });
  return { promise, resolve };
}

function navigate(project: string) {
  act(() => {
    window.history.pushState(null, '', `/?team=t&project=${project}`);
    window.dispatchEvent(new PopStateEvent('popstate'));
  });
}

it.each(['lookup', 'fetch', 'hold', 'layout'])('A to B to A ignores the old A visit during %s', async (stage) => {
  vi.useFakeTimers();
  const old = makeGraph([makeIssue('old')]);
  const obsolete = makeGraph([makeIssue('obsolete')]);
  const current = makeGraph([makeIssue('current')]);
  const firstFetch = deferred<Response>();
  const firstSaved = deferred<Response>();
  const firstLayout = deferred<import('elkjs/lib/elk-api').ElkNode>();
  let aVisits = 0;
  let graphToLayOut: import('elkjs/lib/elk-api').ElkNode | undefined;
  vi.stubGlobal('fetch', vi.fn((input: string) => {
    const url = new URL(input, 'http://localhost');
    const isA = url.searchParams.get('project') === 'project-1';
    if (url.pathname === '/api/saved') {
      if (isA) aVisits += 1;
      if (isA && aVisits === 1 && stage === 'lookup') return firstSaved.promise;
      return Promise.resolve(new Response(JSON.stringify({ snapshot: isA && aVisits === 1 ? { graph: old, fetchedAt: '2026-10-04T12:00:00Z' } : null })));
    }
    if (url.pathname !== '/api/graph') return Promise.resolve(new Response('{"teams":[],"projects":[]}'));
    if (!isA) return new Promise<Response>(() => undefined);
    return aVisits === 1 ? firstFetch.promise : Promise.resolve(new Response(JSON.stringify(current)));
  }));
  const elk = { layout: (graph: import('elkjs/lib/elk-api').ElkNode) => {
    if (stage === 'layout' && graphToLayOut === undefined) { graphToLayOut = graph; return firstLayout.promise; }
    return Promise.resolve(graph);
  } } as unknown as import('elkjs/lib/elk-api').ELK;
  window.history.replaceState(null, '', '/?team=t&project=project-1');
  render(<App elk={elk} />);
  await act(() => vi.advanceTimersByTimeAsync(100));
  if (stage !== 'fetch') {
    firstFetch.resolve(new Response(JSON.stringify(obsolete)));
    await act(() => vi.advanceTimersByTimeAsync(100));
  }
  navigate('project-b'); await act(() => vi.advanceTimersByTimeAsync(0));
  navigate('project-1'); await act(() => vi.advanceTimersByTimeAsync(100));
  firstFetch.resolve(new Response(JSON.stringify(obsolete)));
  firstSaved.resolve(new Response(JSON.stringify({ snapshot: { graph: old, fetchedAt: '2026-10-04T12:00:00Z' } })));
  if (graphToLayOut !== undefined) firstLayout.resolve(graphToLayOut);
  await act(() => vi.advanceTimersByTimeAsync(5000));
  expect(screen.getByText('Issue current')).toBeTruthy();
  expect(screen.queryByText('Issue obsolete')).toBeNull();
  expect(screen.queryByText('Issue old')).toBeNull();
  expect(screen.queryByText(/Saved map/)).toBeNull();
  if (stage === 'lookup') expect(vi.mocked(fetch).mock.calls.filter(([path]) => path === '/api/graph?project=project-1')).toHaveLength(1);
  vi.useRealTimers();
});

it('a failed saved layout falls back to ordinary fresh loading and enables retry after fresh failure', async () => {
  const fresh = deferred<Response>();
  let saved = true;
  vi.stubGlobal('fetch', vi.fn((path: string) => {
    if (path.startsWith('/api/graph')) return fresh.promise;
    const body = path.startsWith('/api/saved') ? { snapshot: { graph: makeGraph([makeIssue('saved')]), fetchedAt: '2026-10-04T12:00:00Z' } } : { teams: [], projects: [] };
    return Promise.resolve(new Response(JSON.stringify(body)));
  }));
  const elk = { layout: (graph: import('elkjs/lib/elk-api').ElkNode) => {
    if (saved) { saved = false; return Promise.reject(new Error('saved layout failed')); }
    return Promise.resolve(graph);
  } } as unknown as import('elkjs/lib/elk-api').ELK;
  window.history.replaceState(null, '', '/?team=t&project=project-1');
  render(<App elk={elk} />);
  await screen.findByText('Loading the Project map…');
  await act(async () => { fresh.resolve(new Response(JSON.stringify({ error: { code: 'offline', message: 'fetch failed' } }), { status: 502 })); await fresh.promise; });
  expect(await screen.findByRole('alert')).toBeTruthy();
  expect(screen.getByRole('button', { name: 'Refresh' }).hasAttribute('disabled')).toBe(false);
});

it('Refresh stays disabled until the first fresh layout has landed', async () => {
  const laid = deferred<import('elkjs/lib/elk-api').ElkNode>();
  const requested = deferred<import('elkjs/lib/elk-api').ElkNode>();
  vi.stubGlobal('fetch', vi.fn((path: string) => {
    let body: unknown = { teams: [], projects: [] };
    if (path.startsWith('/api/saved')) body = { snapshot: null };
    if (path.startsWith('/api/graph')) body = makeGraph([makeIssue('fresh')]);
    return Promise.resolve(new Response(JSON.stringify(body)));
  }));
  const elk = { layout: (graph: import('elkjs/lib/elk-api').ElkNode) => { requested.resolve(graph); return laid.promise; } } as unknown as import('elkjs/lib/elk-api').ELK;
  window.history.replaceState(null, '', '/?team=t&project=project-1');
  render(<App elk={elk} />);
  await screen.findByText('Project One: 1 issues, 0 plates');
  const input = await requested.promise;
  expect(screen.getByRole('button', { name: 'Refresh' }).hasAttribute('disabled')).toBe(true);
  await act(async () => { laid.resolve(input); await laid.promise; });
  expect(await screen.findByText('Issue fresh')).toBeTruthy();
  expect(screen.getByRole('button', { name: 'Refresh' }).hasAttribute('disabled')).toBe(false);
});
