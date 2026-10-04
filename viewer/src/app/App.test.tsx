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
const projects: Project[] = [{ id: 'project-1', name: 'Project One', url: 'https://linear.app/p1', status: { name: 'In Progress', type: 'started' }, targetDate: null, completedAt: null, canceledAt: null }];
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
      if (url.pathname === '/api/saved') return Promise.resolve(json({ snapshot: null }));
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

const projectPicker = () => screen.getByRole('button', { name: /^Project / });

/** Opens the Project picker, waits until it lists `name`, and closes it again. */
async function listed(name: string) {
  fireEvent.click(projectPicker());
  await screen.findByRole('option', { name });
  fireEvent.keyDown(screen.getByRole('combobox', { name: 'Search Projects' }), { key: 'Escape' });
}

function chooseProject(name: string) {
  fireEvent.click(projectPicker());
  fireEvent.click(screen.getByRole('option', { name }));
}

const card = (identifier: string) => document.querySelector(`[data-identifier="${identifier}"]`);

async function mapShown() {
  await waitFor(() => expect(card('T-X')).not.toBeNull());
}

describe('App selectors and URL state', () => {
  it('asks for a team first, then lists its Projects and writes both into the URL', async () => {
    open('');
    expect(await screen.findByText('Choose a team to list its Projects.')).toBeTruthy();
    const teamSelect = screen.getByRole('combobox');
    await screen.findByRole('option', { name: 'Team One (T)' });
    fireEvent.change(teamSelect, { target: { value: 'team-1' } });
    expect(window.location.search).toBe('?team=team-1');
    await listed('Project One');
    chooseProject('Project One');
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
    expect(card('T-A')?.textContent).toContain('Pickable');
    expect(card('T-B')?.textContent).toContain('unassigned');
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

  it('ignores a click on the Linear link, which opens a new tab', async () => {
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

describe('App theme', () => {
  it('themes the page, and the cards on the map, from the toolbar', async () => {
    localStorage.clear();
    open('?team=team-1&project=project-1');
    await mapShown();
    const background = () => (card('T-X') as HTMLElement).style.background;
    const light = background();
    fireEvent.click(screen.getByRole('button', { name: 'Dark theme' }));
    expect(document.documentElement.dataset['theme']).toBe('dark');
    expect(background()).not.toBe(light);
    fireEvent.click(screen.getByRole('button', { name: 'System theme' }));
    expect(document.documentElement.dataset['theme']).toBe('light');
    expect(background()).toBe(light);
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
  const twoProjects: Project[] = [...projects, { id: 'project-2', name: 'Project Two', url: 'u', status: { name: 'Planned', type: 'planned' }, targetDate: null, completedAt: null, canceledAt: null }];
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
        if (url.pathname === '/api/saved') return Promise.resolve(json({ snapshot: null }));
      if (url.pathname === '/api/teams') return Promise.resolve(json({ teams }));
        if (url.pathname === '/api/projects') return Promise.resolve(json({ projects: url.searchParams.get('team') === 'team-1' ? twoProjects : [] }));
        return answers[url.searchParams.get('project') ?? '']!();
      }),
    );
  });

  async function failedProjectOne() {
    open('?team=team-1&project=project-1');
    await screen.findByRole('alert');
    await listed('Project Two');
  }

  it('drops a failed Project\'s error when another Project loads', async () => {
    await failedProjectOne();
    chooseProject('Project Two');
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
    chooseProject('Project Two');
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
    await listed('Project Two');
    chooseProject('Project Two');
    answers['project-1'] = refetch;
    chooseProject('Project One');
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
      if (url.pathname === '/api/saved') return Promise.resolve(json({ snapshot: null }));
      if (url.pathname === '/api/teams') return Promise.resolve(json({ error: { code: 'linear_auth', message: 'Key rejected.' } }, 502));
      if (url.pathname === '/api/projects') return Promise.resolve(json({ projects: twoProjects }));
      return graphAnswer();
    });
    open('?team=team-1&project=project-1');
    expect((await screen.findByRole('alert')).textContent).toBe('linear_auth Key rejected.');
    expect(screen.getByText('Loading the Project map…')).toBeTruthy();
    graphAnswer = () => Promise.resolve(rateLimited());
    await listed('Project Two');
    chooseProject('Project Two');
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
  const projectTwo: Project = { id: 'project-2', name: 'Project Two', url: 'u', status: { name: 'Planned', type: 'planned' }, targetDate: null, completedAt: null, canceledAt: null };
  let pending: Map<string, ReturnType<typeof deferred<Response>>[]>;

  /** Each request waits until the test answers it, oldest first per path and parameter. */
  async function answer(key: string, response: Response) {
    await waitFor(() => expect(pending.get(key)?.length).toBeGreaterThan(0));
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
        if (url.pathname === '/api/saved') return Promise.resolve(json({ snapshot: null }));
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
    await listed('Project Two');
    await answer('team-1', json({ projects }));
    await listed('Project Two');
  });

  it('lists no Projects of the old team while the new team\'s list loads', async () => {
    open('?team=team-1');
    await answer('team-1', json({ projects }));
    await listed('Project One');
    pick(0, 'team-2');
    fireEvent.click(projectPicker());
    expect(screen.queryByRole('option', { name: 'Project One' })).toBeNull();
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
    await listed('Project One');
    expect(screen.queryByRole('alert')).toBeNull();
  });

  it('keeps the new Project\'s map when the old Project\'s graph arrives late', async () => {
    open('?team=team-1&project=project-1');
    await answer('team-1', json({ projects: [...projects, projectTwo] }));
    await listed('Project Two');
    chooseProject('Project Two');
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
    await listed('Project Two');
    chooseProject('Project Two');
    await answer('project-1', rateLimited());
    expect(screen.queryByRole('alert')).toBeNull();
    chooseProject('Project One');
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
      if (url.pathname === '/api/saved') return Promise.resolve(json({ snapshot: null }));
      if (url.pathname === '/api/teams') return Promise.resolve(json({ teams }));
      if (url.pathname === '/api/projects') return Promise.resolve(json({ projects: [...projects, { ...projects[0]!, id: 'project-2', name: 'Project Two' }] }));
      if (url.searchParams.get('project') === 'project-2') return new Promise<Response>(() => undefined);
      return Promise.resolve(graphFails ? json({ error: { code: 'linear_error', message: 'Linear failed.' } }, 502) : json(graphOne));
    });
    openWithElk('?team=team-1&project=project-1');
    await screen.findByRole('alert');
    await listed('Project Two');
    chooseProject('Project Two');
    graphFails = false;
    chooseProject('Project One');
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
    await listed('Project One');
    const start = window.history.length;
    chooseProject('Project One');
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
    await listed('Project One');
    chooseProject('Project One');
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

describe('App refresh in place', () => {
  const transform = () => document.querySelector<HTMLElement>('.react-flow__viewport')?.style.transform;
  const refreshButton = () => screen.getByRole<HTMLButtonElement>('button', { name: 'Refresh' });
  const wrapper = (identifier: string) => card(identifier)?.closest('.react-flow__node');
  const notice = () => screen.findByRole('status');
  const withoutX = makeGraph(
    graphOne.issues.filter((issue) => issue.id !== 'X'),
    graphOne.relations.filter((relation) => relation.to !== 'X' && relation.from !== 'X'),
    graphOne.external,
  );

  /** Lets React Flow apply the fits it queued for the latest nodes. */
  const settle = () => act(() => new Promise((done) => setTimeout(done, 50)));

  async function openMap(focus = '') {
    open(`?team=team-1&project=project-1${focus}`);
    await mapShown();
    await settle();
  }

  it('re-fetches the Project and keeps the viewport, plates, focus and Show related, marking what changed', async () => {
    // A focus opened from the URL is centred at once, where a clicked one glides there.
    await openMap('&focus=X');
    fireEvent.click(screen.getByRole('button', { name: 'Collapse all' }));
    fireEvent.click(screen.getByRole('checkbox', { name: 'Show related' }));
    await waitFor(() => expect(card('T-A')).toBeNull());
    await waitFor(() => expect(card('T-R')).not.toBeNull());
    await settle();
    const viewport = transform();
    const search = window.location.search;
    graphAnswer = () => json(makeGraph([...graphOne.issues.map((issue) => (issue.id === 'X' ? { ...issue, assignee: 'Bo' } : issue)), makeIssue('Y')], graphOne.relations, graphOne.external));

    fireEvent.click(refreshButton());
    expect(refreshButton().disabled).toBe(true);
    expect((await notice()).textContent).toBe('1 changed · 1 new');
    await waitFor(() => expect(card('T-Y')).not.toBeNull());
    expect(wrapper('T-Y')?.classList.contains('refresh-added')).toBe(true);
    expect(wrapper('T-X')?.classList.contains('refresh-changed')).toBe(true);
    expect(card('T-X')?.textContent).toContain('Bo');
    expect(card('T-X')?.className).toContain('selected');
    expect(card('T-A')).toBeNull();
    expect(card('T-R')).not.toBeNull();
    expect(transform()).toBe(viewport);
    expect(window.location.search).toBe(search);
    expect(refreshButton().disabled).toBe(false);
    expect(screen.getByText('Project One: 5 issues, 1 plates')).toBeTruthy();
  });

  it('fades a removed issue out on the old map before the new one replaces it', async () => {
    await openMap();
    const timers = vi.spyOn(window, 'setTimeout');
    graphAnswer = () => json(withoutX);
    fireEvent.click(refreshButton());
    expect((await notice()).textContent).toBe('1 removed · 1 link removed');
    expect(wrapper('T-X')?.classList.contains('refresh-removed')).toBe(true);
    await waitFor(() => expect(card('T-X')).toBeNull());
    expect(timers).toHaveBeenCalledWith(expect.any(Function), 300);
    timers.mockRestore();
  });

  it('reads No changes and marks nothing when a second refresh finds nothing new', async () => {
    await openMap();
    graphAnswer = () => json(withoutX);
    fireEvent.click(refreshButton());
    await waitFor(() => expect(card('T-X')).toBeNull());
    fireEvent.keyDown(document.body, { key: 'r' });
    await waitFor(() => expect(screen.getByRole('status').textContent).toBe('No changes'));
    await waitFor(() => expect(document.querySelector('.react-flow__node[class*="refresh-"]')).toBeNull());
  });

  it('keeps the map and shows the error when a refresh fails, until one succeeds', async () => {
    await openMap();
    graphAnswer = () => json({ error: { code: 'linear_rate_limited', message: 'Linear is rate limiting this key.' } }, 503);
    fireEvent.click(refreshButton());
    expect((await screen.findByRole('alert')).textContent).toBe('linear_rate_limited Linear is rate limiting this key.');
    expect(card('T-X')).not.toBeNull();
    expect(screen.queryByRole('status')).toBeNull();
    expect(refreshButton().disabled).toBe(false);
    graphAnswer = () => json(graphOne);
    fireEvent.click(refreshButton());
    expect((await notice()).textContent).toBe('No changes');
    expect(screen.queryByRole('alert')).toBeNull();
  });

  it('draws a map a refresh fetched after the first fetch failed, with nothing to compare', async () => {
    graphAnswer = () => json({ error: { code: 'linear_error', message: 'Linear failed.' } }, 502);
    open('?team=team-1&project=project-1');
    await screen.findByRole('alert');
    graphAnswer = () => json(graphOne);
    fireEvent.click(refreshButton());
    await mapShown();
    expect(screen.queryByRole('alert')).toBeNull();
    expect(screen.queryByRole('status')).toBeNull();
  });

  it('stays busy until the refreshed map has landed, so a second Refresh cannot drop the first one\'s marks', async () => {
    await openMap();
    const viewport = transform();
    const placeholders: string[] = [];
    const watch = new MutationObserver(() => {
      const shown = document.querySelector('.placeholder');
      if (shown !== null) placeholders.push(shown.textContent);
    });
    watch.observe(document.body, { childList: true, subtree: true });
    graphAnswer = () => json(makeGraph(withoutX.issues.map((issue) => (issue.id === 'A' ? { ...issue, title: 'Renamed' } : issue)), withoutX.relations, withoutX.external));

    fireEvent.click(refreshButton());
    expect((await notice()).textContent).toBe('1 changed · 1 removed · 1 link removed');
    // The answer is in, but the old map still shows its removal fading while the new one lays out.
    expect(wrapper('T-X')?.classList.contains('refresh-removed')).toBe(true);
    expect(refreshButton().disabled).toBe(true);
    const fetches = vi.mocked(fetch).mock.calls.length;
    fireEvent.keyDown(document.body, { key: 'r' });
    fireEvent.click(refreshButton());
    expect(vi.mocked(fetch).mock.calls.length).toBe(fetches);

    await waitFor(() => expect(card('T-X')).toBeNull());
    expect(wrapper('T-A')?.classList.contains('refresh-changed')).toBe(true);
    expect(screen.getByRole('status').textContent).toBe('1 changed · 1 removed · 1 link removed');
    expect(transform()).toBe(viewport);
    expect(refreshButton().disabled).toBe(false);
    watch.disconnect();
    expect(placeholders).toEqual([]);
  });

  it('cannot refresh while the Project\'s map is first loading', async () => {
    open('?team=team-1&project=project-1');
    expect(refreshButton().disabled).toBe(true);
    await mapShown();
    expect(refreshButton().disabled).toBe(false);
  });

  it('frees Refresh on leaving a Project mid-refresh, and drops that answer on coming back', async () => {
    await openMap();
    const answer = deferred<Response>();
    vi.mocked(fetch).mockImplementationOnce(() => answer.promise);
    fireEvent.click(refreshButton());
    const go = (search: string) =>
      act(() => {
        window.history.pushState(null, '', `/${search}`);
        window.dispatchEvent(new PopStateEvent('popstate'));
      });
    go('?team=team-1&project=project-2');
    await waitFor(() => expect(card('T-E')).not.toBeNull());
    expect(refreshButton().disabled).toBe(false);
    go('?team=team-1&project=project-1');
    await mapShown();
    await act(async () => {
      answer.resolve(json(withoutX));
      await new Promise((settle) => setTimeout(settle, 20));
    });
    expect(card('T-X')).not.toBeNull();
    expect(screen.queryByRole('status')).toBeNull();
  });

  it('cannot refresh before a Project is chosen', async () => {
    open('?team=team-1');
    await listed('Project One');
    expect(refreshButton().disabled).toBe(true);
  });

  it('drops a refresh answer for a Project it has left', async () => {
    await openMap();
    const answer = deferred<Response>();
    graphAnswer = () => json(withoutX);
    vi.mocked(fetch).mockImplementationOnce(() => answer.promise);
    fireEvent.click(refreshButton());
    act(() => {
      window.history.pushState(null, '', '/?team=team-1&project=project-2');
      window.dispatchEvent(new PopStateEvent('popstate'));
    });
    await waitFor(() => expect(card('T-E')).not.toBeNull());
    await act(async () => {
      answer.resolve(json(withoutX));
      await new Promise((settle) => setTimeout(settle, 20));
    });
    expect(screen.getByText('Project Two: 1 issues, 0 plates')).toBeTruthy();
    expect(screen.queryByRole('status')).toBeNull();
  });

  it('with reduced motion swaps the map at once and rings what changed without old looks', async () => {
    vi.stubGlobal('matchMedia', (query: string) => ({ matches: query === '(prefers-reduced-motion: reduce)', addEventListener: () => undefined, removeEventListener: () => undefined }));
    await openMap();
    const timers = vi.spyOn(window, 'setTimeout');
    graphAnswer = () => json(makeGraph(withoutX.issues.map((issue) => (issue.id === 'A' ? { ...issue, title: 'Renamed' } : issue)), withoutX.relations, withoutX.external));
    fireEvent.click(refreshButton());
    await waitFor(() => expect(card('T-X')).toBeNull());
    expect(wrapper('T-A')?.querySelector('.refresh-ring')).not.toBeNull();
    expect(document.querySelector('.refresh-ghost')).toBeNull();
    expect(document.querySelector('.refresh-glide')).toBeNull();
    expect(timers).not.toHaveBeenCalledWith(expect.any(Function), 300);
    timers.mockRestore();
  });
});
