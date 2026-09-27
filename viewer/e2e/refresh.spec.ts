import { expect, test, type Page } from '@playwright/test';
import type { Graph } from '../src/api/types';
import { DYDO_TEAM, fixture, issueId, mapUrl, node, SCENARIO_PROJECT, serveFixtures } from './serveFixtures';

const before = fixture<Graph>('graph-scenario.json');
const after = fixture<Graph>('graph-scenario-refreshed.json');
const CHANGED = ['DYD-263', 'DYD-264', 'DYD-265', 'DYD-266', 'DYD-268'];
const ADDED = ['DYD-290', 'DYD-291'];
const REMOVED = ['DYD-262', 'DYD-9001'];

const viewport = (page: Page) => page.locator('.react-flow__viewport').evaluate((element) => (element as HTMLElement).style.transform);
const refresh = (page: Page) => page.getByRole('button', { name: 'Refresh' });
const notice = (page: Page) => page.getByRole('status');
const blocking = (page: Page, graph: Graph, from: string, to: string) =>
  page.locator(`.react-flow__edge[data-id="blocks:${issueId(graph, from)}->${issueId(graph, to)}"]`);

/** Opens fixture A focused on DYD-269 and lets the focus settle, then serves fixture B. */
async function openA(page: Page) {
  const served = await serveFixtures(page);
  await page.goto(mapUrl(SCENARIO_PROJECT, issueId(before, 'DYD-269')));
  await expect(node(page, 'DYD-9001')).toBeVisible();
  await expect.poll(() => viewport(page)).not.toBe('');
  served.graphs[SCENARIO_PROJECT] = 'graph-scenario-refreshed.json';
  return served;
}

test('Refresh swaps in the new graph in place and cascades what changed, then finds no changes', async ({ page }) => {
  await openA(page);
  await page.getByRole('checkbox', { name: 'Show related' }).check();
  await expect(page.locator('.edge-related').first()).toBeAttached();
  const shown = await viewport(page);
  const url = page.url();

  await refresh(page).click();
  await expect(notice(page)).toHaveText('5 changed · 2 new · 2 removed · 2 links added · 1 link removed');
  for (const identifier of REMOVED) await expect(node(page, identifier)).toHaveCount(0);
  for (const identifier of ADDED) await expect(node(page, identifier)).toHaveClass(/refresh-added/);
  for (const identifier of CHANGED) await expect(node(page, identifier)).toHaveClass(/refresh-changed/);
  await expect(node(page, 'DYD-265').locator('.plate-header .state-name').first()).toHaveText('In Review');
  await expect(node(page, 'DYD-268').locator('.pickable-badge')).toHaveCount(0);
  await expect(node(page, 'DYD-268').locator('.assignee').first()).toHaveText('Balazs');
  await expect(blocking(page, after, 'DYD-290', 'DYD-291')).toHaveClass(/refresh-added/);
  await expect(blocking(page, before, 'DYD-270', 'DYD-266')).toHaveCount(0);

  // One after another, in reading order: each turn a stagger after the last.
  const turns = await page
    .locator('.react-flow__node.refresh-changed, .react-flow__node.refresh-added')
    .evaluateAll((nodes) => nodes.map((element) => Number.parseInt((element as HTMLElement).style.getPropertyValue('--refresh-delay'), 10)).sort((a, b) => a - b));
  expect(turns).toHaveLength(CHANGED.length + ADDED.length);
  turns.slice(1).forEach((turn, index) => expect(turn - (turns[index] ?? 0)).toBe(150));

  expect(await viewport(page)).toBe(shown);
  expect(page.url()).toBe(url);
  await expect(node(page, 'DYD-269')).toHaveClass(/selected/);
  await expect(page.getByRole('checkbox', { name: 'Show related' })).toBeChecked();
  await expect(page.locator('.edge-related').first()).toBeAttached();

  await page.keyboard.press('r');
  await expect(notice(page)).toHaveText('No changes');
  await expect(page.locator('.react-flow__node[class*="refresh-"]')).toHaveCount(0);
  expect(await viewport(page)).toBe(shown);
});

test('with reduced motion a refresh applies at once and rings what changed', async ({ page }) => {
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await openA(page);
  await refresh(page).click();
  await expect(node(page, 'DYD-290')).toBeVisible();
  await expect(node(page, 'DYD-9001')).toHaveCount(0);
  await expect(page.locator('.refresh-ghost')).toHaveCount(0);
  const rings = page.locator('.refresh-ring');
  await expect(rings).toHaveCount(CHANGED.length + ADDED.length);
  expect(await rings.evaluateAll((all) => all.map((ring) => getComputedStyle(ring).opacity))).toEqual(Array(CHANGED.length + ADDED.length).fill('1'));
  await expect(node(page, 'DYD-290')).toHaveCSS('opacity', '1');
});

test('a failed refresh keeps the map and shows the error', async ({ page }) => {
  const served = await openA(page);
  served.graphs[SCENARIO_PROJECT] = 'error-linear-auth.json';
  await refresh(page).click();
  await expect(page.getByRole('alert')).toHaveText('linear_auth Linear rejected the API key: Authentication required, not authenticated.');
  await expect(node(page, 'DYD-9001')).toBeVisible();
  await expect(refresh(page)).toBeEnabled();
});

test('Refresh waits for a Project', async ({ page }) => {
  await serveFixtures(page);
  await page.goto(`/?team=${DYDO_TEAM}`);
  await expect(refresh(page)).toBeDisabled();
});
