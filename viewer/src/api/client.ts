import type { Graph, Project, Team } from './types';

export class ApiError extends Error {
  readonly code: string;

  constructor(code: string, message: string) {
    super(message);
    this.code = code;
  }
}

export async function fetchTeams(): Promise<Team[]> {
  return (await getJson<{ teams: Team[] }>('/api/teams')).teams;
}

export async function fetchProjects(teamId: string): Promise<Project[]> {
  return (await getJson<{ projects: Project[] }>(`/api/projects?team=${encodeURIComponent(teamId)}`)).projects;
}

export function fetchGraph(projectId: string): Promise<Graph> {
  return getJson<Graph>(`/api/graph?project=${encodeURIComponent(projectId)}`);
}

async function getJson<T>(path: string): Promise<T> {
  const response = await fetch(path, { cache: 'no-store' });
  const body: unknown = await response.json().catch(() => null);
  if (response.ok && body !== null) return body as T;
  if (isErrorEnvelope(body)) throw new ApiError(body.error.code, body.error.message);
  throw new ApiError(`http_${response.status}`, `The map server answered ${response.status} ${response.statusText} without an error body.`);
}

function isErrorEnvelope(body: unknown): body is { error: { code: string; message: string } } {
  if (typeof body !== 'object' || body === null || !('error' in body)) return false;
  const error: unknown = body.error;
  return typeof error === 'object' && error !== null && 'code' in error && 'message' in error
    && typeof error.code === 'string' && typeof error.message === 'string';
}
