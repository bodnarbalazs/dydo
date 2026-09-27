import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { Toolbar } from './Toolbar';

function renderToolbar(overrides: Partial<Parameters<typeof Toolbar>[0]> = {}) {
  const props = {
    teams: [{ id: 't', key: 'T', name: 'Team' }],
    projects: [{ id: 'p', name: 'Map', url: 'u', status: { name: 'In Progress', type: 'started' }, targetDate: null, completedAt: null, canceledAt: null }],
    team: 't',
    project: null,
    hasPlates: true,
    showRelated: false,
    summary: null,
    canRefresh: true,
    refreshing: false,
    notice: null as string | null,
    onRefresh: vi.fn(),
    onTeam: vi.fn(),
    onProject: vi.fn(),
    onCollapseAll: vi.fn(),
    onExpandAll: vi.fn(),
    onShowRelated: vi.fn(),
    theme: 'system' as const,
    onTheme: vi.fn(),
    ...overrides,
  };
  render(<Toolbar {...props} />);
  return props;
}

describe('Toolbar', () => {
  it('shows the theme preference and reports a new one', () => {
    const props = renderToolbar({ theme: 'light' });
    expect(screen.getByRole('button', { name: 'Light theme' }).getAttribute('aria-pressed')).toBe('true');
    fireEvent.click(screen.getByRole('button', { name: 'Dark theme' }));
    expect(props.onTheme).toHaveBeenCalledWith('dark');
  });

  it('reports each choice', () => {
    const props = renderToolbar();
    fireEvent.change(screen.getByRole('combobox'), { target: { value: 't' } });
    fireEvent.click(screen.getByRole('button', { name: 'Project Choose a Project' }));
    fireEvent.click(screen.getByRole('option', { name: 'Map' }));
    fireEvent.click(screen.getByRole('button', { name: 'Collapse all' }));
    fireEvent.click(screen.getByRole('button', { name: 'Expand all' }));
    fireEvent.click(screen.getByRole('checkbox', { name: 'Show related' }));
    expect(props.onTeam).toHaveBeenCalledWith('t');
    expect(props.onProject).toHaveBeenCalledWith('p');
    expect(props.onCollapseAll).toHaveBeenCalledOnce();
    expect(props.onExpandAll).toHaveBeenCalledOnce();
    expect(props.onShowRelated).toHaveBeenCalledWith(true);
  });

  it('prompts for a team and a Project until they are chosen, and neither prompt is a choice', () => {
    renderToolbar({ team: null });
    const select = screen.getByRole<HTMLSelectElement>('combobox');
    expect(select.selectedOptions[0]?.textContent).toBe('Choose a team');
    expect(select.selectedOptions[0]?.disabled).toBe(true);
    expect(screen.getByRole('button', { name: 'Project Choose a Project' })).toBeTruthy();
    expect(screen.getByText('dydo map')).toBeTruthy();
  });

  it('shows the chosen team and whether related links are shown', () => {
    renderToolbar({ showRelated: true });
    expect(screen.getByRole<HTMLSelectElement>('combobox').selectedOptions[0]?.textContent).toBe('Team (T)');
    expect(screen.getByRole<HTMLInputElement>('checkbox', { name: 'Show related' }).checked).toBe(true);
  });

  it('names the chosen Project on the picker', () => {
    renderToolbar({ project: 'p' });
    expect(screen.getByRole('button', { name: 'Project Map' })).toBeTruthy();
  });

  it('disables the Project choice without a team and the plate buttons without plates', () => {
    renderToolbar({ team: null, hasPlates: false, summary: 'Map: 3 issues' });
    expect(screen.getByRole<HTMLButtonElement>('button', { name: 'Project Choose a Project' }).disabled).toBe(true);
    expect(screen.getByRole<HTMLButtonElement>('button', { name: 'Collapse all' }).disabled).toBe(true);
    expect(screen.getByRole<HTMLButtonElement>('button', { name: 'Expand all' }).disabled).toBe(true);
    expect(screen.getByText('Map: 3 issues')).toBeTruthy();
  });

  it('offers Refresh beside the Project choice and reports it', () => {
    const props = renderToolbar({ project: 'p' });
    const buttons = screen.getAllByRole('button').map((button) => button.textContent);
    expect(buttons.indexOf('Refresh')).toBe(buttons.indexOf('Map▾') + 1);
    fireEvent.click(screen.getByRole('button', { name: 'Refresh' }));
    expect(props.onRefresh).toHaveBeenCalledOnce();
    expect(screen.queryByRole('status')).toBeNull();
  });

  it('disables Refresh without a Project and while one runs', () => {
    renderToolbar({ canRefresh: false });
    expect(screen.getByRole<HTMLButtonElement>('button', { name: 'Refresh' }).disabled).toBe(true);
  });

  it('shows what the latest refresh changed right after the summary', () => {
    renderToolbar({ summary: 'Map: 3 issues', notice: '3 changed · 1 new' });
    const notice = screen.getByRole('status');
    expect(notice.textContent).toBe('3 changed · 1 new');
    expect(notice.previousElementSibling?.textContent).toBe('Map: 3 issues');
  });
});
