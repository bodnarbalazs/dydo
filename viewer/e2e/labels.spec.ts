import { expect, test, type Locator, type Page } from '@playwright/test';
import { mapUrl, node, SCENARIO_PROJECT, serveFixtures } from './serveFixtures';

interface Box {
  left: number;
  right: number;
  top: number;
  bottom: number;
}

interface TopRow {
  row: Box;
  link: Box;
  chips: { text: string; box: Box; truncated: boolean; layoutLeft: number; layoutRight: number }[];
}

async function openScenario(page: Page) {
  await serveFixtures(page);
  await page.goto(mapUrl(SCENARIO_PROJECT));
  await expect(node(page, 'DYD-268')).toBeVisible();
}

/** The label row, its chips and the link of a card's top row, in page pixels. */
function topRow(card: Locator): Promise<TopRow> {
  return card.locator('.card-top').first().evaluate((top) => {
    const box = (element: Element) => {
      const { left, right, top: upper, bottom } = element.getBoundingClientRect();
      return { left, right, top: upper, bottom };
    };
    const row = top.querySelector('.labels');
    const link = top.querySelector('.linear-link');
    if (row === null || link === null) throw new Error('no label row or link');
    return { row: box(row), link: box(link), chips: [...row.querySelectorAll('.label-chip')].map((chip) => {
        const name = chip.querySelector('.label-name');
        const { offsetLeft, offsetWidth } = chip as HTMLElement;
        // Layout pixels, before the map's zoom.
        return { text: chip.textContent, box: box(chip), truncated: name !== null && name.scrollWidth > name.clientWidth, layoutLeft: offsetLeft, layoutRight: offsetLeft + offsetWidth };
      }) };
  });
}

const inside = (chip: Box, row: Box) => chip.left >= row.left - 0.5 && chip.right <= row.right + 0.5 && chip.top >= row.top - 0.5 && chip.bottom <= row.bottom + 0.5;
const outside = (chip: Box, row: Box) => chip.top >= row.bottom || chip.left >= row.right;
const middle = (box: Box) => (box.top + box.bottom) / 2;

test.describe('label chips (DYD-303)', () => {
  test('show what fits whole, hide the rest whole, and sit centred on one row with the link', async ({ page }) => {
    await openScenario(page);
    for (const identifier of ['DYD-268', 'DYD-270', 'DYD-271']) {
      const { row, link, chips } = await topRow(node(page, identifier));
      expect(chips.length, identifier).toBeLessThanOrEqual(3);
      const shown = chips.filter((chip) => inside(chip.box, row));
      expect(shown.length, identifier).toBeGreaterThan(0);
      chips.forEach((chip) => expect(inside(chip.box, row) || outside(chip.box, row), `${identifier} ${chip.text}`).toBe(true));
      // The shown chips are a prefix, never a later chip in the place of a hidden one.
      expect(shown.map((chip) => chip.text)).toEqual(chips.slice(0, shown.length).map((chip) => chip.text));
      shown.forEach((chip) => expect(Math.abs(middle(chip.box) - middle(link)), `${identifier} ${chip.text}`).toBeLessThan(1));
      shown.forEach((chip) => expect(chip.truncated, `${identifier} ${chip.text} is cut`).toBe(false));
      expect(row.right, identifier).toBeLessThanOrEqual(link.left);
      shown.slice(1).forEach((chip, index) => expect(chip.layoutLeft - (shown[index]?.layoutRight ?? 0)).toBe(4));
    }
  });

  test('cap five labels at three chips, by name, and hide the one that does not fit', async ({ page }) => {
    await openScenario(page);
    const card = node(page, 'DYD-271');
    await expect(card.locator('.label-chip')).toHaveText(['AFK', 'Improvement', 'Merge']);
    await expect(card.locator('.labels')).toHaveAttribute('title', 'AFK, Improvement, Merge, Needs human, Walkthrough');
    const { row, chips } = await topRow(card);
    expect(chips.map((chip) => inside(chip.box, row))).toEqual([true, true, false]);
  });

  test('truncate a lone first chip too wide for the row with an ellipsis', async ({ page }) => {
    await openScenario(page);
    const card = node(page, 'DYD-272');
    const { row, chips } = await topRow(card);
    const [long, merge] = chips;
    if (long === undefined || merge === undefined) throw new Error('DYD-272 lost its labels');
    expect(long.text).toBe('Blocked upstream on Linear archived-relation semantics');
    expect(inside(long.box, row)).toBe(true);
    expect(long.box.right - long.box.left).toBeCloseTo(row.right - row.left, 0);
    expect(outside(merge.box, row)).toBe(true);
    const name = card.locator('.label-name').first();
    await expect(name).toHaveCSS('text-overflow', 'ellipsis');
    expect(await name.evaluate((element) => element.scrollWidth > element.clientWidth)).toBe(true);
  });

  test('the identifier link opens Linear in a new tab and leaves the card unselected', async ({ page, context }) => {
    await context.route('https://linear.app/**', (route) => route.fulfill({ contentType: 'text/html', body: '<title>Linear</title>' }));
    await openScenario(page);
    const card = node(page, 'DYD-268');
    const link = card.getByRole('link', { name: 'Open DYD-268 in Linear' });
    await expect(link).toHaveText('DYD-268 ↗');
    await expect(link).toHaveAttribute('target', '_blank');
    await expect(link).toHaveAttribute('rel', 'noopener');
    await expect(link).toHaveCSS('font-family', /monospace/);
    const [tab] = await Promise.all([context.waitForEvent('page'), link.click()]);
    await expect.poll(() => tab.url()).toMatch(/^https:\/\/linear\.app\//);
    expect(new URL(page.url()).searchParams.get('focus')).toBeNull();
    await expect(card).not.toHaveClass(/selected/);
  });

  test('the Pickable badge takes the bottom-right corner', async ({ page }) => {
    await openScenario(page);
    const card = node(page, 'DYD-268').locator('.issue-card');
    const [cardBox, badge, state] = await Promise.all([card.boundingBox(), card.locator('.pickable-badge').boundingBox(), card.locator('.state').boundingBox()]);
    if (cardBox === null || badge === null || state === null) throw new Error('DYD-268 is not drawn');
    expect(cardBox.x + cardBox.width - (badge.x + badge.width)).toBeLessThan(14);
    expect(badge.y).toBeGreaterThan(cardBox.y + cardBox.height / 2);
    expect(Math.abs(badge.y + badge.height / 2 - (state.y + state.height / 2))).toBeLessThan(1.5);
    await expect(card.locator('.assignee')).toHaveCount(0);
  });
});
