import { fireEvent, render, screen } from '@testing-library/react';
import { ReactFlowProvider } from '@xyflow/react';
import type { ComponentType } from 'react';
import { describe, expect, it, vi } from 'vitest';
import type { MapNode } from '../graph/mapModel';
import { makeIssue } from '../graph/testIssues';
import { MapActionsContext } from './MapActions';
import { nodeTypes } from './nodes';

function draw(node: MapNode, selected = false, togglePlate = vi.fn()) {
  const Component = nodeTypes[node.kind] as ComponentType<{ id: string; data: { node: MapNode }; selected: boolean }>;
  render(
    <ReactFlowProvider>
      <MapActionsContext.Provider value={{ togglePlate }}>
        <Component id={node.id} data={{ node }} selected={selected} />
      </MapActionsContext.Provider>
    </ReactFlowProvider>,
  );
  return togglePlate;
}

const base: MapNode = { id: 'a', kind: 'issue', issue: makeIssue('a'), parentId: null, pickable: false, closed: false, collapsed: false, descendants: 0 };

describe('map nodes', () => {
  it('shows identifier, title, exact status name, assignee and a Linear button on an issue card', () => {
    draw({ ...base, issue: makeIssue('a', { assignee: 'Ada', state: { name: 'Ready to Merge', type: 'started', color: '#4cb782' } }) });
    expect(screen.getByText('T-a')).toBeTruthy();
    expect(screen.getByText('Issue a')).toBeTruthy();
    expect(screen.getByText('Ready to Merge')).toBeTruthy();
    expect(screen.getByText('Ada')).toBeTruthy();
    const link = screen.getByRole('link', { name: 'Open T-a in Linear' });
    expect(link.getAttribute('rel')).toBe('noopener');
  });

  it('highlights a pickable, selected card and dims a closed one', () => {
    draw({ ...base, pickable: true }, true);
    const card = document.querySelector('.issue-card');
    expect(card?.className).toBe('issue-card pickable selected');
    expect(screen.getByText('Pickable')).toBeTruthy();
  });

  it('toggles a plate from its pill without selecting it', () => {
    const toggle = draw({ ...base, kind: 'plate', descendants: 1, closed: true });
    expect(document.querySelector('.plate')?.className).toBe('plate closed');
    fireEvent.click(screen.getByRole('button', { name: 'Collapse T-a' }));
    expect(toggle).toHaveBeenCalledWith('a');
    expect(screen.getByText(/1 sub-issue$/)).toBeTruthy();
  });

  it('marks a collapsed plate and counts its hidden sub-issues', () => {
    draw({ ...base, kind: 'plate', collapsed: true, descendants: 4 }, true);
    expect(document.querySelector('.plate')?.className).toBe('plate collapsed selected');
    expect(screen.getByRole('button', { name: 'Expand T-a' }).textContent).toBe('▸ 4 sub-issues');
  });

  it('draws an external issue with its Project, or none', () => {
    draw({ ...base, kind: 'external', closed: true, issue: makeIssue('e', { project: null }) }, true);
    expect(document.querySelector('.external-card')?.className).toBe('external-card closed selected');
    expect(screen.getByText('No project')).toBeTruthy();
  });
});
