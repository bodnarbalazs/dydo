import { expect, test, type Page } from '@playwright/test';
import type { Graph } from '../src/api/types';
import { DYDO_TEAM, fixture, issueId, LARGE_PROJECT, mapUrl, node, SCENARIO_PROJECT, serveFixtures } from './serveFixtures';

const scenario = fixture<Graph>('graph-scenario.json');
const large = fixture<Graph>('graph-large.json');

async function openScenario(page: Page) {
  const served = await serveFixtures(page);
  await page.goto(mapUrl(SCENARIO_PROJECT));
  await expect(node(page, 'DYD-268')).toBeVisible();
  return served;
}

function search(page: Page): URLSearchParams {
  return new URL(page.url()).searchParams;
}

async function expectCentred(page: Page, identifier: string) {
  const pane = await page.locator('.react-flow').boundingBox();
  const box = await node(page, identifier).boundingBox();
  if (pane === null || box === null) throw new Error(`${identifier} or the map is not on screen`);
  expect(Math.abs(box.x + box.width / 2 - (pane.x + pane.width / 2))).toBeLessThan(40);
  expect(Math.abs(box.y + box.height / 2 - (pane.y + pane.height / 2))).toBeLessThan(40);
}

test('selectors write team and Project into the URL and draw the map', async ({ page }) => {
  await serveFixtures(page);
  await page.goto('/');
  await page.getByRole('combobox').first().selectOption({ label: 'Dydo (DYD)' });
  await expect.poll(() => search(page).get('team')).toBe(DYDO_TEAM);
  await page.getByRole('combobox').nth(1).selectOption({ label: 'Visual Linear project map · In Progress' });
  await expect.poll(() => search(page).get('project')).toBe(SCENARIO_PROJECT);
  await expect(node(page, 'DYD-265')).toBeVisible();
  await page.screenshot({ path: 'e2e/screenshots/scenario.png' });
});

test.describe('pickable marker (AC2)', () => {
  test('marks the unassigned, unblocked Todo only', async ({ page }) => {
    await openScenario(page);
    await expect(page.locator('.pickable-badge')).toHaveCount(1);
    await expect(node(page, 'DYD-268').locator('.pickable-badge')).toHaveText('Pickable');
    await expect(node(page, 'DYD-9001').locator('.assignee')).toHaveText('Balazs');
    await expect(node(page, 'DYD-270').locator('.pickable-badge')).toHaveCount(0);
  });

  for (const variant of ['graph-scenario-assigned.json', 'graph-scenario-blocked.json']) {
    test(`loses the marker after a reload serving ${variant}`, async ({ page }) => {
      const served = await openScenario(page);
      served.graphs[SCENARIO_PROJECT] = variant;
      await page.reload();
      await expect(node(page, 'DYD-268')).toBeVisible();
      await expect(page.locator('.pickable-badge')).toHaveCount(0);
    });
  }
});

test.describe('external blockers (AC3)', () => {
  test('an external issue with a Project opens its team and Project, focused', async ({ page }) => {
    await openScenario(page);
    const external = node(page, 'DYD-217');
    await expect(external.locator('.external-card')).toHaveCSS('border-top-style', 'dashed');
    const link = external.getByRole('link', { name: 'Open DYD-217 in Linear' });
    await expect(link).toHaveAttribute('href', large.issues.find((issue) => issue.identifier === 'DYD-217')?.url ?? '');
    await expect(link).toHaveAttribute('target', '_blank');
    await expect(link).toHaveAttribute('rel', 'noopener');
    await external.locator('.title').click();
    await expect.poll(() => search(page).get('project')).toBe(LARGE_PROJECT);
    expect(search(page).get('team')).toBe(DYDO_TEAM);
    expect(search(page).get('focus')).toBe(issueId(large, 'DYD-217'));
    await expect(node(page, 'DYD-217')).toHaveClass(/selected/);
    await expect(node(page, 'DYD-217').locator('.issue-card')).toBeVisible();
    await expectCentred(page, 'DYD-217');
  });

  test('an external issue without a Project only takes the focus', async ({ page }) => {
    await openScenario(page);
    const before = new URL(page.url());
    await node(page, 'DYD-198').locator('.title').click();
    await expect.poll(() => search(page).get('focus')).toBe(issueId(scenario, 'DYD-198'));
    expect(search(page).get('project')).toBe(before.searchParams.get('project'));
    await expect(node(page, 'DYD-198')).toHaveClass(/selected/);
    await expect(node(page, 'DYD-198').locator('.external-project')).toHaveText('No project');
  });
});

test.describe('staying in the map (AC4)', () => {
  test('clicking a card body selects and centres it without leaving the page', async ({ page, context }) => {
    await openScenario(page);
    const origin = new URL(page.url()).origin;
    await node(page, 'DYD-266').locator('.title').click();
    await expect.poll(() => search(page).get('focus')).toBe(issueId(scenario, 'DYD-266'));
    expect(new URL(page.url()).origin).toBe(origin);
    expect(context.pages()).toHaveLength(1);
    await expect(node(page, 'DYD-266')).toHaveClass(/selected/);
    await expect.poll(async () => {
      try {
        await expectCentred(page, 'DYD-266');
        return true;
      } catch {
        return false;
      }
    }).toBe(true);
  });

  test('related links stay hidden until the toggle, then draw dashed without arrowheads', async ({ page }) => {
    await openScenario(page);
    await expect(page.locator('.edge-blocks').first()).toBeAttached();
    await expect(page.locator('.edge-related')).toHaveCount(0);
    await page.getByRole('checkbox', { name: 'Show related' }).check();
    await expect(page.locator('.edge-related').first()).toBeAttached();
    const related = page.locator('.edge-related').first();
    await expect(related).toHaveCSS('stroke-dasharray', '6px, 4px');
    expect(await related.getAttribute('marker-end')).toBeNull();
    const blocking = page.locator('.edge-blocks').first();
    await expect(blocking).toHaveCSS('stroke-dasharray', 'none');
    expect(await blocking.getAttribute('marker-end')).toMatch(/^url\(/);
    await page.screenshot({ path: 'e2e/screenshots/scenario-related.png' });
  });

  test('a closed blocker draws a muted arrow, an open one a strong arrow', async ({ page }) => {
    await openScenario(page);
    const edge = (from: string, to: string) => page.locator(`.react-flow__edge[data-id="blocks:${issueId(scenario, from)}->${issueId(scenario, to)}"] path.react-flow__edge-path`);
    await expect(edge('DYD-261', 'DYD-267')).toHaveCSS('stroke', 'rgb(180, 188, 200)');
    await expect(edge('DYD-269', 'DYD-270')).toHaveCSS('stroke', 'rgb(51, 65, 85)');
    await expect(node(page, 'DYD-261').locator('.issue-card')).toHaveCSS('opacity', '0.55');
  });
});

test('shows the error envelope to the user', async ({ page }) => {
  const served = await serveFixtures(page);
  served.graphs[SCENARIO_PROJECT] = 'error-linear-auth.json';
  await page.goto(mapUrl(SCENARIO_PROJECT));
  await expect(page.getByRole('alert')).toHaveText('linear_auth Linear rejected the API key: Authentication required, not authenticated.');
});
