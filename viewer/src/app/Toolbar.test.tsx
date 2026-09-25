import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { Toolbar } from './Toolbar';

function renderToolbar(overrides: Partial<Parameters<typeof Toolbar>[0]> = {}) {
  const props = {
    teams: [{ id: 't', key: 'T', name: 'Team' }],
    projects: [{ id: 'p', name: 'Map', url: 'u', status: { name: 'Completed', type: 'completed' } }],
    team: 't',
    project: null,
    hasPlates: true,
    showRelated: false,
    summary: null,
    onTeam: vi.fn(),
    onProject: vi.fn(),
    onCollapseAll: vi.fn(),
    onExpandAll: vi.fn(),
    onShowRelated: vi.fn(),
    ...overrides,
  };
  render(<Toolbar {...props} />);
  return props;
}

describe('Toolbar', () => {
  it('reports each choice', () => {
    const props = renderToolbar();
    const [team, project] = screen.getAllByRole('combobox');
    fireEvent.change(team!, { target: { value: 't' } });
    fireEvent.change(project!, { target: { value: 'p' } });
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
    const selects = screen.getAllByRole<HTMLSelectElement>('combobox');
    expect(selects.map((select) => select.selectedOptions[0]?.textContent)).toEqual(['Choose a team', 'Choose a Project']);
    expect(selects.map((select) => select.selectedOptions[0]?.disabled)).toEqual([true, true]);
    expect(screen.getByText('dydo map')).toBeTruthy();
  });

  it('shows the chosen team and whether related links are shown', () => {
    renderToolbar({ showRelated: true });
    expect(screen.getAllByRole<HTMLSelectElement>('combobox')[0]?.selectedOptions[0]?.textContent).toBe('Team (T)');
    expect(screen.getByRole<HTMLInputElement>('checkbox', { name: 'Show related' }).checked).toBe(true);
  });

  it('names each Project with its status', () => {
    renderToolbar();
    expect(screen.getByRole('option', { name: 'Map · Completed' })).toBeTruthy();
  });

  it('disables the Project choice without a team and the plate buttons without plates', () => {
    renderToolbar({ team: null, hasPlates: false, summary: 'Map: 3 issues' });
    expect((screen.getAllByRole('combobox')[1] as HTMLSelectElement).disabled).toBe(true);
    expect(screen.getByRole<HTMLButtonElement>('button', { name: 'Collapse all' }).disabled).toBe(true);
    expect(screen.getByRole<HTMLButtonElement>('button', { name: 'Expand all' }).disabled).toBe(true);
    expect(screen.getByText('Map: 3 issues')).toBeTruthy();
  });
});
