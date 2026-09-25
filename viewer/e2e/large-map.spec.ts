import { expect, test, type Page } from '@playwright/test';
import type { Graph } from '../src/api/types';
import { fixture, issueId, LARGE_PROJECT, mapUrl, node, serveFixtures } from './serveFixtures';

// Acceptance criterion 1: the captured 161-issue Project reads as a map.
const graph = fixture<Graph>('graph-large.json');
const ids = new Set(graph.issues.map((issue) => issue.id));
const parentOf = new Map(graph.issues.flatMap((issue) => (issue.parentId !== null && ids.has(issue.parentId) ? [[issue.id, issue.parentId] as const] : [])));
const plates = new Set(parentOf.values());
const topLevel = graph.issues.filter((issue) => !parentOf.has(issue.id));
const SHOTS = 'e2e/screenshots';

async function openLarge(page: Page, focus?: string) {
  await serveFixtures(page);
  await page.goto(mapUrl(LARGE_PROJECT, focus));
  await expect(page.locator('.react-flow__node-issue, .react-flow__node-plate')).toHaveCount(graph.issues.length);
}

function viewport(page: Page) {
  return page.locator('.react-flow__viewport').evaluate((element) => new DOMMatrixReadOnly(getComputedStyle(element).transform));
}

test.describe('large Project map', () => {
  test.use({ viewport: { width: 2400, height: 1500 } });

  test('draws every issue with expanded plates, status cues and closed work', async ({ page }) => {
    await openLarge(page);
    expect(graph.issues.length).toBeGreaterThanOrEqual(150);
    await expect(page.locator('.react-flow__node-plate')).toHaveCount(plates.size);
    await expect(page.locator('.plate.collapsed')).toHaveCount(0);
    const closed = graph.issues.filter((issue) => ['completed', 'canceled', 'duplicate'].includes(issue.state.type));
    await expect(page.locator('.issue-card.closed, .plate.closed')).toHaveCount(closed.length);
    await expect(page.locator('.react-flow__node-issue .status-icon, .react-flow__node-plate .plate-header .status-icon')).toHaveCount(graph.issues.length);
    await expect(node(page, 'DYD-217').locator('.state-name')).toHaveText('In Progress');
    await expect(node(page, 'DYD-11').locator('.state-name')).toHaveText('Done');
    await page.screenshot({ path: `${SHOTS}/large-overview.png` });
  });

  test('points every blocks arrow from the blocker into the blocked issue', async ({ page }) => {
    await openLarge(page);
    const ends = await page.locator('.react-flow__edge').evaluateAll((edges) =>
      edges
        .filter((edge) => edge.querySelector('.edge-blocks') !== null)
        .map((edge) => {
          const path = edge.querySelector('path.react-flow__edge-path') as SVGPathElement;
          const matrix = path.getScreenCTM() as DOMMatrix;
          const toScreen = (point: DOMPoint) => new DOMPoint(point.x, point.y).matrixTransform(matrix);
          const start = toScreen(path.getPointAtLength(0));
          const end = toScreen(path.getPointAtLength(path.getTotalLength()));
          const [source, target] = (edge.getAttribute('data-id') ?? '').replace('blocks:', '').split('->');
          const box = (id: string | undefined) => document.querySelector(`.react-flow__node[data-id="${id ?? ''}"]`)?.getBoundingClientRect();
          const from = box(source);
          const to = box(target);
          return {
            marker: path.getAttribute('marker-end') !== null,
            leavesSource: from !== undefined && Math.abs(start.x - from.right) < 3,
            entersTarget: to !== undefined && Math.abs(end.x - to.left) < 3 && end.y >= to.top - 1 && end.y <= to.bottom + 1,
          };
        }),
    );
    expect(ends.length).toBeGreaterThan(50);
    expect(ends.filter((end) => !(end.marker && end.leavesSource && end.entersTarget))).toEqual([]);
  });

  test('pans and zooms', async ({ page }) => {
    await openLarge(page);
    const before = await viewport(page);
    await page.mouse.move(1200, 800);
    await page.mouse.wheel(0, -600);
    await expect.poll(async () => (await viewport(page)).a).toBeGreaterThan(before.a);
    const zoomed = await viewport(page);
    await page.mouse.move(1200, 800);
    await page.mouse.down();
    await page.mouse.move(1000, 600, { steps: 5 });
    await page.mouse.up();
    await expect.poll(async () => (await viewport(page)).e).toBeLessThan(zoomed.e);
  });

  test('Collapse all keeps only top-level plates and Expand all restores them', async ({ page }) => {
    await openLarge(page);
    await page.getByRole('button', { name: 'Collapse all' }).click();
    const topPlates = topLevel.filter((issue) => plates.has(issue.id));
    await expect(page.locator('.react-flow__node-plate')).toHaveCount(topPlates.length);
    await expect(page.locator('.plate.collapsed')).toHaveCount(topPlates.length);
    await expect(page.locator('.react-flow__node-issue, .react-flow__node-plate')).toHaveCount(topLevel.length);
    await page.screenshot({ path: `${SHOTS}/large-collapsed.png` });
    await page.getByRole('button', { name: 'Expand all' }).click();
    await expect(page.locator('.react-flow__node-plate')).toHaveCount(plates.size);
    await expect(page.locator('.react-flow__node-issue, .react-flow__node-plate')).toHaveCount(graph.issues.length);
  });

  test('collapses one plate and re-anchors its edges', async ({ page }) => {
    await openLarge(page);
    const plate = node(page, 'DYD-96');
    await plate.getByRole('button', { name: 'Collapse DYD-96' }).click();
    await expect(node(page, 'DYD-105')).toHaveCount(0);
    const plateId = issueId(graph, 'DYD-96');
    await expect(page.locator(`.react-flow__edge[data-id$="->${plateId}"], .react-flow__edge[data-id^="blocks:${plateId}->"]`).first()).toBeAttached();
    await plate.getByRole('button', { name: 'Expand DYD-96' }).click();
    await expect(node(page, 'DYD-105')).toHaveCount(1);
  });
});

test.describe('large Project close-ups', () => {
  test.use({ viewport: { width: 1600, height: 1000 } });

  for (const [name, identifier] of [
    ['large-zoom-plates', 'DYD-130'],
    ['large-zoom-release-hub', 'DYD-11'],
  ] as const) {
    test(`focuses ${identifier} for the ${name} close-up`, async ({ page }) => {
      await openLarge(page, issueId(graph, identifier));
      await expect(node(page, identifier)).toHaveClass(/selected/);
      await expect(node(page, identifier)).toBeInViewport();
      await page.screenshot({ path: `${SHOTS}/${name}.png` });
    });
  }
});

test.describe('large Project mid-zoom', () => {
  // The fit-all overview only shows structure; this shot shows a busy region with readable cards.
  test.use({ viewport: { width: 1600, height: 1000 }, deviceScaleFactor: 2 });

  test('zooms out from DYD-164 over the busy column', async ({ page }) => {
    await openLarge(page, issueId(graph, 'DYD-164'));
    await expect(node(page, 'DYD-164')).toBeInViewport();
    await page.mouse.move(800, 500);
    await expect
      .poll(async () => {
        await page.mouse.wheel(0, 100);
        return (await viewport(page)).a;
      })
      .toBeLessThan(0.55);
    await expect(node(page, 'DYD-164')).toBeInViewport();
    await page.screenshot({ path: `${SHOTS}/large-mid-zoom.png` });
  });
});
