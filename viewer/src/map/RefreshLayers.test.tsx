import { fireEvent, render } from '@testing-library/react';
import { ReactFlowProvider } from '@xyflow/react';
import type { ComponentType } from 'react';
import { describe, expect, it, vi } from 'vitest';
import type { MapNode } from '../graph/mapModel';
import { makeIssue } from '../graph/testIssues';
import { ThemeContext } from '../theme/useTheme';
import type { Theme } from '../theme/theme';
import type { Cascade, NodeMark } from './cascade';
import { statusPalette } from './colors';
import { MapActionsContext } from './MapActions';
import { nodeTypes } from './nodes';
import { CascadeContext } from './RefreshLayers';

const inReview = { name: 'In Review', type: 'started', color: '#26b5ce' } as const;
const node = (kind: MapNode['kind'], overrides: Partial<MapNode> = {}): MapNode => ({
  id: 'a',
  kind,
  issue: makeIssue('a', { state: inReview }),
  parentId: null,
  pickable: false,
  closed: false,
  collapsed: false,
  descendants: kind === 'plate' ? 2 : 0,
  ...overrides,
});
const previous = (kind: MapNode['kind']) => node(kind, { issue: makeIssue('a', { title: 'Old title' }) });

function tree(shown: MapNode, mark: NodeMark | null, theme: Theme = 'light') {
  const Component = nodeTypes[shown.kind] as ComponentType<{ id: string; data: { node: MapNode }; selected: boolean }>;
  const cascade: Cascade = { nodes: new Map(mark === null ? [] : [[shown.id, mark]]), edges: new Map(), glide: false };
  return (
    <ThemeContext.Provider value={theme}>
      <CascadeContext.Provider value={cascade}>
        <ReactFlowProvider>
          <MapActionsContext.Provider value={{ togglePlate: vi.fn() }}>
            <Component id={shown.id} data={{ node: shown }} selected={false} />
          </MapActionsContext.Provider>
        </ReactFlowProvider>
      </CascadeContext.Provider>
    </ThemeContext.Provider>
  );
}

const draw = (shown: MapNode, mark: NodeMark | null, theme: Theme = 'light') => render(tree(shown, mark, theme));

const ghost = () => document.querySelector<HTMLElement>('.refresh-ghost');
const ring = () => document.querySelector<HTMLElement>('.refresh-ring');

describe('RefreshLayers', () => {
  it.each(['issue', 'external'] as const)('lays a changed %s card\'s old look over its new one and rings the card', (kind) => {
    draw(node(kind), { kind: 'changed', delay: 300, previous: previous(kind) });
    expect(ghost()?.getAttribute('aria-hidden')).toBe('true');
    expect(ghost()?.querySelector('.title')?.textContent).toBe('Old title');
    expect(document.querySelector('[data-identifier="T-a"] .title')?.textContent).toBe('Issue a');
    expect(ring()?.className).toBe('refresh-ring refresh-card');
  });

  it('lays a changed plate\'s whole old look over it, pill included, and rings its header', () => {
    draw(node('plate'), { kind: 'changed', delay: 0, previous: previous('plate') });
    const old = ghost()?.querySelector('.plate');
    expect(old?.querySelector('.plate-header .title')?.textContent).toBe('Old title');
    expect(old?.querySelector('.plate-toggle')?.textContent).toBe('▾ 2 sub-issues');
    expect(old?.querySelector('button')).toBeNull();
    expect(ring()?.className).toBe('refresh-ring refresh-header');
  });

  it('rings in the new status colour as it reads on each theme', () => {
    draw(node('issue'), { kind: 'added', delay: 0 }, 'dark');
    expect(ring()?.style.getPropertyValue('--ring')).toBe(statusPalette(inReview.color, 'dark').readable);
  });

  it('rings an added card without an old look', () => {
    draw(node('issue'), { kind: 'added', delay: 150 });
    expect(ghost()).toBeNull();
    expect(ring()).not.toBeNull();
  });

  it('drops the old look once it has faded, and only then', () => {
    // jsdom has no AnimationEvent, so React listens for the prefixed name its style object offers.
    const animationEnd = (element: Element) => fireEvent(element, new Event('webkitAnimationEnd', { bubbles: true }));
    draw(node('issue'), { kind: 'changed', delay: 0, previous: previous('issue') });
    animationEnd(ghost()!.querySelector('.issue-card')!);
    expect(ghost()).not.toBeNull();
    animationEnd(ghost()!);
    expect(ghost()).toBeNull();
    expect(ring()).not.toBeNull();
  });

  it('lays the old look over a card again when a later refresh changes it again', () => {
    const animationEnd = (element: Element) => fireEvent(element, new Event('webkitAnimationEnd', { bubbles: true }));
    const { rerender } = draw(node('issue'), { kind: 'changed', delay: 0, previous: previous('issue') });
    animationEnd(ghost()!);
    expect(ghost()).toBeNull();
    const again = node('issue', { issue: makeIssue('a', { title: 'Newer title' }) });
    rerender(tree(again, { kind: 'changed', delay: 150, previous: node('issue') }));
    expect(ghost()?.querySelector('.title')?.textContent).toBe('Issue a');
    expect(ring()).not.toBeNull();
  });

  it('draws no old look when motion is reduced, only the ring', () => {
    draw(node('issue'), { kind: 'changed', delay: 0, previous: null });
    expect(ghost()).toBeNull();
    expect(ring()).not.toBeNull();
  });

  it('draws nothing over a removed or an unmarked node', () => {
    draw(node('issue'), { kind: 'removed' });
    expect(document.querySelector('.refresh-ghost, .refresh-ring')).toBeNull();
    draw(node('plate', { id: 'b' }), null);
    expect(document.querySelector('.refresh-ghost, .refresh-ring')).toBeNull();
  });
});
