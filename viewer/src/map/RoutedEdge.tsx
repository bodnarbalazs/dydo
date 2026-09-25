import { BaseEdge, getSmoothStepPath, type EdgeProps, type XYPosition } from '@xyflow/react';
import { EDGE_COLORS, type MapFlowEdge } from '../layout/toFlow';

/** Draws ELK's orthogonal route with rounded corners; an unrouted edge falls back to a smooth step. */
export function RoutedEdge(props: EdgeProps<MapFlowEdge>) {
  const { id, data, markerEnd, sourceX, sourceY, targetX, targetY, sourcePosition, targetPosition } = props;
  const path =
    data?.points == null
      ? getSmoothStepPath({ sourceX, sourceY, targetX, targetY, sourcePosition, targetPosition })[0]
      : roundedPath(data.points);
  const edge = data?.edge;
  const related = edge?.type === 'related';
  const color = related ? EDGE_COLORS.related : edgeColor(edge?.muted === true);
  return (
    <BaseEdge
      id={id}
      path={path}
      {...(markerEnd === undefined ? {} : { markerEnd })}
      className={related ? 'edge-related' : 'edge-blocks'}
      style={{ stroke: color, strokeWidth: related ? 1.4 : 1.8, ...(related ? { strokeDasharray: '6 4' } : {}) }}
    />
  );
}

function edgeColor(muted: boolean): string {
  return muted ? EDGE_COLORS.muted : EDGE_COLORS.blocks;
}

const RADIUS = 8;

export function roundedPath(points: XYPosition[]): string {
  const [first, ...rest] = points;
  if (first === undefined) return '';
  let path = `M ${first.x} ${first.y}`;
  rest.forEach((point, index) => {
    const previous = points[index] ?? first;
    const next = rest[index + 1];
    if (next === undefined) {
      path += ` L ${point.x} ${point.y}`;
      return;
    }
    const inset = (from: XYPosition, to: XYPosition): XYPosition => {
      const length = Math.hypot(to.x - from.x, to.y - from.y);
      const r = Math.min(RADIUS, length / 2);
      return length === 0 ? from : { x: from.x + ((to.x - from.x) * r) / length, y: from.y + ((to.y - from.y) * r) / length };
    };
    const before = inset(point, previous);
    const after = inset(point, next);
    path += ` L ${before.x} ${before.y} Q ${point.x} ${point.y} ${after.x} ${after.y}`;
  });
  return path;
}

export const edgeTypes = { routed: RoutedEdge };
