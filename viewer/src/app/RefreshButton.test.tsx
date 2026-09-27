import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { RefreshButton } from './RefreshButton';

function renderButton(props: { disabled?: boolean; busy?: boolean } = {}) {
  const onRefresh = vi.fn();
  render(
    <>
      <input aria-label="Search" />
      <textarea aria-label="Notes" />
      <select aria-label="Pick" />
      <RefreshButton disabled={props.disabled ?? false} busy={props.busy ?? false} onRefresh={onRefresh} />
    </>,
  );
  return onRefresh;
}

const button = () => screen.getByRole<HTMLButtonElement>('button', { name: 'Refresh' });

describe('RefreshButton', () => {
  it('refreshes on a click and names its shortcut', () => {
    const onRefresh = renderButton();
    fireEvent.click(button());
    expect(onRefresh).toHaveBeenCalledOnce();
    expect(button().title).toBe('Refresh the map in place (R)');
    expect(button().getAttribute('aria-busy')).toBe('false');
  });

  it('refreshes on R or r pressed outside a text field, and keeps the key from typing anywhere', () => {
    const onRefresh = renderButton();
    expect(fireEvent.keyDown(document.body, { key: 'r' })).toBe(false);
    fireEvent.keyDown(button(), { key: 'R' });
    expect(onRefresh).toHaveBeenCalledTimes(2);
  });

  it.each([
    ['another key', { key: 'e' }],
    ['Ctrl+R, the browser reload', { key: 'r', ctrlKey: true }],
    ['Cmd+R', { key: 'r', metaKey: true }],
    ['Alt+R', { key: 'r', altKey: true }],
    ['a held-down R', { key: 'r', repeat: true }],
  ])('ignores %s', (_name, init) => {
    const onRefresh = renderButton();
    expect(fireEvent.keyDown(document.body, init)).toBe(true);
    expect(onRefresh).not.toHaveBeenCalled();
  });

  it.each(['Search', 'Notes', 'Pick'])('lets R type into %s', (field) => {
    const onRefresh = renderButton();
    fireEvent.keyDown(screen.getByLabelText(field), { key: 'r' });
    expect(onRefresh).not.toHaveBeenCalled();
  });

  it('is disabled, shortcut included, without a Project', () => {
    const onRefresh = renderButton({ disabled: true });
    expect(button().disabled).toBe(true);
    fireEvent.keyDown(document.body, { key: 'r' });
    expect(onRefresh).not.toHaveBeenCalled();
  });

  it('spins and waits while a refresh is under way', () => {
    const onRefresh = renderButton({ busy: true });
    expect(button().disabled).toBe(true);
    expect(button().getAttribute('aria-busy')).toBe('true');
    expect(button().querySelector('svg')?.getAttribute('class')).toBe('refresh-icon spinning');
    fireEvent.keyDown(document.body, { key: 'r' });
    expect(onRefresh).not.toHaveBeenCalled();
  });
});
