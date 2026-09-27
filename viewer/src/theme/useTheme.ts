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

/** Blocked storage throws on any access; it reads as System. */
function storedPreference(): ThemePreference {
  try {
    return readPreference(localStorage.getItem(THEME_KEY));
  } catch {
    return 'system';
  }
}

/** The saved preference and the theme it resolves to, kept on the page and following the OS live on System. */
export function useThemePreference(): { preference: ThemePreference; theme: Theme; choose: (next: ThemePreference) => void } {
  const [preference, setPreference] = useState(storedPreference);
  const systemDark = useSyncExternalStore(subscribeToOs, osIsDark);
  const theme = resolveTheme(preference, systemDark);

  useLayoutEffect(() => applyTheme(document.documentElement, theme), [theme]);

  const choose = useCallback((next: ThemePreference) => {
    try {
      localStorage.setItem(THEME_KEY, next);
    } catch {
      // Storage blocked or full: the choice holds for this session only.
    }
    setPreference(next);
  }, []);
  return { preference, theme, choose };
}
