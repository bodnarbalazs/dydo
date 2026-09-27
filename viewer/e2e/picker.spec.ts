import { expect, test, type Page } from '@playwright/test';
import { DYDO_TEAM, LARGE_PROJECT, mapUrl, node, SCENARIO_PROJECT, serveFixtures } from './serveFixtures';

// The fixture's target dates are chosen around this day, so overdue tints are fixed.
const TODAY = new Date('2026-09-27T12:00:00');

const trigger = (page: Page) => page.getByRole('button', { name: /^Project / });
const search = (page: Page) => page.getByRole('combobox', { name: 'Search Projects' });
const listbox = (page: Page) => page.getByRole('listbox', { name: 'Projects' });
const group = (page: Page, name: string) => listbox(page).getByRole('group', { name, exact: true });
const optionTexts = (page: Page, name: string) => group(page, name).getByRole('option').allTextContents();

async function openTeam(page: Page) {
  await serveFixtures(page);
  await page.clock.setFixedTime(TODAY);
  await page.goto(`/?team=${DYDO_TEAM}`);
  await expect(trigger(page)).toBeEnabled();
}

test('the Project picker groups, picks, expands Closed, searches and works by keyboard', async ({ page }) => {
  await openTeam(page);

  await test.step('open: status sections in order, earliest target first, overdue tinted', async () => {
    await expect(trigger(page)).toHaveAccessibleName('Project Choose a Project');
    await expect(trigger(page)).toHaveAttribute('aria-haspopup', 'listbox');
    await trigger(page).click();
    await expect(trigger(page)).toHaveAttribute('aria-expanded', 'true');
    await expect(search(page)).toBeFocused();
    expect(await listbox(page).getByRole('group').evaluateAll((groups) => groups.map((each) => each.getAttribute('aria-label')))).toEqual([
      'In Progress',
      'Planned',
      'Paused',
      'Backlog',
      'Closed',
    ]);
    expect(await optionTexts(page, 'In Progress')).toEqual([
      'Release pipeline hardeningSep 15',
      'Onboarding tourOct 3',
      'dydo 3.0 / Consolidate and release',
      'dydo 3.0 / Harmonize the skill system',
      'Visual Linear project map',
    ]);
    expect(await optionTexts(page, 'Planned')).toEqual(['Docs site refreshAug 31', 'Viewer dark modeNov 20', 'Map export to PNGFeb 12, 2027']);
    expect(await optionTexts(page, 'Paused')).toEqual(['Linear webhooksDec 1', 'Plugin marketplace']);
    expect(await optionTexts(page, 'Backlog')).toEqual(['Windows installerMar 1, 2027', 'Offline cache']);
    expect(await optionTexts(page, 'Closed')).toEqual(['▸Closed (7)']);
    await expect(listbox(page).locator('.picker-date.overdue')).toHaveText(['Sep 15', 'Aug 31']);
    await expect(listbox(page).locator('.picker-date.overdue').first()).toHaveCSS('color', 'rgb(220, 38, 38)');
    await expect(listbox(page).getByRole('option', { name: 'Dogfood' })).toHaveCount(0);
    await page.screenshot({ path: 'e2e/screenshots/picker-open.png' });
  });

  await test.step('pick: the URL names the Project and its map is drawn', async () => {
    await listbox(page).getByRole('option', { name: 'Visual Linear project map' }).click();
    await expect.poll(() => new URL(page.url()).searchParams.get('project')).toBe(SCENARIO_PROJECT);
    await expect(node(page, 'DYD-268')).toBeVisible();
    await expect(trigger(page)).toHaveAccessibleName('Project Visual Linear project map');
    await expect(trigger(page)).toBeFocused();
    await expect(listbox(page)).toHaveCount(0);
  });

  await test.step('Closed: 5 newest completions and Show all, collapsed again on the next open', async () => {
    await trigger(page).click();
    await expect(listbox(page).getByRole('option', { name: 'Visual Linear project map' })).toHaveAttribute('aria-selected', 'true');
    await group(page, 'Closed').getByRole('option', { name: 'Closed (7)' }).click();
    expect(await optionTexts(page, 'Closed')).toEqual([
      '▾Closed (7)',
      'Search index rebuild',
      'CLI telemetry opt-out',
      'dydo 3.0 / Simplify the skill model',
      'dydo 3.0 / Restore skill craftsmanship',
      'dydo 3.0 / Migrate the v2 work corpus',
      'Show all 7',
    ]);
    await group(page, 'Closed').getByRole('option', { name: 'Show all 7' }).scrollIntoViewIfNeeded();
    await page.screenshot({ path: 'e2e/screenshots/picker-closed.png' });
    await group(page, 'Closed').getByRole('option', { name: 'Show all 7' }).click();
    await expect(group(page, 'Closed').getByRole('option')).toHaveCount(8);
    await expect(group(page, 'Closed').getByRole('option').last()).toHaveText('dydo 3.0 / Adopt Linear-native work model');
    await page.locator('.canvas').click({ position: { x: 20, y: 20 } });
    await expect(listbox(page)).toHaveCount(0);
    await trigger(page).click();
    expect(await optionTexts(page, 'Closed')).toEqual(['▸Closed (7)']);
  });

  await test.step('search: every status by name, Canceled included, each with a status cue', async () => {
    await search(page).fill('DOGFOOD');
    await expect(listbox(page).getByRole('option')).toHaveText(['dydo 3.0 / Dogfood and accept Linear PMCanceled']);
    await search(page).fill('map');
    await expect(listbox(page).getByRole('option')).toHaveText([
      'Visual Linear project mapIn Progress',
      'Map export to PNGPlannedFeb 12, 2027',
    ]);
    await search(page).fill('dydo 3.0');
    await expect(listbox(page).locator('.picker-cue')).toHaveText(['In Progress', 'In Progress', 'Completed', 'Completed', 'Completed', 'Completed', 'Completed', 'Canceled']);
    await page.screenshot({ path: 'e2e/screenshots/picker-search.png' });
    await page.keyboard.press('Escape');
    await expect(listbox(page)).toHaveCount(0);
    await expect(trigger(page)).toBeFocused();
  });

  await test.step('keyboard only: open, search, move and choose', async () => {
    await page.keyboard.press('ArrowDown');
    await expect(search(page)).toBeFocused();
    await page.keyboard.type('dydo 3.0');
    await expect(search(page)).toHaveAttribute('aria-activedescendant', /.+/);
    await expect(listbox(page).locator('.picker-row.active')).toHaveText('dydo 3.0 / Consolidate and releaseIn Progress');
    await page.keyboard.press('ArrowDown');
    await expect(listbox(page).locator('.picker-row.active')).toHaveText('dydo 3.0 / Harmonize the skill systemIn Progress');
    await page.keyboard.press('End');
    await expect(listbox(page).locator('.picker-row.active')).toHaveText('dydo 3.0 / Dogfood and accept Linear PMCanceled');
    await page.keyboard.press('Home');
    await page.keyboard.press('Enter');
    await expect.poll(() => new URL(page.url()).searchParams.get('project')).toBe(LARGE_PROJECT);
    await expect(trigger(page)).toHaveAccessibleName('Project dydo 3.0 / Consolidate and release');
    await expect(trigger(page)).toBeFocused();
  });
});

test('Escape closes an open picker from a deep link and keeps the Project', async ({ page }) => {
  await serveFixtures(page);
  await page.clock.setFixedTime(TODAY);
  await page.goto(mapUrl(SCENARIO_PROJECT));
  await expect(trigger(page)).toHaveAccessibleName('Project Visual Linear project map');
  await trigger(page).click();
  await page.keyboard.press('ArrowDown');
  await page.keyboard.press('Escape');
  await expect(listbox(page)).toHaveCount(0);
  expect(new URL(page.url()).searchParams.get('project')).toBe(SCENARIO_PROJECT);
});
