import { act, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { THEME_KEY } from './theme';
import { ThemeControl } from './ThemeControl';
import { useThemePreference } from './useTheme';

/** A stand-in OS colour scheme the test can flip, as `matchMedia` reports it. */
function fakeOs(dark: boolean) {
  const listeners = new Set<() => void>();
  const os = {
    dark,
    flip(next: boolean) {
      os.dark = next;
      listeners.forEach((listener) => listener());
    },
  };
  vi.stubGlobal('matchMedia', (query: string) => ({
    get matches() {
      return query === '(prefers-color-scheme: dark)' && os.dark;
    },
    addEventListener: (_type: 'change', listener: () => void) => listeners.add(listener),
    removeEventListener: (_type: 'change', listener: () => void) => listeners.delete(listener),
  }));
  return os;
}

function Harness() {
  const { preference, theme, choose } = useThemePreference();
  return (
    <>
      <output>{`${preference}:${theme}`}</output>
      <ThemeControl preference={preference} onChoose={choose} />
    </>
  );
}

const root = document.documentElement;
const shown = () => screen.getByRole('status').textContent;
const applied = () => [root.dataset['theme'], root.style.colorScheme];

beforeEach(() => {
  localStorage.clear();
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('useThemePreference', () => {
  it('follows a dark OS by default and marks the page dark', () => {
    fakeOs(true);
    render(<Harness />);
    expect(shown()).toBe('system:dark');
    expect(applied()).toEqual(['dark', 'dark']);
  });

  it('flips live when the OS scheme changes while on System', () => {
    const os = fakeOs(false);
    render(<Harness />);
    expect(shown()).toBe('system:light');
    act(() => os.flip(true));
    expect(shown()).toBe('system:dark');
    expect(applied()).toEqual(['dark', 'dark']);
  });

  it('saves a chosen theme, which then overrides the OS', () => {
    const os = fakeOs(true);
    render(<Harness />);
    fireEvent.click(screen.getByRole('button', { name: 'Light theme' }));
    expect(localStorage.getItem(THEME_KEY)).toBe('light');
    expect(shown()).toBe('light:light');
    act(() => os.flip(false));
    act(() => os.flip(true));
    expect(shown()).toBe('light:light');
    expect(applied()).toEqual(['light', 'light']);
  });

  it('restores the saved theme on load', () => {
    fakeOs(false);
    localStorage.setItem(THEME_KEY, 'dark');
    render(<Harness />);
    expect(shown()).toBe('dark:dark');
  });

  it('treats an invalid saved value as System', () => {
    fakeOs(true);
    localStorage.setItem(THEME_KEY, 'neon');
    render(<Harness />);
    expect(shown()).toBe('system:dark');
  });

  it('reads blocked storage as System and still switches the theme for the session', () => {
    const os = fakeOs(true);
    const blocked = () => {
      throw new DOMException('The operation is insecure.', 'SecurityError');
    };
    vi.stubGlobal('localStorage', { getItem: blocked, setItem: blocked });
    render(<Harness />);
    expect(shown()).toBe('system:dark');
    fireEvent.click(screen.getByRole('button', { name: 'Light theme' }));
    expect(shown()).toBe('light:light');
    act(() => os.flip(false));
    act(() => os.flip(true));
    expect(applied()).toEqual(['light', 'light']);
  });

  it('stops listening to the OS once unmounted', () => {
    const os = fakeOs(false);
    const view = render(<Harness />);
    view.unmount();
    act(() => os.flip(true));
    expect(applied()).toEqual(['light', 'light']);
  });
});

describe('ThemeControl', () => {
  it('offers System, Light and Dark, pressing only the current one', () => {
    const onChoose = vi.fn();
    render(<ThemeControl preference="dark" onChoose={onChoose} />);
    const group = screen.getByRole('group', { name: 'Theme' });
    const buttons = [...group.querySelectorAll('button')].map((button) => [button.getAttribute('aria-label'), button.getAttribute('aria-pressed')]);
    expect(buttons).toEqual([
      ['System theme', 'false'],
      ['Light theme', 'false'],
      ['Dark theme', 'true'],
    ]);
    fireEvent.click(screen.getByRole('button', { name: 'System theme' }));
    expect(onChoose).toHaveBeenCalledWith('system');
  });
});
