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

  it('shows a failed team list', async () => {
    vi.stubGlobal('fetch', vi.fn(() => Promise.resolve(json({ error: { code: 'linear_auth', message: 'Key rejected.' } }, 502))));
    open('?team=team-1');
    expect((await screen.findByRole('alert')).textContent).toBe('linear_auth Key rejected.');
  });

  it('shows a layout failure', async () => {
    const broken = { layout: () => Promise.reject(new Error('ELK exploded')) } as unknown as ElkApi;
    window.history.replaceState(null, '', '/?team=team-1&project=project-1');
    render(<App elk={broken} />);
    expect((await screen.findByRole('alert')).textContent).toBe('viewer_error ELK exploded');
  });
});
