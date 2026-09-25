import { readFileSync } from 'node:fs';
import type { Page, Route } from '@playwright/test';
import type { Graph } from '../src/api/types';

const FIXTURES = new URL('../fixtures/', import.meta.url);

export function fixture<T>(name: string): T {
  return JSON.parse(readFileSync(new URL(name, FIXTURES), 'utf-8')) as T;
}

export const DYDO_TEAM = 'caa6ccbf-4f9b-477e-826c-a51ed43b0687';
export const LARGE_PROJECT = '2ecbc168-2a42-482d-9b1b-a29537985ca5';
export const SCENARIO_PROJECT = '19bea6b6-c574-4b40-896e-bdb94d5cea9c';

export interface Served {
  /** The fixture served for a Project id; change it between reloads. An `error-` file answers 502. */
  graphs: Record<string, string>;
}

/** Serves the committed fixtures as the `dydo map` HTTP API. */
export async function serveFixtures(page: Page): Promise<Served> {
  const served: Served = { graphs: { [LARGE_PROJECT]: 'graph-large.json', [SCENARIO_PROJECT]: 'graph-scenario.json' } };
  const json = (route: Route, status: number, body: unknown) =>
    route.fulfill({ status, contentType: 'application/json; charset=utf-8', body: JSON.stringify(body) });
  const notFound = (route: Route) => json(route, 404, { error: { code: 'not_found', message: 'No such fixture.' } });

  await page.route('**/api/teams', (route) => json(route, 200, fixture('teams.json')));
  await page.route('**/api/projects?*', (route) => {
    const team = new URL(route.request().url()).searchParams.get('team');
    return team === DYDO_TEAM ? json(route, 200, fixture('projects-dydo.json')) : json(route, 200, { projects: [] });
  });
  await page.route('**/api/graph?*', (route) => {
    const file = served.graphs[new URL(route.request().url()).searchParams.get('project') ?? ''];
    if (file === undefined) return notFound(route);
    return file.startsWith('error-') ? json(route, 502, fixture(file)) : json(route, 200, fixture<Graph>(file));
  });
  return served;
}

export function mapUrl(project: string, focus?: string): string {
  return `/?team=${DYDO_TEAM}&project=${project}${focus === undefined ? '' : `&focus=${focus}`}`;
}

/** The node drawn for an issue: a card, a plate header or an external card. */
export function node(page: Page, identifier: string) {
  return page.locator(`.react-flow__node:has(> [data-identifier="${identifier}"])`);
}

export function issueId(graph: Graph, identifier: string): string {
  const issue = [...graph.issues, ...graph.external].find((candidate) => candidate.identifier === identifier);
  if (issue === undefined) throw new Error(`no ${identifier} in the fixture`);
  return issue.id;
}
