import { afterEach, describe, expect, it, vi } from 'vitest';
import { makeGraph } from '../graph/testIssues';
import { ApiError, fetchGraph, fetchProjects, fetchTeams } from './client';

function answer(status: number, body: string) {
  const fetchMock = vi.fn(() => Promise.resolve(new Response(body, { status, statusText: status === 200 ? 'OK' : 'Bad Gateway' })));
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('api client', () => {
  it('reads teams', async () => {
    const fetchMock = answer(200, JSON.stringify({ teams: [{ id: 't', key: 'T', name: 'Team' }] }));
    expect(await fetchTeams()).toEqual([{ id: 't', key: 'T', name: 'Team' }]);
    expect(fetchMock).toHaveBeenCalledWith('/api/teams', expect.objectContaining({ cache: 'no-store' }));
  });

  it('reads the projects of a team, escaping the id', async () => {
    const fetchMock = answer(200, JSON.stringify({ projects: [] }));
    expect(await fetchProjects('a&b')).toEqual([]);
    expect(fetchMock).toHaveBeenCalledWith('/api/projects?team=a%26b', expect.anything());
  });

  it('reads a graph', async () => {
    const fetchMock = answer(200, JSON.stringify(makeGraph([])));
    expect((await fetchGraph('p1')).project.id).toBe('project-1');
    expect(fetchMock).toHaveBeenCalledWith('/api/graph?project=p1', expect.anything());
  });

  it('escapes the project id', async () => {
    const fetchMock = answer(200, JSON.stringify(makeGraph([])));
    await fetchGraph('a&b');
    expect(fetchMock).toHaveBeenCalledWith('/api/graph?project=a%26b', expect.anything());
  });

  it('reports a successful answer without a JSON body by its HTTP status', async () => {
    answer(200, '<html>');
    await expect(fetchTeams()).rejects.toMatchObject({ code: 'http_200' });
  });

  it('tells the user what the server answered when it sent no error body', async () => {
    answer(502, '<html>');
    await expect(fetchTeams()).rejects.toMatchObject({ message: 'The map server answered 502 Bad Gateway without an error body.' });
  });

  it('raises the error envelope as an ApiError', async () => {
    answer(502, JSON.stringify({ error: { code: 'linear_auth', message: 'Linear rejected the key.' } }));
    await expect(fetchGraph('p1')).rejects.toEqual(new ApiError('linear_auth', 'Linear rejected the key.'));
    await expect(fetchGraph('p1')).rejects.toMatchObject({ code: 'linear_auth' });
  });

  it.each([
    ['a body that is not JSON', '<html>'],
    ['a body without an envelope', '{"oops":1}'],
    ['a malformed envelope', '{"error":{"code":7}}'],
    ['a null error', '{"error":null}'],
    ['a JSON string', '"oops"'],
    ['an error that is a string', '{"error":"boom"}'],
    ['an error code that is not a string', '{"error":{"code":7,"message":"m"}}'],
    ['an error message that is not a string', '{"error":{"code":"c","message":7}}'],
  ])('reports %s by its HTTP status', async (_, body) => {
    answer(502, body);
    await expect(fetchTeams()).rejects.toMatchObject({ code: 'http_502' });
  });
});
