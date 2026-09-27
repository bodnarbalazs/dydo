/** What the viewer is asked to show: the OS scheme, or a fixed theme. */
export type ThemePreference = 'system' | 'light' | 'dark';
export type Theme = 'light' | 'dark';

// index.html's pre-paint script repeats these two names and the rules below; theme.test.ts keeps them in step.
export const THEME_KEY = 'dydo-map-theme';
export const DARK_QUERY = '(prefers-color-scheme: dark)';

/** The stored preference; anything else, including nothing, means System. */
export function readPreference(stored: string | null): ThemePreference {
  return stored === 'light' || stored === 'dark' ? stored : 'system';
}

export function resolveTheme(preference: ThemePreference, systemDark: boolean): Theme {
  if (preference !== 'system') return preference;
  return systemDark ? 'dark' : 'light';
}

/** Marks the root for the stylesheet and sets `color-scheme`, so native controls and scrollbars match. */
export function applyTheme(root: HTMLElement, theme: Theme): void {
  root.dataset['theme'] = theme;
  root.style.colorScheme = theme;
}
