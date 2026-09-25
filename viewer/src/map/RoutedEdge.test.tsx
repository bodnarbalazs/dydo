import { render } from '@testing-library/react';
import { Position, type EdgeProps } from '@xyflow/react';
import { describe, expect, it } from 'vitest';
import type { MapEdge } from '../graph/mapModel';
import type { MapFlowEdge } from '../layout/toFlow';
import { RoutedEdge, roundedPath } from './RoutedEdge';

function draw(edge: MapEdge, points: { x: number; y: number }[] | null) {
  const props = {
    id: edge.id,
    source: edge.source,
    target: edge.target,
    sourceX: 0,
    sourceY: 0,
    targetX: 100,
    targetY: 50,
    sourcePosition: Position.Right,
    targetPosition: Position.Left,
    data: { edge, points },
    markerEnd: 'url(#arrow)',
  } as unknown as EdgeProps<MapFlowEdge>;
  const { container } = render(
    <svg>
      <RoutedEdge {...props} />
    </svg>,
  );
  const path = container.querySelector('path.react-flow__edge-path');
  if (path === null) throw new Error('no edge path drawn');
  return path;
}

const blocking: MapEdge = { id: 'b', type: 'blocks', source: 's', target: 't', muted: false };

describe('RoutedEdge', () => {
  it('draws blocks solid with an arrowhead along the ELK route', () => {
    const path = draw(blocking, [
      { x: 0, y: 0 },
      { x: 50, y: 0 },
      { x: 50, y: 40 },
    ]);
    expect(path.getAttribute('d')).toBe('M 0 0 L 42 0 Q 50 0 50 8 L 50 40');
    expect(path.getAttribute('marker-end')).toBe('url(#arrow)');
    expect(path.getAttribute('class')).toContain('edge-blocks');
    expect((path as SVGPathElement).style.strokeDasharray).toBe('');
  });

  it('mutes a resolved blocker', () => {
    const path = draw({ ...blocking, muted: true }, null);
    expect((path as SVGPathElement).style.stroke).toBe('rgb(180, 188, 200)');
  });

  it('draws related dashed and falls back to a smooth step without a route', () => {
    const path = draw({ ...blocking, type: 'related' }, null);
    expect(path.getAttribute('class')).toContain('edge-related');
    expect((path as SVGPathElement).style.strokeDasharray).toBe('6 4');
    expect(path.getAttribute('d')).toMatch(/^M/);
  });
});

describe('roundedPath', () => {
  it('is empty without points and straight for two', () => {
    expect(roundedPath([])).toBe('');
    expect(roundedPath([{ x: 0, y: 0 }, { x: 10, y: 0 }])).toBe('M 0 0 L 10 0');
  });

  it('shrinks the corner radius on short segments and tolerates repeated points', () => {
    expect(roundedPath([{ x: 0, y: 0 }, { x: 4, y: 0 }, { x: 4, y: 4 }])).toBe('M 0 0 L 2 0 Q 4 0 4 2 L 4 4');
    expect(roundedPath([{ x: 0, y: 0 }, { x: 0, y: 0 }, { x: 0, y: 9 }])).toBe('M 0 0 L 0 0 Q 0 0 0 4.5 L 0 9');
  });
});
