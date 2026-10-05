import { spawn, type ChildProcessWithoutNullStreams } from 'node:child_process';
import { once } from 'node:events';
import { resolve, join } from 'node:path';
import { mkdtemp, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { fileURLToPath } from 'node:url';
import { test as base, expect, type Page } from '@playwright/test';
import { startFakeLinear, type FakeLinear } from './fake-linear/server';
import { node } from './serveFixtures';
import type { Graph } from '../src/api/types';

// The real `dydo map` binary, published by `dotnet publish DynaDocs.csproj -r <rid> -o artifacts/map-e2e`.
const VIEWER = fileURLToPath(new URL('..', import.meta.url));
const BINARY = resolve(VIEWER, process.env['DYDO_E2E_BIN'] ?? '../artifacts/map-e2e/dydo');
const API_KEY = 'lin_api_fake';

function startMap(env: Record<string, string>): ChildProcessWithoutNullStreams {
  const inherited = { ...process.env };
  delete inherited['LINEAR_API_KEY'];
  return spawn(BINARY, ['map', '--no-browser'], { env: { ...inherited, ...env } });
}

/** The URL `dydo map` prints once it serves; a map that exits first fails with its stderr. */
function servingUrl(map: ChildProcessWithoutNullStreams): Promise<string> {
  return new Promise((resolveUrl, reject) => {
    let stdout = '';
    let stderr = '';
    map.stdout.on('data', (chunk) => {
      stdout += String(chunk);
      const url = /serving (\S+)/.exec(stdout)?.[1];
      if (url !== undefined) resolveUrl(url);
    });
    map.stderr.on('data', (chunk) => (stderr += String(chunk)));
    map.once('exit', (code) => reject(new Error(`dydo map exited ${String(code)}: ${stderr}`)));
  });
}

const test = base.extend<{ linear: FakeLinear; map: string }>({
  // Playwright requires the first fixture argument to be an object destructuring
  // pattern (a rest-only pattern is rejected too), but `linear` has no fixture
  // dependencies of its own. `browserName` is a plain string read from worker
  // config, not something that launches a browser or page, so naming it here
  // costs nothing and keeps the pattern non-empty for `no-empty-pattern`; `void`
  // marks it deliberately unused for `no-unused-vars`.
  linear: async ({ browserName }, provide) => {
    void browserName;
    const linear = await startFakeLinear(API_KEY);
    await provide(linear);
    await linear.close();
  },
  map: async ({ linear }, provide) => {
    const cache = await mkdtemp(join(tmpdir(), 'dydo-map-e2e-'));
    const map = startMap({ LINEAR_API_KEY: API_KEY, DYDO_LINEAR_ENDPOINT: linear.url, DYDO_MAP_CACHE_DIR: cache });
    const exited = once(map, 'exit');
    try {
      await provide(await servingUrl(map));
    } finally {
      map.kill();
      await exited;
      await rm(cache, { recursive: true, force: true });
    }
  },
});

function search(page: Page): URLSearchParams {
  return new URL(page.url()).searchParams;
}

async function openProjectMap(page: Page, map: string) {
  await page.goto(`${map}?team=team-dyd&project=project-map`);
  await expect(node(page, 'DYD-1')).toBeVisible();
}

test('a team and a Project are chosen and their graph is drawn from Linear', async ({ page, map }) => {
  await page.goto(map);
  await page.getByRole('combobox').first().selectOption({ label: 'Dydo (DYD)' });
  await expect.poll(() => search(page).get('team')).toBe('team-dyd');
  await page.getByRole('button', { name: 'Project Choose a Project' }).click();
  await expect(page.getByRole('option', { name: /^Project map/ })).toContainText('Oct 3');
  await page.getByRole('option', { name: /^Project map/ }).click();
  await expect(page.getByRole('button', { name: 'Project Project map' })).toBeVisible();
  await expect.poll(() => search(page).get('project')).toBe('project-map');
  await expect(node(page, 'DYD-1')).toBeVisible();
  await expect(node(page, 'DYD-3')).toBeVisible();
  await expect(node(page, 'DYD-2').locator('.external-project')).toHaveText('Release');
  await expect(page.locator('.edge-blocks')).toHaveCount(2);
});

test('labels come from Linear in its colours, by name, beside the identifier link', async ({ page, map, linear }) => {
  await openProjectMap(page, map);
  const card = node(page, 'DYD-1');
  await expect(card.locator('.label-chip')).toHaveText(['AFK', 'Feature']);
  await expect(card.locator('.label-dot').first()).toHaveCSS('background-color', 'rgb(48, 164, 108)');
  await expect(card.getByRole('link', { name: 'Open DYD-1 in Linear' })).toHaveText('DYD-1 ↗');
  await expect(node(page, 'DYD-2').locator('.label-chip')).toHaveText(['HITL']);
  await expect(node(page, 'DYD-3').locator('.label-chip')).toHaveCount(0);

  const drawTheMap = linear.workspace.issues.find((issue) => issue.identifier === 'DYD-1');
  if (drawTheMap === undefined) throw new Error('no DYD-1 in the fake workspace');
  drawTheMap.labels = [];
  await page.getByRole('button', { name: 'Refresh' }).click();
  await expect(page.getByRole('status')).toHaveText('1 changed');
  await expect(card.locator('.label-chip')).toHaveCount(0);
});

test('a blocker in another Project links to Linear and opens its Project, focused', async ({ page, map }) => {
  await openProjectMap(page, map);
  const external = node(page, 'DYD-2');
  await expect(external.getByRole('link', { name: 'Open DYD-2 in Linear' })).toHaveAttribute('href', 'https://linear.app/fake/issue/DYD-2');
  await external.locator('.title').click();
  await expect.poll(() => search(page).get('project')).toBe('project-release');
  expect(search(page).get('team')).toBe('team-dyd');
  expect(search(page).get('focus')).toBe('issue-DYD-2');
  await expect(node(page, 'DYD-2')).toHaveClass(/selected/);
  await expect(node(page, 'DYD-2').locator('.issue-card')).toBeVisible();
});

test('a reload shows what Linear answers now (AC5)', async ({ page, map, linear }) => {
  await openProjectMap(page, map);
  await expect(node(page, 'DYD-1').locator('.assignee')).toHaveText('unassigned');
  const drawTheMap = linear.workspace.issues.find((issue) => issue.identifier === 'DYD-1');
  if (drawTheMap === undefined) throw new Error('no DYD-1 in the fake workspace');
  drawTheMap.assignee = 'Grace';
  await page.reload();
  await expect(node(page, 'DYD-1').locator(':scope > .issue-card .assignee')).toHaveText('Grace');
});

base('without LINEAR_API_KEY dydo map exits with the key help', async () => {
  const map = startMap({});
  let stderr = '';
  map.stderr.on('data', (chunk) => (stderr += String(chunk)));
  const [code] = (await once(map, 'close')) as [number | null];
  expect(code).toBe(2);
  expect(stderr).toContain('LINEAR_API_KEY is not set, so dydo map cannot read Linear.');
});

test('the embedded bundle serves the favicon index.html links', async ({ page, map }) => {
  await page.goto(map);
  const href = await page.locator('link[rel="icon"]').getAttribute('href');
  const response = await page.request.get(new URL(href ?? '', page.url()).toString());
  expect(response.status()).toBe(200);
  expect(response.headers()['content-type']).toContain('image/svg+xml');
  expect(await response.text()).toContain('<svg');
});


test('actual process restart on a different port retains the saved map while fresh Linear is blocked', async ({ page, linear }) => {
  const cache = await mkdtemp(join(tmpdir(), 'dydo-map-restart-'));
  const env = { LINEAR_API_KEY: API_KEY, DYDO_LINEAR_ENDPOINT: linear.url, DYDO_MAP_CACHE_DIR: cache };
  let map = startMap(env);
  let exited = once(map, 'exit');
  try {
    const first = await servingUrl(map);
    await openProjectMap(page, first);
    await expect(node(page, 'DYD-1').locator('.assignee')).toHaveText('unassigned');
    map.kill(); await exited;
    const issue = linear.workspace.issues.find((item) => item.identifier === 'DYD-1');
    if (issue === undefined) throw new Error('missing DYD-1');
    issue.assignee = 'After restart';
    map = startMap(env); exited = once(map, 'exit');
    const second = await servingUrl(map);
    expect(new URL(second).port).not.toBe(new URL(first).port);
    let resume!: () => void;
    const held = new Promise<void>((done) => { resume = done; });
    await page.route('**/api/graph?*', async (route) => { await held; await route.continue(); });
    await page.goto(`${second}?team=team-dyd&project=project-map`);
    await expect(page.getByText(/Saved map/)).toBeVisible();
    await expect(node(page, 'DYD-1').locator('.assignee')).toHaveText('unassigned');
    resume();
    await expect(node(page, 'DYD-1').locator('.assignee').first()).toHaveText('After restart');
    await expect(page.getByText(/Saved map/)).toHaveCount(0);
  } finally {
    map.kill(); await exited;
    await rm(cache, { recursive: true, force: true });
  }
});


test('saved lookup captures the old server snapshot before a fast fresh fetch replaces it', async ({ page, map, linear }) => {
  await openProjectMap(page, map);
  await expect(node(page, 'DYD-1').locator('.assignee')).toHaveText('unassigned');
  const previous = await (await page.request.get(`${map}api/saved?project=project-map`)).json() as { snapshot: { fetchedAt: string } };
  const issue = linear.workspace.issues.find((item) => item.identifier === 'DYD-1');
  if (issue === undefined) throw new Error('missing DYD-1');
  issue.assignee = 'Freshly persisted';
  let release!: () => void;
  const held = new Promise<void>((done) => { release = done; });
  await page.route('**/api/saved?*', async (route) => { await held; await route.continue(); });
  const freshRequests: string[] = [];
  page.on('request', (request) => { if (request.url().includes('/api/graph?')) freshRequests.push(request.url()); });
  await page.clock.install();
  await page.clock.pauseAt(new Date());
  await page.reload();
  await page.clock.runFor(64);
  expect(freshRequests).toHaveLength(0);
  const freshResponse = page.waitForResponse((answer) => answer.url().includes('/api/graph?'));
  release(); await freshResponse;
  const persisted = await (await page.request.get(`${map}api/saved?project=project-map`)).json() as { snapshot: { graph: Graph } };
  expect(persisted.snapshot.graph.issues.find((item) => item.identifier === 'DYD-1')?.assignee).toBe('Freshly persisted');
  await expect.poll(async () => {
    await page.clock.runFor(16);
    return node(page, 'DYD-1').isVisible();
  }).toBe(true);
  await expect(node(page, 'DYD-1').locator('.assignee')).toHaveText('unassigned');
  await expect(page.getByText(/Saved map/)).toContainText(previous.snapshot.fetchedAt);
  await page.clock.runFor(1900);
  await expect(node(page, 'DYD-1').locator('.assignee')).toHaveText('unassigned');
  await expect(page.getByRole('button', { name: 'Refresh' })).toBeDisabled();
  await page.clock.runFor(600);
  await page.clock.resume();
  await expect(node(page, 'DYD-1').locator('.assignee').first()).toHaveText('Freshly persisted');
  await expect(page.getByText(/Saved map/)).toHaveCount(0);
});
