import { spawn, type ChildProcessWithoutNullStreams } from 'node:child_process';
import { once } from 'node:events';
import { resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { test as base, expect, type Page } from '@playwright/test';
import { startFakeLinear, type FakeLinear } from './fake-linear/server';
import { node } from './serveFixtures';

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
    const map = startMap({ LINEAR_API_KEY: API_KEY, DYDO_LINEAR_ENDPOINT: linear.url });
    const exited = once(map, 'exit');
    try {
      await provide(await servingUrl(map));
    } finally {
      map.kill();
      await exited;
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
  await page.getByRole('combobox').nth(1).selectOption({ label: 'Project map · In Progress' });
  await expect.poll(() => search(page).get('project')).toBe('project-map');
  await expect(node(page, 'DYD-1')).toBeVisible();
  await expect(node(page, 'DYD-3')).toBeVisible();
  await expect(node(page, 'DYD-2').locator('.external-project')).toHaveText('Release');
  await expect(page.locator('.edge-blocks')).toHaveCount(2);
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
  await expect(node(page, 'DYD-1').locator('.assignee')).toHaveText('Grace');
});

base('without LINEAR_API_KEY dydo map exits with the key help', async () => {
  const map = startMap({});
  let stderr = '';
  map.stderr.on('data', (chunk) => (stderr += String(chunk)));
  const [code] = (await once(map, 'close')) as [number | null];
  expect(code).toBe(2);
  expect(stderr).toContain('LINEAR_API_KEY is not set, so dydo map cannot read Linear.');
});
