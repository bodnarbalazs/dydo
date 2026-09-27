import { render, type RenderResult } from '@testing-library/react';
import { ReactFlowProvider, useStoreApi } from '@xyflow/react';
import ELK from 'elkjs/lib/elk.bundled.js';
import { useEffect } from 'react';
import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';
import { buildMapModel } from '../graph/mapModel';
import { makeGraph, makeIssue } from '../graph/testIssues';
import { layoutMap } from '../layout/layout';
import type { MapFlow } from '../layout/toFlow';
import { ThemeContext } from '../theme/useTheme';
import { readableOnDark } from './colors';
import { MapCanvas } from './MapCanvas';

const fitView = vi.fn((options?: object) => Promise.resolve(options !== undefined));

vi.mock('@xyflow/react', async (original) => ({ ...(await original<typeof import('@xyflow/react')>()), useReactFlow: () => ({ fitView }) }));

const graph = makeGraph([makeIssue('A'), makeIssue('B')]);
let flow: MapFlow;

beforeAll(async () => {
  flow = await layoutMap(new ELK(), buildMapModel(graph, { collapsed: new Set(), showRelated: false }));
});

beforeEach(() => {
  fitView.mockClear();
});

let store: ReturnType<typeof useStoreApi> | null = null;

/** Shares the canvas's provider, so the test can read the settings React Flow was given. */
function StoreProbe() {
  const api = useStoreApi();
  useEffect(() => {
    store = api;
  }, [api]);
  return null;
}

function canvas(focus: string | null, fitKey: number, shown: MapFlow = flow) {
  return (
    <ReactFlowProvider>
      <MapCanvas flow={shown} focus={focus} fitKey={fitKey} onFocus={vi.fn()} onOpenExternal={vi.fn()} onTogglePlate={vi.fn()} />
      <StoreProbe />
    </ReactFlowProvider>
  );
}

/** The same map laid out again, as a Show related toggle or a plate toggle produces. */
const relaidOut = () => ({ nodes: flow.nodes.map((node) => ({ ...node })), edges: [...flow.edges] });

describe('MapCanvas viewport', () => {
  it('fits the whole map when nothing is focused', () => {
    render(canvas(null, 0));
    expect(fitView.mock.calls).toEqual([[{ padding: 0.04 }]]);
  });

  it('fits the whole map when the focus names no node on it', () => {
    render(canvas('gone', 0));
    expect(fitView.mock.calls).toEqual([[{ padding: 0.04 }]]);
  });

  it('opens on the focused node', () => {
    render(canvas('A', 0));
    expect(fitView.mock.calls).toEqual([[{ nodes: [{ id: 'A' }], maxZoom: 1, duration: 0 }]]);
  });

  it('centres the focus again only when it moves to another node', () => {
    const view: RenderResult = render(canvas('A', 0));
    fitView.mockClear();
    view.rerender(canvas('A', 0, relaidOut()));
    expect(fitView).not.toHaveBeenCalled();
    view.rerender(canvas('B', 0, relaidOut()));
    expect(fitView.mock.calls).toEqual([[{ nodes: [{ id: 'B' }], maxZoom: 1, minZoom: 0.6, duration: 300 }]]);
    view.rerender(canvas('B', 0, relaidOut()));
    view.rerender(canvas('gone', 0, relaidOut()));
    expect(fitView).toHaveBeenCalledOnce();
  });

  it('fits the map again when asked to', () => {
    const view = render(canvas('A', 0));
    fitView.mockClear();
    view.rerender(canvas(null, 1, relaidOut()));
    expect(fitView.mock.calls).toEqual([[{ padding: 0.04 }]]);
  });
});

describe('MapCanvas chrome', () => {
  it('is a read-only map that zooms from 0.05 to 2', () => {
    render(canvas(null, 0));
    const { minZoom, maxZoom, nodesDraggable, nodesConnectable, elementsSelectable } = store!.getState();
    expect({ minZoom, maxZoom, nodesDraggable, nodesConnectable, elementsSelectable }).toEqual({
      minZoom: 0.05,
      maxZoom: 2,
      nodesDraggable: false,
      nodesConnectable: false,
      elementsSelectable: false,
    });
  });

  it('draws graph paper with a faint 24 px grid under a stronger 120 px one, zoom controls without the lock, and the legend bottom left', () => {
    render(canvas(null, 0));
    const grids = [...document.querySelectorAll<SVGElement>('.react-flow__background')].map((background) => [
      background.style.getPropertyValue('--xy-background-pattern-color-props'),
      background.querySelector('pattern')?.getAttribute('width'),
      background.querySelector('path')?.classList.contains('lines'),
    ]);
    expect(grids).toEqual([
      ['var(--grid-minor)', '24', true],
      ['var(--grid-major)', '120', true],
    ]);
    expect([...document.querySelectorAll('.react-flow__controls button')].map((button) => button.getAttribute('aria-label'))).toEqual(['Zoom In', 'Zoom Out', 'Fit View']);
    expect(document.querySelector('[aria-label="Legend"]')?.parentElement?.className).toBe('react-flow__panel bottom left');
  });

  it('shows each issue on the minimap in its readable status colour, without outline', () => {
    render(canvas(null, 0));
    const nodes = [...document.querySelectorAll<SVGRectElement>('.react-flow__minimap-node')];
    expect(nodes).toHaveLength(2);
    // makeIssue's #e2e2e2 is too light to read, so the minimap uses its slate mix.
    expect(nodes.map((node) => [node.style.fill, node.style.strokeWidth])).toEqual([
      ['rgb(157, 166, 178)', '0'],
      ['rgb(157, 166, 178)', '0'],
    ]);
  });

  it('themes React Flow and shows the minimap in dark readable colours in the dark theme', () => {
    render(<ThemeContext.Provider value="dark">{canvas(null, 0)}</ThemeContext.Provider>);
    expect(document.querySelector('.react-flow')?.classList.contains('dark')).toBe(true);
    const fills = [...document.querySelectorAll<SVGRectElement>('.react-flow__minimap-node')].map((node) => node.style.fill);
    const probe = document.createElement('div');
    probe.style.color = readableOnDark('#e2e2e2');
    expect(fills).toEqual([probe.style.color, probe.style.color]);
  });

  it('keeps React Flow light in the light theme', () => {
    render(canvas(null, 0));
    expect(document.querySelector('.react-flow')?.classList.contains('light')).toBe(true);
  });
});
