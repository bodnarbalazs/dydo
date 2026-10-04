import { expect, test, type Page } from '@playwright/test';
import type { Graph } from '../src/api/types';
import { fixture, mapUrl, node, SCENARIO_PROJECT, serveFixtures } from './serveFixtures';

const saved = fixture<Graph>('graph-scenario.json');
const stamp = '2026-10-04T12:00:00Z';
const refresh = (page: Page) => page.getByRole('button', { name: 'Refresh' });

async function revisit(page: Page, fresh = 'graph-scenario-refreshed.json') {
  const served = await serveFixtures(page);
  served.graphs[SCENARIO_PROJECT] = fresh;
  await page.route('**/api/saved?*', (route) => route.fulfill({ json: { snapshot: { graph: saved, fetchedAt: stamp } } }));
  let release!: () => void;
  const held = new Promise<void>((done) => { release = done; });
  await page.route('**/api/graph?*', async (route) => { await held; await route.fallback(); });
  await page.clock.install();
  await page.goto(mapUrl(SCENARIO_PROJECT));
  await expect(node(page, 'DYD-9001')).toBeVisible();
  await expect(page.getByText(/Saved map/)).toContainText(stamp);
  await page.clock.runFor(64);
  const response = page.waitForResponse((answer) => answer.url().includes('/api/graph?'));
  release(); await response;
  return served;
}

test('changed saved revisit shows the old map before the existing refresh cascade', async ({ page }) => {
  await revisit(page);
  await expect(refresh(page)).toBeDisabled();
  await expect(page.locator('.react-flow__node[class*="refresh-"]')).toHaveCount(0);
  await page.clock.fastForward(2500);
  await expect(node(page, 'DYD-290')).toHaveClass(/refresh-added/);
  await expect(node(page, 'DYD-9001')).toHaveCount(0);
  await expect(page.getByText(/Saved map/)).toHaveCount(0);
  await expect(refresh(page)).toBeEnabled();
});

test('unchanged saved revisit settles quietly', async ({ page }) => {
  await revisit(page, 'graph-scenario.json');
  await page.clock.fastForward(2500);
  await expect(page.getByRole('status')).toHaveText('No changes');
  await expect(page.locator('.react-flow__node[class*="refresh-"]')).toHaveCount(0);
  await expect(refresh(page)).toBeEnabled();
});

test('failed fresh fetch retains saved timestamp and map, and Refresh recovers', async ({ page }) => {
  const served = await revisit(page, 'error-linear-auth.json');
  await expect(page.getByRole('alert')).toContainText('Failed to fetch fresh map.');
  await expect(refresh(page)).toBeEnabled();
  await expect(page.getByText(/Saved map/)).toContainText(stamp);
  served.graphs[SCENARIO_PROJECT] = 'graph-scenario-refreshed.json';
  await refresh(page).click();
  await page.clock.fastForward(2500);
  await expect(node(page, 'DYD-290')).toBeVisible();
  await expect(page.getByRole('alert')).toHaveCount(0);
  await expect(page.getByText(/Saved map/)).toHaveCount(0);
});

test('reduced motion keeps the saved viewing window and applies without motion', async ({ page }) => {
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await revisit(page);
  await expect(node(page, 'DYD-9001')).toBeVisible();
  await expect(node(page, 'DYD-290')).toHaveCount(0);
  await page.clock.fastForward(2500);
  await expect(node(page, 'DYD-290')).toBeVisible();
  await expect(page.locator('.refresh-ghost, .refresh-glide')).toHaveCount(0);
  await expect(node(page, 'DYD-290')).toHaveCSS('opacity', '1');
});

test('saved lookup settles before fresh starts, then old map completes its viewing window before cascade', async ({ page }) => {
  const served = await serveFixtures(page);
  served.graphs[SCENARIO_PROJECT] = 'graph-scenario-refreshed.json';
  let release!: () => void;
  const held = new Promise<void>((done) => { release = done; });
  await page.route('**/api/saved?*', async (route) => { await held; await route.fulfill({ json: { snapshot: { graph: saved, fetchedAt: stamp } } }); });
  await page.clock.install();
  await page.clock.pauseAt(new Date());
  const freshRequests: string[] = [];
  page.on('request', (request) => { if (request.url().includes('/api/graph?')) freshRequests.push(request.url()); });
  await page.goto(mapUrl(SCENARIO_PROJECT));
  await page.clock.runFor(64);
  expect(freshRequests).toHaveLength(0);
  await expect(node(page, 'DYD-290')).toHaveCount(0);
  await expect(refresh(page)).toBeDisabled();
  const response = page.waitForResponse((answer) => answer.url().includes('/api/saved?'));
  const freshResponse = page.waitForResponse((answer) => answer.url().includes('/api/graph?'));
  release(); await response; await freshResponse;
  await expect.poll(async () => {
    await page.clock.runFor(16);
    return node(page, 'DYD-9001').isVisible();
  }).toBe(true);
  await expect(page.getByText(/Saved map/)).toContainText(stamp);
  await page.clock.runFor(64);
  await page.clock.runFor(1900);
  await expect(node(page, 'DYD-9001')).toBeVisible();
  await expect(node(page, 'DYD-290')).toHaveCount(0);
  await expect(page.getByText(/Saved map/)).toContainText(stamp);
  await expect(refresh(page)).toBeDisabled();
  await page.clock.runFor(600);
  await page.clock.resume();
  await expect(node(page, 'DYD-290')).toHaveClass(/refresh-added/);
  await expect(node(page, 'DYD-9001')).toHaveCount(0);
  await expect(page.getByText(/Saved map/)).toHaveCount(0);
  await expect(refresh(page)).toBeEnabled();
});

const empty: Graph = { ...saved, issues: [], external: [], relations: [] };

for (const populated of [false, true]) {
  test(`empty saved map finishes its viewing window with ${populated ? 'populated' : 'empty'} fresh data`, async ({ page }) => {
    await serveFixtures(page);
    await page.route('**/api/saved?*', (route) => route.fulfill({ json: { snapshot: { graph: empty, fetchedAt: stamp } } }));
    let release!: () => void;
    const held = new Promise<void>((done) => { release = done; });
    await page.route('**/api/graph?*', async (route) => { await held; await route.fulfill({ json: populated ? saved : empty }); });
    await page.clock.install();
    await page.clock.pauseAt(new Date());
    await page.goto(mapUrl(SCENARIO_PROJECT));
    await expect.poll(async () => {
      await page.clock.runFor(16);
      return page.locator('.react-flow').isVisible();
    }).toBe(true);
    await expect(page.getByText(/Saved map/)).toContainText(stamp);
    await page.clock.runFor(64);
    const response = page.waitForResponse((answer) => answer.url().includes('/api/graph?'));
    release(); await response;
    await page.clock.runFor(1900);
    await expect(page.getByText(/Saved map/)).toContainText(stamp);
    await expect(refresh(page)).toBeDisabled();
    await expect(page.locator('.react-flow__node')).toHaveCount(0);
    await page.clock.runFor(600);
    await expect(page.getByText(/Saved map/)).toHaveCount(0);
    await page.clock.resume();
    await expect(refresh(page)).toBeEnabled();
    if (populated) await expect(node(page, 'DYD-9001')).toBeInViewport();
    else await expect(page.getByRole('status')).toHaveText('No changes');
  });
}

test('an empty first visit enables Refresh after paint without a saved viewing window', async ({ page }) => {
  await serveFixtures(page);
  await page.route('**/api/graph?*', (route) => route.fulfill({ json: empty }));
  await page.clock.install();
  await page.clock.pauseAt(new Date());
  await page.goto(mapUrl(SCENARIO_PROJECT));
  await expect.poll(async () => {
    await page.clock.runFor(16);
    return page.locator('.react-flow').isVisible();
  }).toBe(true);
  await page.clock.runFor(64);
  await expect(refresh(page)).toBeEnabled();
  await expect(page.getByText(/Saved map/)).toHaveCount(0);
  await expect(page.locator('.react-flow__node')).toHaveCount(0);
});
