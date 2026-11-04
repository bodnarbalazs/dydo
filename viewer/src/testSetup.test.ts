import { afterEach, describe, expect, it, vi } from 'vitest';

// Each parameterized case has its own storage area, just as a browser window does.
describe.each(['localStorage', 'sessionStorage'] as const)('%s browser environment', (name) => {
  afterEach(() => {
    vi.unstubAllGlobals();
    globalThis[name].clear();
  });

  it('uses browser storage through global and window access', () => {
    const storage = globalThis[name];
    expect(storage).toBeInstanceOf(Storage);
    expect(storage).toBe(window[name]);
    storage.setItem('theme', 'dark');
    expect(window[name].getItem('theme')).toBe('dark');
    expect(storage.length).toBe(1);
    expect(storage.key(0)).toBe('theme');
    storage.removeItem('theme');
    expect(storage.getItem('theme')).toBeNull();
  });

  it('restores browser storage after a blocked-storage stub', () => {
    const storage = globalThis[name];
    vi.stubGlobal(name, undefined);
    vi.unstubAllGlobals();
    expect(globalThis[name]).toBe(storage);
    globalThis[name].setItem('restored', 'yes');
    expect(window[name].getItem('restored')).toBe('yes');
  });
});

it('keeps local and session storage separate', () => {
  localStorage.setItem('scope', 'local');
  expect(sessionStorage.getItem('scope')).toBeNull();
  localStorage.clear();
});
