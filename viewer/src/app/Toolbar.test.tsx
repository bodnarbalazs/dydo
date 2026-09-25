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

  it('names each Project with its status', () => {
    renderToolbar();
    expect(screen.getByRole('option', { name: 'Map · Completed' })).toBeTruthy();
  });

  it('disables the Project choice without a team and the plate buttons without plates', () => {
    renderToolbar({ team: null, hasPlates: false, summary: 'Map: 3 issues' });
    expect((screen.getAllByRole('combobox')[1] as HTMLSelectElement).disabled).toBe(true);
    expect(screen.getByRole<HTMLButtonElement>('button', { name: 'Collapse all' }).disabled).toBe(true);
    expect(screen.getByText('Map: 3 issues')).toBeTruthy();
  });
});
