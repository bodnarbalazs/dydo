import { expect, test, type Page } from '@playwright/test';
import { mapUrl, SCENARIO_PROJECT, serveFixtures } from './serveFixtures';

// A plain dark card's shadow: no status glow.
const DARK_CARD_SHADOW = 'rgba(0, 0, 0, 0.3) 0px 1px 2px 0px';

const theme = (page: Page) => page.locator('html').getAttribute('data-theme');
const choose = (page: Page, name: 'System' | 'Light' | 'Dark') => page.getByRole('button', { name: `${name} theme` }).click();
const colorOf = (page: Page, selector: string, property: string) =>
  page.locator(selector).first().evaluate((element, name) => getComputedStyle(element).getPropertyValue(name), property);

async function openScenario(page: Page) {
  await serveFixtures(page);
  await page.goto(mapUrl(SCENARIO_PROJECT));
  await expect(page.locator('.react-flow__node').first()).toBeVisible();
}

test('an OS dark scheme paints the page dark before the app runs', async ({ page }) => {
  await page.emulateMedia({ colorScheme: 'dark' });
  // Without the bundle only index.html's pre-paint script can theme the page: what the first frame shows.
  await page.route('**/assets/*.js', (route) => route.abort());
  await page.goto('/');
  expect(await theme(page)).toBe('dark');
  expect(await colorOf(page, 'html', 'color-scheme')).toBe('dark');
  await page.unrouteAll();
  await page.goto('/');
  await expect(page.getByRole('button', { name: 'System theme' })).toHaveAttribute('aria-pressed', 'true');
  expect(await colorOf(page, 'html', 'background-color')).toBe('rgb(15, 16, 17)');
});

test('the toolbar theme control overrides the OS, persists across reload and returns to it', async ({ page }) => {
  await page.emulateMedia({ colorScheme: 'light' });
  await openScenario(page);
  expect(await theme(page)).toBe('light');

  await choose(page, 'Dark');
  expect(await theme(page)).toBe('dark');
  await page.reload();
  await expect(page.getByRole('button', { name: 'Dark theme' })).toHaveAttribute('aria-pressed', 'true');
  expect(await theme(page)).toBe('dark');

  await choose(page, 'Light');
  await page.emulateMedia({ colorScheme: 'dark' });
  expect(await theme(page)).toBe('light');

  await choose(page, 'System');
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');
  await page.emulateMedia({ colorScheme: 'light' });
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'light');
  await page.reload();
  await expect(page.getByRole('button', { name: 'System theme' })).toHaveAttribute('aria-pressed', 'true');
});

test('light keeps today\'s colours; dark themes the canvas, cards and edges and glows only started issues', async ({ page }) => {
  await page.emulateMedia({ colorScheme: 'light' });
  await openScenario(page);
  const light = {
    toolbar: await colorOf(page, '.toolbar', 'background-color'),
    canvas: await colorOf(page, 'html', 'background-color'),
    edge: await colorOf(page, '.edge-blocks', 'stroke'),
    glow: await colorOf(page, '.plate.started > .plate-header', 'box-shadow'),
    edges: [await colorOf(page, 'html', '--edge-muted'), await colorOf(page, 'html', '--edge-related')],
  };
  expect(light).toEqual({ toolbar: 'rgb(255, 255, 255)', canvas: 'rgb(248, 250, 252)', edge: 'rgb(51, 65, 85)', glow: 'none', edges: ['#b4bcc8', '#8b5cf6'] });

  await choose(page, 'Dark');
  expect(await colorOf(page, 'html', 'background-color')).toBe('rgb(15, 16, 17)');
  expect(await colorOf(page, '.toolbar', 'background-color')).not.toBe(light.toolbar);
  expect(await colorOf(page, '.edge-blocks', 'stroke')).not.toBe(light.edge);
  expect(await colorOf(page, '.react-flow__controls-button', 'background-color')).not.toBe('rgb(255, 255, 255)');
  expect(await colorOf(page, '.react-flow__minimap', 'background-color')).not.toBe('rgb(255, 255, 255)');
  // DYD-265 is Implementing (#f2994a): a hairline and a soft halo in its colour.
  expect(await colorOf(page, '.plate[data-identifier="DYD-265"] > .plate-header', 'box-shadow')).toBe(
    'color(srgb 0.94902 0.6 0.290196 / 0.38) 0px 0px 0px 1px, color(srgb 0.94902 0.6 0.290196 / 0.55) 0px 0px 18px -3px',
  );
  expect(await colorOf(page, '.issue-card[data-identifier="DYD-262"]', 'box-shadow')).toBe(DARK_CARD_SHADOW);
});

test('the favicon is an SVG of the map', async ({ page, request }) => {
  await page.goto('/');
  const href = await page.locator('link[rel="icon"]').getAttribute('href');
  expect(href).not.toBeNull();
  const response = await request.get(new URL(href ?? '', page.url()).toString());
  expect(response.status()).toBe(200);
  expect(response.headers()['content-type']).toContain('image/svg+xml');
  expect(await response.text()).toContain('<svg');
});
