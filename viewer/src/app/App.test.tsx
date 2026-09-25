import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import ELK from 'elkjs/lib/elk.bundled.js';
import type { ELK as ElkApi } from 'elkjs/lib/elk-api';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { Graph, Project, Team } from '../api/types';
import { blocks, makeGraph, makeIssue, related } from '../graph/testIssues';
import { App } from './App';

const teams: Team[] = [
  { id: 'team-1', key: 'T', name: 'Team One' },
  { id: 'team-2', key: 'U', name: 'Team Two' },
];
const projects: Project[] = [{ id: 'project-1', name: 'Project One', url: 'https://linear.app/p1', status: { name: 'In Progress', type: 'started' } }];
const otherProject = { id: 'project-2', name: 'Project Two' };

const graphOne: Graph = makeGraph(
  [
    makeIssue('P', { type: 'started' }),
    makeIssue('A', { parentId: 'P' }),
    makeIssue('B', { parentId: 'P', type: 'completed' }),
    makeIssue('X', { assignee: 'Ada' }),
  ],
  [blocks('E', 'X'), blocks('N', 'A'), related('X', 'B'), related('R', 'X')],
  [
    makeIssue('E', { type: 'started', team: { id: 'team-2', key: 'U' }, project: otherProject }),
    makeIssue('N', { type: 'completed', project: null }),
    makeIssue('R', { project: otherProject }),
  ],
);
const graphTwo: Graph = { ...makeGraph([makeIssue('E', { team: { id: 'team-2', key: 'U' }, project: otherProject })]), project: { ...otherProject, url: 'u' } };

let graphAnswer: () => Response;

function json(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), { status });
}

beforeEach(() => {
  graphAnswer = () => json(graphOne);
  vi.stubGlobal(
    'fetch',
    vi.fn((input: string) => {
      const url = new URL(input, 'http://localhost');
      if (url.pathname === '/api/teams') return Promise.resolve(json({ teams }));
      if (url.pathname === '/api/projects') return Promise.resolve(json({ projects: url.searchParams.get('team') === 'team-1' ? projects : [] }));
      return Promise.resolve(url.searchParams.get('project') === 'project-2' ? json(graphTwo) : graphAnswer());
    }),
  );
});

afterEach(() => {
  vi.unstubAllGlobals();
  window.history.replaceState(null, '', '/');
});

function open(search: string) {
  window.history.replaceState(null, '', `/${search}`);
  return render(<App elk={new ELK()} />);
}

const card = (identifier: string) => document.querySelector(`[data-identifier="${identifier}"]`);

async function mapShown() {
  await waitFor(() => expect(card('T-X')).not.toBeNull());
}

describe('App selectors and URL state', () => {
  it('asks for a team first, then lists its Projects and writes both into the URL', async () => {
    open('');
    expect(await screen.findByText('Choose a team to list its Projects.')).toBeTruthy();
    const [teamSelect, projectSelect] = screen.getAllByRole('combobox');
    await screen.findByRole('option', { name: 'Team One (T)' });
    fireEvent.change(teamSelect!, { target: { value: 'team-1' } });
    expect(window.location.search).toBe('?team=team-1');
    await screen.findByRole('option', { name: 'Project One · In Progress' });
    fireEvent.change(projectSelect!, { target: { value: 'project-1' } });
    expect(window.location.search).toBe('?team=team-1&project=project-1');
    await mapShown();
    expect(screen.getByText('Project One: 4 issues, 1 plates')).toBeTruthy();
  });

  it('prompts for a Project once a team is chosen', async () => {
    open('?team=team-2');
    expect(await screen.findByText('Choose a Project to draw its map.')).toBeTruthy();
  });

  it('follows the browser history', async () => {
    open('?team=team-1&project=project-1');
    await mapShown();
    act(() => {
      window.history.pushState(null, '', '/?team=team-1');
      window.dispatchEvent(new PopStateEvent('popstate'));
    });
    expect(await screen.findByText('Choose a Project to draw its map.')).toBeTruthy();
  });
});

describe('App map', () => {
  it('draws plates, cards, externals and the status of each issue', async () => {
    open('?team=team-1&project=project-1');
    await mapShown();
    expect(document.querySelectorAll('.react-flow__node-plate')).toHaveLength(1);
    expect(card('T-X')?.textContent).toContain('Ada');
    expect(card('T-A')?.textContent).toContain('unassigned');
    expect(card('T-B')?.className).toContain('closed');
    expect(card('T-E')?.className).toContain('external-card');
    expect(card('T-N')?.textContent).toContain('No project');
  });

  it('marks only the pickable issue', async () => {
    open('?team=team-1&project=project-1');
    await mapShown();
    expect([...document.querySelectorAll('.pickable-badge')].map((badge) => badge.closest('[data-identifier]')?.getAttribute('data-identifier'))).toEqual(['T-A']);
  });

  it('selects a clicked card and records it as the focus without leaving the page', async () => {
    open('?team=team-1&project=project-1');
    await mapShown();
    fireEvent.click(card('T-X')!);
    expect(window.location.search).toBe('?team=team-1&project=project-1&focus=X');
    await waitFor(() => expect(card('T-X')?.className).toContain('selected'));
  });

  it('ignores a click on the Linear button, which opens a new tab', async () => {
    open('?team=team-1&project=project-1');
    await mapShown();
    const link = within(card('T-X') as HTMLElement).getByRole('link', { name: 'Open T-X in Linear' });
    expect(link.getAttribute('href')).toBe('https://linear.app/t/issue/T-X');
    expect(link.getAttribute('target')).toBe('_blank');
    fireEvent.click(link);
    expect(window.location.search).toBe('?team=team-1&project=project-1');
  });

  it('opens an external issue with a Project in its own team and Project, focused', async () => {
    open('?team=team-1&project=project-1');
    await mapShown();
    fireEvent.click(card('T-E')!);
    expect(window.location.search).toBe('?team=team-2&project=project-2&focus=E');
    await waitFor(() => expect(screen.getByText('Project Two: 1 issues, 0 plates')).toBeTruthy());
  });

  it('only focuses an external issue without a Project', async () => {
    open('?team=team-1&project=project-1');
    await mapShown();
    fireEvent.click(card('T-N')!);
    expect(window.location.search).toBe('?team=team-1&project=project-1&focus=N');
  });

  it('collapses and expands one plate, and all of them', async () => {
    open('?team=team-1&project=project-1');
    await mapShown();
    fireEvent.click(screen.getByRole('button', { name: 'Collapse T-P' }));
    await waitFor(() => expect(card('T-A')).toBeNull());
    expect(window.location.search).toContain('focus=P');
    fireEvent.click(screen.getByRole('button', { name: 'Expand T-P' }));
    await waitFor(() => expect(card('T-A')).not.toBeNull());
    fireEvent.click(screen.getByRole('button', { name: 'Collapse all' }));
    await waitFor(() => expect(card('T-A')).toBeNull());
    fireEvent.click(screen.getByRole('button', { name: 'Expand all' }));
    await waitFor(() => expect(card('T-A')).not.toBeNull());
  });

  it('lays related links out only when asked', async () => {
    open('?team=team-1&project=project-1');
    await mapShown();
    expect(card('T-R')).toBeNull();
    fireEvent.click(screen.getByRole('checkbox', { name: 'Show related' }));
    await waitFor(() => expect(card('T-R')).not.toBeNull());
  });
});

describe('App errors', () => {
  it('shows the error envelope to the user', async () => {
    graphAnswer = () => json({ error: { code: 'linear_rate_limited', message: 'Linear is rate limiting this key.' } }, 503);
    open('?team=team-1&project=project-1');
    const alert = await screen.findByRole('alert');
    expect(alert.textContent).toBe('linear_rate_limited Linear is rate limiting this key.');
    expect(screen.getByText('The map could not be drawn.')).toBeTruthy();
  });

  it('shows a failed team list, and the failed Project list beside it', async () => {
    vi.stubGlobal('fetch', vi.fn(() => Promise.resolve(json({ error: { code: 'linear_auth', message: 'Key rejected.' } }, 502))));
    open('?team=team-1');
    await waitFor(() => expect(screen.getAllByRole('alert').map((alert) => alert.textContent)).toEqual(['linear_auth Key rejected.', 'linear_auth Key rejected.']));
  });

  it('shows a layout failure', async () => {
    const broken = { layout: () => Promise.reject(new Error('ELK exploded')) } as unknown as ElkApi;
    window.history.replaceState(null, '', '/?team=team-1&project=project-1');
    render(<App elk={broken} />);
    expect((await screen.findByRole('alert')).textContent).toBe('viewer_error ELK exploded');
  });
});

describe('App errors belong to the view that failed', () => {
  const rateLimited = () => json({ error: { code: 'linear_rate_limited', message: 'Linear is rate limiting this key.' } }, 503);
  const twoProjects: Project[] = [...projects, { id: 'project-2', name: 'Project Two', url: 'u', status: { name: 'Planned', type: 'planned' } }];
  let answers: Record<string, () => Promise<Response>>;

  beforeEach(() => {
    answers = {
      'project-1': () => Promise.resolve(rateLimited()),
      'project-2': () => new Promise<Response>(() => undefined),
    };
    vi.stubGlobal(
      'fetch',
      vi.fn((input: string) => {
        const url = new URL(input, 'http://localhost');
        if (url.pathname === '/api/teams') return Promise.resolve(json({ teams }));
        if (url.pathname === '/api/projects') return Promise.resolve(json({ projects: url.searchParams.get('team') === 'team-1' ? twoProjects : [] }));
        return answers[url.searchParams.get('project') ?? '']!();
      }),
    );
  });

  async function failedProjectOne() {
    open('?team=team-1&project=project-1');
    await screen.findByRole('alert');
    await screen.findByRole('option', { name: 'Project Two · Planned' });
  }

  it('drops a failed Project\'s error when another Project loads', async () => {
    await failedProjectOne();
    fireEvent.change(screen.getAllByRole('combobox')[1]!, { target: { value: 'project-2' } });
    expect(screen.queryByRole('alert')).toBeNull();
    expect(screen.getByText('Loading the Project map…')).toBeTruthy();
  });

  it('drops a failed Project\'s error when only the team changes', async () => {
    await failedProjectOne();
    fireEvent.change(screen.getAllByRole('combobox')[0]!, { target: { value: 'team-2' } });
    expect(screen.queryByRole('alert')).toBeNull();
    expect(screen.getByText('Choose a Project to draw its map.')).toBeTruthy();
  });

  it('clears a Project\'s error once it loads on a later visit', async () => {
    await failedProjectOne();
    fireEvent.change(screen.getAllByRole('combobox')[1]!, { target: { value: 'project-2' } });
    answers['project-1'] = () => Promise.resolve(json(graphOne));
    act(() => {
      window.history.back();
    });
    await waitFor(() => expect(window.location.search).toBe('?team=team-1&project=project-1'));
    await mapShown();
    expect(screen.queryByRole('alert')).toBeNull();
  });

  it('drops a failed Project list\'s error when the team changes', async () => {
    const fetchProjectsOnce = vi.mocked(fetch);
    fetchProjectsOnce.mockImplementationOnce(() => Promise.resolve(json({ teams })));
    fetchProjectsOnce.mockImplementationOnce(() => Promise.resolve(rateLimited()));
    open('?team=team-1');
    expect((await screen.findByRole('alert')).textContent).toContain('linear_rate_limited');
    fireEvent.change(screen.getAllByRole('combobox')[0]!, { target: { value: 'team-2' } });
    expect(screen.queryByRole('alert')).toBeNull();
  });

  it('keeps a failed team list\'s error across navigation', async () => {
    vi.mocked(fetch).mockImplementationOnce(() => Promise.resolve(rateLimited()));
    open('?team=team-1');
    await screen.findByRole('alert');
    act(() => {
      window.history.pushState(null, '', '/?team=team-2');
      window.dispatchEvent(new PopStateEvent('popstate'));
    });
    expect((await screen.findByRole('alert')).textContent).toContain('linear_rate_limited');
  });

  /** Opens Project One with a map, leaves for Project Two, which never answers, then returns. */
  async function revisitProjectOne(elk: ElkApi, refetch: () => Promise<Response>) {
    answers['project-1'] = () => Promise.resolve(json(graphOne));
    window.history.replaceState(null, '', '/?team=team-1&project=project-1');
    render(<App elk={elk} />);
    await mapShown();
    await screen.findByRole('option', { name: 'Project Two · Planned' });
    fireEvent.change(screen.getAllByRole('combobox')[1]!, { target: { value: 'project-2' } });
    answers['project-1'] = refetch;
    fireEvent.change(screen.getAllByRole('combobox')[1]!, { target: { value: 'project-1' } });
  }

  it('draws nothing from an earlier visit while a revisited Project refetches', async () => {
    await revisitProjectOne(new ELK(), () => new Promise<Response>(() => undefined));
    expect(card('T-X')).toBeNull();
    expect(screen.getByText('Loading the Project map…')).toBeTruthy();
  });

  it('keeps a revisited Project\'s refetch failure through a relayout request', async () => {
    const real = new ELK();
    let layouts = 0;
    const counted = {
      layout: (graph: Parameters<ElkApi['layout']>[0]) => {
        layouts += 1;
        return real.layout(graph);
      },
    } as unknown as ElkApi;
    await revisitProjectOne(counted, () => Promise.resolve(rateLimited()));
    expect((await screen.findByRole('alert')).textContent).toContain('linear_rate_limited');
    const before = layouts;
    fireEvent.click(screen.getByRole('checkbox', { name: 'Show related' }));
    expect(layouts).toBe(before);
    expect(screen.getByRole('alert').textContent).toContain('linear_rate_limited');
    expect(screen.getByText('The map could not be drawn.')).toBeTruthy();
    expect(card('T-X')).toBeNull();
  });

  it('keeps a revisited Project\'s refetch failure when a layout of that Project finishes late', async () => {
    const real = new ELK();
    const held: { graph: Parameters<ElkApi['layout']>[0]; result: ReturnType<typeof deferred<Awaited<ReturnType<ElkApi['layout']>>>> }[] = [];
    const elk = {
      layout: (graph: Parameters<ElkApi['layout']>[0]) => {
        const result = deferred<Awaited<ReturnType<ElkApi['layout']>>>();
        held.push({ graph, result });
        // The first layout answers at once so the map is drawn; later ones wait for the test.
        if (held.length === 1) void real.layout(graph).then(result.resolve);
        return result.promise;
      },
    } as unknown as ElkApi;
    await revisitProjectOne(elk, () => Promise.resolve(rateLimited()));
    expect((await screen.findByRole('alert')).textContent).toContain('linear_rate_limited');
    await act(async () => {
      for (const layout of held.slice(1)) layout.result.resolve(await real.layout(layout.graph));
      // A zero-delay task runs after every promise callback those answers queued.
      await new Promise((drained) => setTimeout(drained, 0));
    });
    expect(screen.getByRole('alert').textContent).toContain('linear_rate_limited');
    expect(card('T-X')).toBeNull();
  });

  it('shows the failure of every stage on screen, and a team list failure is no map failure', async () => {
    let graphAnswer: () => Promise<Response> = () => new Promise<Response>(() => undefined);
    vi.mocked(fetch).mockImplementation((input) => {
      const url = new URL(input as string, 'http://localhost');
      if (url.pathname === '/api/teams') return Promise.resolve(json({ error: { code: 'linear_auth', message: 'Key rejected.' } }, 502));
      if (url.pathname === '/api/projects') return Promise.resolve(json({ projects: twoProjects }));
      return graphAnswer();
    });
    open('?team=team-1&project=project-1');
    expect((await screen.findByRole('alert')).textContent).toBe('linear_auth Key rejected.');
    expect(screen.getByText('Loading the Project map…')).toBeTruthy();
    graphAnswer = () => Promise.resolve(rateLimited());
    await screen.findByRole('option', { name: 'Project Two · Planned' });
    fireEvent.change(screen.getAllByRole('combobox')[1]!, { target: { value: 'project-2' } });
    await waitFor(() => expect(screen.getAllByRole('alert')).toHaveLength(2));
    expect(screen.getAllByRole('alert').map((alert) => alert.textContent)).toEqual(['linear_auth Key rejected.', 'linear_rate_limited Linear is rate limiting this key.']);
    expect(screen.getByText('The map could not be drawn.')).toBeTruthy();
  });

  it('shows a failed relayout instead of the map it could not redraw', async () => {
    const real = new ELK();
    let calls = 0;
    const flaky = { layout: (graph: Parameters<ElkApi['layout']>[0]) => (++calls === 2 ? Promise.reject(new Error('ELK exploded')) : real.layout(graph)) } as unknown as ElkApi;
    answers['project-1'] = () => Promise.resolve(json(graphOne));
    window.history.replaceState(null, '', '/?team=team-1&project=project-1');
    render(<App elk={flaky} />);
    await mapShown();
    fireEvent.click(screen.getByRole('checkbox', { name: 'Show related' }));
    expect((await screen.findByRole('alert')).textContent).toBe('viewer_error ELK exploded');
    expect(card('T-X')).toBeNull();
    expect(screen.getByText('The map could not be drawn.')).toBeTruthy();
  });

  it('clears a layout failure once the map lays out', async () => {
    const real = new ELK();
    let calls = 0;
    const flaky = { layout: (graph: Parameters<ElkApi['layout']>[0]) => (++calls === 1 ? Promise.reject(new Error('ELK exploded')) : real.layout(graph)) } as unknown as ElkApi;
    answers['project-1'] = () => Promise.resolve(json(graphOne));
    window.history.replaceState(null, '', '/?team=team-1&project=project-1');
    render(<App elk={flaky} />);
    await screen.findByRole('alert');
    fireEvent.click(screen.getByRole('checkbox', { name: 'Show related' }));
    await mapShown();
    expect(screen.queryByRole('alert')).toBeNull();
  });
});

function deferred<T>() {
  let resolve: (value: T) => void = () => undefined;
  let reject: (reason: Error) => void = () => undefined;
  const promise = new Promise<T>((settle, fail) => {
    resolve = settle;
    reject = fail;
  });
  return { promise, resolve, reject };
}

describe('App ignores answers for a view it has left', () => {
  const rateLimited = () => json({ error: { code: 'linear_rate_limited', message: 'Linear is rate limiting this key.' } }, 503);
  const projectTwo: Project = { id: 'project-2', name: 'Project Two', url: 'u', status: { name: 'Planned', type: 'planned' } };
  let pending: Map<string, ReturnType<typeof deferred<Response>>[]>;

  /** Each request waits until the test answers it, oldest first per path and parameter. */
  function answer(key: string, response: Response) {
    return act(async () => {
      pending.get(key)?.shift()?.resolve(response);
      await Promise.resolve();
    });
  }

  beforeEach(() => {
    pending = new Map();
    vi.stubGlobal(
      'fetch',
      vi.fn((input: string) => {
        const url = new URL(input, 'http://localhost');
        if (url.pathname === '/api/teams') return Promise.resolve(json({ teams }));
        const key = url.searchParams.get('team') ?? url.searchParams.get('project') ?? '';
        const request = deferred<Response>();
        pending.set(key, [...(pending.get(key) ?? []), request]);
        return request.promise;
      }),
    );
  });

  const pick = (index: number, value: string) => {
    fireEvent.change(screen.getAllByRole('combobox')[index]!, { target: { value } });
  };

  it('keeps the new team\'s Projects when the old team\'s list arrives late', async () => {
    open('?team=team-1');
    await screen.findByRole('option', { name: 'Team Two (U)' });
    pick(0, 'team-2');
    await answer('team-2', json({ projects: [projectTwo] }));
    await screen.findByRole('option', { name: 'Project Two · Planned' });
    await answer('team-1', json({ projects }));
    expect(screen.getByRole('option', { name: 'Project Two · Planned' })).toBeTruthy();
  });

  it('lists no Projects of the old team while the new team\'s list loads', async () => {
    open('?team=team-1');
    await answer('team-1', json({ projects }));
    await screen.findByRole('option', { name: 'Project One · In Progress' });
    pick(0, 'team-2');
    expect(screen.queryByRole('option', { name: 'Project One · In Progress' })).toBeNull();
  });

  it('drops a Project list failure that arrives after the team changed', async () => {
    open('?team=team-1');
    await screen.findByRole('option', { name: 'Team Two (U)' });
    pick(0, 'team-2');
    await answer('team-1', rateLimited());
    pick(0, 'team-1');
    expect(screen.queryByRole('alert')).toBeNull();
  });

  it('clears a team\'s Project list failure once the list loads on a later visit', async () => {
    open('?team=team-1');
    await answer('team-1', rateLimited());
    await screen.findByRole('alert');
    pick(0, 'team-2');
    pick(0, 'team-1');
    await answer('team-1', json({ projects }));
    await screen.findByRole('option', { name: 'Project One · In Progress' });
    expect(screen.queryByRole('alert')).toBeNull();
  });

  it('keeps the new Project\'s map when the old Project\'s graph arrives late', async () => {
    open('?team=team-1&project=project-1');
    await answer('team-1', json({ projects: [...projects, projectTwo] }));
    await screen.findByRole('option', { name: 'Project Two · Planned' });
    pick(1, 'project-2');
    await answer('project-2', json(graphTwo));
    await waitFor(() => expect(card('T-E')).not.toBeNull());
    await answer('project-1', json(graphOne));
    await new Promise((settle) => setTimeout(settle, 50));
    expect(card('T-E')).not.toBeNull();
    expect(card('T-X')).toBeNull();
    expect(screen.getByText('Project Two: 1 issues, 0 plates')).toBeTruthy();
  });

  it('drops a graph failure that arrives after the Project changed', async () => {
    open('?team=team-1&project=project-1');
    await answer('team-1', json({ projects: [...projects, projectTwo] }));
    await screen.findByRole('option', { name: 'Project Two · Planned' });
    pick(1, 'project-2');
    await answer('project-1', rateLimited());
    expect(screen.queryByRole('alert')).toBeNull();
    pick(1, 'project-1');
    expect(screen.queryByRole('alert')).toBeNull();
    expect(screen.getByText('Loading the Project map…')).toBeTruthy();
  });
});

describe('App ignores layouts it no longer needs', () => {
  const real = new ELK();
  let layouts: { graph: Parameters<ElkApi['layout']>[0]; result: ReturnType<typeof deferred<Awaited<ReturnType<ElkApi['layout']>>>> }[];
  const elk = {
    layout: (graph: Parameters<ElkApi['layout']>[0]) => {
      const result = deferred<Awaited<ReturnType<ElkApi['layout']>>>();
      layouts.push({ graph, result });
      return result.promise;
    },
  } as unknown as ElkApi;

  async function finish(index: number) {
    const layout = layouts[index]!;
    const done = await real.layout(layout.graph);
    await act(async () => {
      layout.result.resolve(done);
      await new Promise((settle) => setTimeout(settle, 20));
    });
  }

  beforeEach(() => {
    layouts = [];
  });

  function openWithElk(search: string) {
    window.history.replaceState(null, '', `/${search}`);
    return render(<App elk={elk} />);
  }

  it('keeps the newer layout when an older one finishes late', async () => {
    openWithElk('?team=team-1&project=project-1');
    await waitFor(() => expect(layouts).toHaveLength(1));
    fireEvent.click(screen.getByRole('checkbox', { name: 'Show related' }));
    await waitFor(() => expect(layouts).toHaveLength(2));
    await finish(1);
    await waitFor(() => expect(card('T-R')).not.toBeNull());
    await finish(0);
    expect(card('T-R')).not.toBeNull();
  });

  it('drops a failure of a layout it no longer needs', async () => {
    openWithElk('?team=team-1&project=project-1');
    await waitFor(() => expect(layouts).toHaveLength(1));
    fireEvent.click(screen.getByRole('checkbox', { name: 'Show related' }));
    await waitFor(() => expect(layouts).toHaveLength(2));
    await finish(1);
    await waitFor(() => expect(card('T-R')).not.toBeNull());
    await act(async () => {
      layouts[0]!.result.reject(new Error('ELK exploded'));
      await new Promise((settle) => setTimeout(settle, 20));
    });
    expect(screen.queryByRole('alert')).toBeNull();
  });

  it('shows no stale failure while a Project it failed before is laid out again', async () => {
    let graphFails = true;
    vi.mocked(fetch).mockImplementation((input) => {
      const url = new URL(input as string, 'http://localhost');
      if (url.pathname === '/api/teams') return Promise.resolve(json({ teams }));
      if (url.pathname === '/api/projects') return Promise.resolve(json({ projects: [...projects, { ...projects[0]!, id: 'project-2', name: 'Project Two' }] }));
      if (url.searchParams.get('project') === 'project-2') return new Promise<Response>(() => undefined);
      return Promise.resolve(graphFails ? json({ error: { code: 'linear_error', message: 'Linear failed.' } }, 502) : json(graphOne));
    });
    openWithElk('?team=team-1&project=project-1');
    await screen.findByRole('alert');
    await screen.findByRole('option', { name: 'Project Two · In Progress' });
    fireEvent.change(screen.getAllByRole('combobox')[1]!, { target: { value: 'project-2' } });
    graphFails = false;
    fireEvent.change(screen.getAllByRole('combobox')[1]!, { target: { value: 'project-1' } });
    await waitFor(() => expect(layouts).toHaveLength(1));
    expect(screen.queryByRole('alert')).toBeNull();
  });
});

describe('App navigation and fitting', () => {
  const scale = () => Number(/scale\(([\d.]+)\)/.exec(document.querySelector<HTMLElement>('.react-flow__viewport')?.style.transform ?? '')?.[1]);
  const settle = () => act(() => new Promise((done) => setTimeout(done, 50)));

  it('asks for no Projects before a team is chosen', async () => {
    open('');
    await screen.findByRole('option', { name: 'Team One (T)' });
    expect(vi.mocked(fetch).mock.calls.map(([input]) => input as string)).toEqual(['/api/teams']);
  });

  it('adds a history entry per selector change, external jump and nothing for a focus', async () => {
    open('?team=team-1');
    await screen.findByRole('option', { name: 'Project One · In Progress' });
    const start = window.history.length;
    fireEvent.change(screen.getAllByRole('combobox')[1]!, { target: { value: 'project-1' } });
    await mapShown();
    fireEvent.click(card('T-X')!);
    expect(window.history.length).toBe(start + 1);
    fireEvent.click(card('T-E')!);
    expect(window.history.length).toBe(start + 2);
    await screen.findByRole('option', { name: 'Team One (T)' });
    fireEvent.change(screen.getAllByRole('combobox')[0]!, { target: { value: 'team-1' } });
    expect(window.history.length).toBe(start + 3);
  });

  it('drops the focus when another Project is chosen', async () => {
    open('?team=team-1&project=project-2&focus=E');
    await screen.findByRole('option', { name: 'Project One · In Progress' });
    fireEvent.change(screen.getAllByRole('combobox')[1]!, { target: { value: 'project-1' } });
    expect(window.location.search).toBe('?team=team-1&project=project-1');
  });

  it('opens a Project with every plate expanded, whatever was collapsed before', async () => {
    open('?team=team-1&project=project-1');
    await mapShown();
    fireEvent.click(screen.getByRole('button', { name: 'Collapse all' }));
    await waitFor(() => expect(card('T-A')).toBeNull());
    act(() => {
      window.history.pushState(null, '', '/?team=team-1&project=project-2');
      window.dispatchEvent(new PopStateEvent('popstate'));
    });
    await waitFor(() => expect(card('T-E')).not.toBeNull());
    act(() => {
      window.history.pushState(null, '', '/?team=team-1&project=project-1');
      window.dispatchEvent(new PopStateEvent('popstate'));
    });
    await waitFor(() => expect(card('T-A')).not.toBeNull());
  });

  it('offers Collapse all only for a map with plates', async () => {
    open('?team=team-2&project=project-2');
    await waitFor(() => expect(card('T-E')).not.toBeNull());
    expect(screen.getByRole<HTMLButtonElement>('button', { name: 'Collapse all' }).disabled).toBe(true);
  });

  it('fits each newly opened Project into view', async () => {
    open('?team=team-1&project=project-1');
    await mapShown();
    await settle();
    expect(scale()).toBeLessThan(1);
    act(() => {
      window.history.pushState(null, '', '/?team=team-1&project=project-2');
      window.dispatchEvent(new PopStateEvent('popstate'));
    });
    await waitFor(() => expect(card('T-E')).not.toBeNull());
    await settle();
    expect(scale()).toBeGreaterThan(1);
  });

  it('fits the map again after Collapse all', async () => {
    open('?team=team-1&project=project-1');
    await mapShown();
    await settle();
    const expanded = scale();
    fireEvent.click(screen.getByRole('button', { name: 'Collapse all' }));
    await waitFor(() => expect(card('T-A')).toBeNull());
    await settle();
    expect(scale()).toBeGreaterThan(expanded);
  });
});
