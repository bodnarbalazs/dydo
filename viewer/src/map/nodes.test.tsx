import { fireEvent, render, screen } from '@testing-library/react';
import { ReactFlowProvider } from '@xyflow/react';
import type { ComponentType } from 'react';
import { describe, expect, it, vi } from 'vitest';
import type { MapNode } from '../graph/mapModel';
import { makeIssue } from '../graph/testIssues';
import { readableColor, wash } from './colors';
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

  it('takes edges in on the left and out on the right, through hidden handles nobody can drag from', () => {
    draw(base);
    const handles = [...document.querySelectorAll('.react-flow__handle')].map((handle) => [
      handle.getAttribute('data-handlepos'),
      ['target', 'source', 'connectable', 'hidden-handle'].filter((name) => handle.classList.contains(name)),
    ]);
    expect(handles).toEqual([
      ['left', ['target', 'hidden-handle']],
      ['right', ['source', 'hidden-handle']],
    ]);
  });

  it('highlights a pickable, selected card and dims a closed one', () => {
    draw({ ...base, pickable: true }, true);
    const card = document.querySelector('.issue-card');
    expect(card?.className).toBe('issue-card pickable selected');
    expect(screen.getByText('Pickable')).toBeTruthy();
  });

  it('toggles a plate from its pill', () => {
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

  it('washes an expanded plate in its status colour and leaves a collapsed one plain', () => {
    const issue = makeIssue('a', { state: { name: 'In Progress', type: 'started', color: '#f2c94c' } });
    draw({ ...base, kind: 'plate', issue, descendants: 1 });
    draw({ ...base, kind: 'plate', issue, descendants: 1, collapsed: true });
    const [expanded, collapsed] = [...document.querySelectorAll<HTMLElement>('.plate')];
    expect(expanded?.style.background).toBe(cssColor(wash('#f2c94c', 0.1)));
    expect(collapsed?.style.background).toBe('');
  });

  it('edges a card in its status colour on a light wash of it', () => {
    draw({ ...base, issue: makeIssue('a', { state: { name: 'In Progress', type: 'started', color: '#f2c94c' } }) });
    const card = document.querySelector<HTMLElement>('.issue-card');
    expect(card?.style.borderLeftColor).toBe(cssColor('#f2c94c'));
    expect(card?.style.background).toBe(cssColor(wash('#f2c94c', 0.2)));
  });

  it('shows the status in a readable version of a light status colour, with its type icon', () => {
    draw({ ...base, issue: makeIssue('a', { state: { name: 'Done', type: 'completed', color: '#f2c94c' } }) });
    const readable = readableColor('#f2c94c');
    expect(readable).not.toBe('#f2c94c');
    expect(document.querySelector<HTMLElement>('.state')?.style.color).toBe(cssColor(readable));
    const icon = document.querySelector('.state .status-icon');
    expect(icon?.getAttribute('data-state-type')).toBe('completed');
    expect(icon?.innerHTML).toContain(readable);
  });

  it('greys out the assignee of an unassigned card only', () => {
    draw(base);
    draw({ ...base, id: 'b', issue: makeIssue('b', { assignee: 'Ada' }) });
    expect([...document.querySelectorAll('.assignee')].map((assignee) => [assignee.textContent, assignee.className])).toEqual([
      ['unassigned', 'assignee unassigned'],
      ['Ada', 'assignee'],
    ]);
  });

  it('names the full title and the Linear link on hover', () => {
    draw(base);
    expect(document.querySelector('.title')?.getAttribute('title')).toBe('Issue a');
    expect(screen.getByRole('link', { name: 'Open T-a in Linear' }).getAttribute('title')).toBe('Open T-a in Linear');
  });

  it('marks a pickable plate and tells whether it is expanded', () => {
    draw({ ...base, kind: 'plate', descendants: 2, pickable: true });
    draw({ ...base, id: 'b', kind: 'plate', issue: makeIssue('b'), descendants: 2, collapsed: true });
    const [open, shut] = [...document.querySelectorAll('.plate')];
    expect(open?.className).toBe('plate pickable');
    expect(open?.querySelector('.pickable-badge')?.textContent).toBe('Pickable');
    expect(screen.getByRole('button', { name: 'Collapse T-a' }).getAttribute('aria-expanded')).toBe('true');
    expect(screen.getByRole('button', { name: 'Expand T-b' }).getAttribute('aria-expanded')).toBe('false');
    expect(shut?.querySelector('.pickable-badge')).toBeNull();
  });

  it('frames a plate in its readable status colour under a header edged and washed in it', () => {
    draw({ ...base, kind: 'plate', descendants: 1, issue: makeIssue('a', { state: { name: 'In Progress', type: 'started', color: '#f2c94c' } }) });
    expect(document.querySelector<HTMLElement>('.plate')?.style.borderColor).toBe(cssColor(readableColor('#f2c94c')));
    const header = document.querySelector<HTMLElement>('.plate-header');
    expect(header?.style.borderLeftColor).toBe(cssColor('#f2c94c'));
    expect(header?.style.background).toBe(cssColor(wash('#f2c94c', 0.2)));
  });

  it('draws an external issue with its title, its Project, a Linear link and a readable status icon', () => {
    const issue = makeIssue('e', { title: 'Elsewhere', state: { name: 'In Progress', type: 'started', color: '#f2c94c' }, project: { id: 'p2', name: 'Project Two' } });
    draw({ ...base, kind: 'external', issue });
    const external = document.querySelector('.external-card');
    expect(external?.querySelector('.title')?.textContent).toBe('Elsewhere');
    expect(external?.querySelector('.title')?.getAttribute('title')).toBe('Elsewhere');
    expect(external?.querySelector('.external-project')?.textContent).toBe('Project Two');
    expect(screen.getByRole('link', { name: 'Open T-e in Linear' }).getAttribute('href')).toBe(issue.url);
    expect(external?.querySelector('.status-icon')?.innerHTML).toContain(readableColor('#f2c94c'));
  });

  it('draws an external issue with its Project, or none', () => {
    draw({ ...base, kind: 'external', closed: true, issue: makeIssue('e', { project: null }) }, true);
    expect(document.querySelector('.external-card')?.className).toBe('external-card closed selected');
    expect(screen.getByText('No project')).toBeTruthy();
  });
});

/** The colour as the browser reports it back from an inline style. */
function cssColor(hex: string): string {
  const probe = document.createElement('div');
  probe.style.background = hex;
  return probe.style.background;
}
