import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { runInNewContext } from 'node:vm';
import { describe, expect, it } from 'vitest';
import { applyTheme, readPreference, resolveTheme, THEME_KEY, type Theme, type ThemePreference } from './theme';

describe('readPreference', () => {
  it.each(['system', 'light', 'dark'] as const)('reads the stored preference %s', (stored) => {
    expect(readPreference(stored)).toBe(stored);
  });

  it.each([null, '', 'Dark', 'sepia'])('falls back to System for the stored value %s', (stored) => {
    expect(readPreference(stored)).toBe('system');
  });
});

describe('resolveTheme', () => {
  it.each([
    ['system', false, 'light'],
    ['system', true, 'dark'],
    ['light', true, 'light'],
    ['light', false, 'light'],
    ['dark', false, 'dark'],
    ['dark', true, 'dark'],
  ] as const)('resolves %s with an OS dark scheme of %s to %s', (preference, systemDark, theme) => {
    expect(resolveTheme(preference, systemDark)).toBe(theme);
  });
});

describe('applyTheme', () => {
  it('marks the root with the theme and its colour scheme', () => {
    const root = document.createElement('html');
    applyTheme(root, 'dark');
    expect([root.dataset['theme'], root.style.colorScheme]).toEqual(['dark', 'dark']);
    applyTheme(root, 'light');
    expect([root.dataset['theme'], root.style.colorScheme]).toEqual(['light', 'light']);
  });
});

describe('the pre-paint script in index.html', () => {
  const html = readFileSync(resolve(import.meta.dirname, '../../index.html'), 'utf-8');
  const script = /<script>([\s\S]*?)<\/script>/.exec(html)?.[1] ?? '';

  /** Runs the inline script against a stored value and an OS scheme, returning what it put on the root. */
  function prePaint(stored: string | null, systemDark: boolean): [string | undefined, string] {
    const root = document.createElement('html');
    runInNewContext(script, {
      localStorage: { getItem: (key: string) => (key === THEME_KEY ? stored : null) },
      matchMedia: (query: string) => ({ matches: query === '(prefers-color-scheme: dark)' && systemDark }),
      document: { documentElement: root },
    });
    return [root.dataset['theme'], root.style.colorScheme];
  }

  it('runs before the module bundle', () => {
    expect(script).not.toBe('');
    expect(html.indexOf(script)).toBeLessThan(html.indexOf('type="module"'));
  });

  const cases: [string | null, boolean][] = [null, 'system', 'light', 'dark', 'bogus'].flatMap((stored) => [
    [stored, false],
    [stored, true],
  ]);
  it.each(cases)('agrees with resolveTheme for the stored value %s and an OS dark scheme of %s', (stored, systemDark) => {
    const theme: Theme = resolveTheme(readPreference(stored) satisfies ThemePreference, systemDark);
    expect(prePaint(stored, systemDark)).toEqual([theme, theme]);
  });
});
