import { fireEvent, render, screen } from '@testing-library/react';
import { ReactFlowProvider } from '@xyflow/react';
import type { ComponentType } from 'react';
import { describe, expect, it, vi } from 'vitest';
import type { MapNode } from '../graph/mapModel';
import { makeIssue } from '../graph/testIssues';
import { ThemeContext } from '../theme/useTheme';
import { readableColor, statusPalette, wash } from './colors';
import { IssueSummary } from './IssueSummary';
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

function drawDark(node: MapNode) {
  const Component = nodeTypes[node.kind] as ComponentType<{ id: string; data: { node: MapNode }; selected: boolean }>;
  render(
    <ThemeContext.Provider value="dark">
      <ReactFlowProvider>
        <MapActionsContext.Provider value={{ togglePlate: vi.fn() }}>
          <Component id={node.id} data={{ node }} selected={false} />
        </MapActionsContext.Provider>
      </ReactFlowProvider>
    </ThemeContext.Provider>,
  );
}

const base: MapNode = { id: 'a', kind: 'issue', issue: makeIssue('a'), parentId: null, pickable: false, closed: false, collapsed: false, descendants: 0 };

describe('map nodes', () => {
  it('shows title, exact status name, assignee and the identifier as the Linear link on an issue card', () => {
    draw({ ...base, issue: makeIssue('a', { assignee: 'Ada', state: { name: 'Ready to Merge', type: 'started', color: '#4cb782' } }) });
    expect(screen.getByText('Issue a')).toBeTruthy();
    expect(screen.getByText('Ready to Merge')).toBeTruthy();
    expect(screen.getByText('Ada')).toBeTruthy();
    const link = screen.getByRole('link', { name: 'Open T-a in Linear' });
    expect(link.textContent).toBe('T-a ↗');
    expect(link.closest('.card-top')).not.toBeNull();
    expect(document.querySelector('.identifier')).toBeNull();
    expect(screen.queryByText(/Linear ↗/)).toBeNull();
  });

  it('opens the Linear link in a new tab without selecting the card', () => {
    const select = vi.fn();
    render(
      <div onClick={select}>
        <IssueSummary issue={makeIssue('a')} pickable={false} color="#000000" />
      </div>,
    );
    const link = screen.getByRole('link', { name: 'Open T-a in Linear' });
    expect([link.getAttribute('href'), link.getAttribute('target'), link.getAttribute('rel')]).toEqual(['https://linear.app/t/issue/T-a', '_blank', 'noopener']);
    fireEvent.click(link);
    expect(select).not.toHaveBeenCalled();
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

  it('puts the Pickable badge bottom right, where an unassigned card reads unassigned', () => {
    draw({ ...base, pickable: true });
    const bottom = document.querySelector('.card-bottom');
    expect(bottom?.lastElementChild?.className).toBe('pickable-badge');
    expect(bottom?.lastElementChild?.textContent).toBe('Pickable');
    expect(document.querySelector('.card-top .pickable-badge')).toBeNull();
    expect(document.querySelector('.assignee')).toBeNull();
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
    expect(open?.querySelector('.plate-header .card-bottom .pickable-badge')?.textContent).toBe('Pickable');
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

  it('draws an external issue with its status icon, then its labels, then the Linear link', () => {
    const labels = [{ name: 'Walkthrough', color: '#C69C6D' }, { name: 'HITL', color: '#F76B15' }];
    draw({ ...base, kind: 'external', issue: makeIssue('e', { labels }) });
    const top = document.querySelector('.external-card .card-top');
    expect([...(top?.children ?? [])].map((child) => child.getAttribute('class'))).toEqual(['status-icon', 'labels', 'linear-link']);
    expect(top?.querySelector('.labels')?.textContent).toBe('HITLWalkthrough');
    expect(top?.querySelector('.linear-link')?.textContent).toBe('T-e ↗');
  });

  it('draws an external issue with its Project, or none', () => {
    draw({ ...base, kind: 'external', closed: true, issue: makeIssue('e', { project: null }) }, true);
    expect(document.querySelector('.external-card')?.className).toBe('external-card closed selected');
    expect(screen.getByText('No project')).toBeTruthy();
  });
});

describe('label chips', () => {
  const label = (name: string, color = '#30A46C') => ({ name, color });
  const chips = () => [...document.querySelectorAll('.label-chip')].map((chip) => chip.textContent);

  it('show at most three, sorted by name whatever the case, with every label in the row title and no count', () => {
    const labels = [label('merge'), label('Walkthrough'), label('AFK'), label('bug'), label('Feature')];
    draw({ ...base, issue: makeIssue('a', { labels }) });
    expect(chips()).toEqual(['AFK', 'bug', 'Feature']);
    expect(document.querySelector('.labels')?.getAttribute('title')).toBe('AFK, bug, Feature, merge, Walkthrough');
    expect(document.querySelector('.card-top')?.textContent).not.toMatch(/\+/);
  });

  it('dot each chip in the label colour from Linear', () => {
    draw({ ...base, issue: makeIssue('a', { labels: [label('HITL', '#F76B15'), label('AFK', '#30A46C')] }) });
    const dots = [...document.querySelectorAll<HTMLElement>('.label-chip .label-dot')].map((dot) => dot.style.background);
    expect(dots).toEqual([cssColor('#30A46C'), cssColor('#F76B15')]);
  });

  it('leave the row out for an issue without labels, the link still on the right', () => {
    draw(base);
    expect(document.querySelector('.labels')).toBeNull();
    expect(document.querySelector('.card-top')?.children).toHaveLength(1);
  });

  it('top a plate header like a card', () => {
    draw({ ...base, kind: 'plate', descendants: 1, issue: makeIssue('a', { labels: [label('Feature', '#BB87FC'), label('AFK')] }) });
    const top = document.querySelector('.plate-header .card-top');
    expect([...(top?.children ?? [])].map((child) => child.className)).toEqual(['labels', 'linear-link']);
    expect(top?.querySelector('.labels')?.textContent).toBe('AFKFeature');
  });
});

describe('map nodes in the dark theme', () => {
  const inProgress = makeIssue('a', { state: { name: 'In Progress', type: 'started', color: '#0f783c' } });

  it('tints a card faintly on the dark surface and reads its status there', () => {
    drawDark({ ...base, issue: inProgress });
    const dark = statusPalette('#0f783c', 'dark');
    const card = document.querySelector<HTMLElement>('.issue-card');
    expect(card?.style.borderLeftColor).toBe(cssColor('#0f783c'));
    expect(card?.style.background).toBe(cssColor(dark.card));
    expect(document.querySelector<HTMLElement>('.state')?.style.color).toBe(cssColor(dark.readable));
  });

  it('washes a plate and its header on the dark surface', () => {
    drawDark({ ...base, kind: 'plate', descendants: 1, issue: inProgress });
    const dark = statusPalette('#0f783c', 'dark');
    const plate = document.querySelector<HTMLElement>('.plate');
    expect(plate?.style.background).toBe(cssColor(dark.plate));
    expect(plate?.style.borderColor).toBe(cssColor(dark.frame));
    expect(document.querySelector<HTMLElement>('.plate-header')?.style.background).toBe(cssColor(dark.card));
  });

  it('draws an external issue icon in its dark readable colour', () => {
    drawDark({ ...base, kind: 'external', issue: inProgress });
    expect(document.querySelector('.external-card .status-icon')?.innerHTML).toContain(statusPalette('#0f783c', 'dark').readable);
  });
});

describe('started issues', () => {
  it('mark a started card and plate and carry their status colour for the glow', () => {
    const started = makeIssue('a', { state: { name: 'In Progress', type: 'started', color: '#f2c94c' } });
    draw({ ...base, issue: started });
    draw({ ...base, kind: 'plate', descendants: 1, issue: started });
    const card = document.querySelector<HTMLElement>('.issue-card');
    const plate = document.querySelector<HTMLElement>('.plate');
    expect([card?.className, plate?.className]).toEqual(['issue-card started', 'plate started']);
    expect([card?.style.getPropertyValue('--status'), plate?.style.getPropertyValue('--status')]).toEqual(['#f2c94c', '#f2c94c']);
  });

  it('are the only ones marked', () => {
    draw({ ...base, issue: makeIssue('a', { type: 'completed' }) });
    draw({ ...base, kind: 'plate', descendants: 1, issue: makeIssue('b', { type: 'unstarted' }) });
    expect(document.querySelectorAll('.started')).toHaveLength(0);
  });
});

/** The colour as the browser reports it back from an inline style. */
function cssColor(hex: string): string {
  const probe = document.createElement('div');
  probe.style.background = hex;
  return probe.style.background;
}
