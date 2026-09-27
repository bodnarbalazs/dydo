import { useEffect } from 'react';

interface RefreshButtonProps {
  disabled: boolean;
  busy: boolean;
  onRefresh: () => void;
}

/** Typing R into a field, or pressing it with a modifier such as the browser's Ctrl+R, is not a refresh. */
function isShortcut(event: KeyboardEvent): boolean {
  if (event.key.toLowerCase() !== 'r' || event.ctrlKey || event.metaKey || event.altKey || event.repeat) return false;
  return !(event.target instanceof HTMLElement && ['INPUT', 'TEXTAREA', 'SELECT'].includes(event.target.tagName));
}

/** Re-fetches the Project in place, from the toolbar or with the R key. */
export function RefreshButton({ disabled, busy, onRefresh }: RefreshButtonProps) {
  const ready = !disabled && !busy;

  useEffect(() => {
    if (!ready) return undefined;
    const onKey = (event: KeyboardEvent) => {
      if (!isShortcut(event)) return;
      event.preventDefault();
      onRefresh();
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [ready, onRefresh]);

  return (
    <button type="button" className="refresh-button" disabled={!ready} aria-busy={busy} title="Refresh the map in place (R)" onClick={onRefresh}>
      <svg className={busy ? 'refresh-icon spinning' : 'refresh-icon'} width="14" height="14" viewBox="0 0 16 16" aria-hidden="true">
        <path d="M13.25 8a5.25 5.25 0 1 1-1.54-3.71" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" />
        <path d="M13.5 2v3.25h-3.25" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" />
      </svg>
      Refresh
    </button>
  );
}
