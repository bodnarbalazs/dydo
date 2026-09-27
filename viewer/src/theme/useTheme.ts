import { createContext, useCallback, useContext, useLayoutEffect, useState, useSyncExternalStore } from 'react';
import { applyTheme, DARK_QUERY, readPreference, resolveTheme, THEME_KEY, type Theme, type ThemePreference } from './theme';

/** The resolved theme, for components that colour themselves in script. */
export const ThemeContext = createContext<Theme>('light');

export function useTheme(): Theme {
  return useContext(ThemeContext);
}

function subscribeToOs(onChange: () => void): () => void {
  const query = window.matchMedia(DARK_QUERY);
  query.addEventListener('change', onChange);
  return () => query.removeEventListener('change', onChange);
}

const osIsDark = () => window.matchMedia(DARK_QUERY).matches;

/** The saved preference and the theme it resolves to, kept on the page and following the OS live on System. */
export function useThemePreference(): { preference: ThemePreference; theme: Theme; choose: (next: ThemePreference) => void } {
  const [preference, setPreference] = useState(() => readPreference(localStorage.getItem(THEME_KEY)));
  const systemDark = useSyncExternalStore(subscribeToOs, osIsDark);
  const theme = resolveTheme(preference, systemDark);

  useLayoutEffect(() => applyTheme(document.documentElement, theme), [theme]);

  const choose = useCallback((next: ThemePreference) => {
    localStorage.setItem(THEME_KEY, next);
    setPreference(next);
  }, []);
  return { preference, theme, choose };
}
