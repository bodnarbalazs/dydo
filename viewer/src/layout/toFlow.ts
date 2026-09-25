import { MarkerType, type Edge, type Node, type XYPosition } from '@xyflow/react';
import type { ElkExtendedEdge, ElkNode } from 'elkjs/lib/elk-api';
import { COMPONENT_PREFIX, type MapEdge, type MapModel, type MapNode, type MapNodeKind } from '../graph/mapModel';

export const EDGE_COLORS = { blocks: '#334155', muted: '#b4bcc8', related: '#8b5cf6' } as const;

export type MapFlowNode = Node<{ node: MapNode }, MapNodeKind>;
export type MapFlowEdge = Edge<{ edge: MapEdge; points: XYPosition[] | null }, 'routed'>;

export interface MapFlow {
  nodes: MapFlowNode[];
  edges: MapFlowEdge[];
}

/** Places the model's nodes where ELK put them; ELK's routes become edge bend points. */
export function toFlow(model: MapModel, laidOut: ElkNode): MapFlow {
  const shapes = new Map<string, ElkNode>();
  const routes = new Map<string, XYPosition[]>();
  collect(laidOut, shapes, routes);

  const nodes = model.nodes.map((node): MapFlowNode => {
    const shape = shapes.get(node.id);
    const size = { width: shape?.width ?? 0, height: shape?.height ?? 0 };
    return {
      id: node.id,
      type: node.kind,
      position: { x: shape?.x ?? 0, y: shape?.y ?? 0 },
      ...size,
      // ELK already sized every node, so React Flow need not wait for a measurement to fit the view.
      measured: size,
      data: { node },
      draggable: false,
      connectable: false,
      ...(node.parentId === null ? {} : { parentId: node.parentId }),
    };
  });
  const edges = model.edges.map(
    (edge): MapFlowEdge => ({
      id: edge.id,
      type: 'routed',
      source: edge.source,
      target: edge.target,
      data: { edge, points: routes.get(edge.id) ?? null },
      focusable: false,
      ...(edge.type === 'blocks'
        ? { markerEnd: { type: MarkerType.ArrowClosed, width: 16, height: 16, color: edge.muted ? EDGE_COLORS.muted : EDGE_COLORS.blocks } }
        : {}),
      zIndex: 1,
    }),
  );
  return { nodes, edges };
}

function collect(node: ElkNode, shapes: Map<string, ElkNode>, routes: Map<string, XYPosition[]>, offset: XYPosition = { x: 0, y: 0 }): void {
  node.edges?.forEach((edge: ElkExtendedEdge) => {
    const points = (edge.sections ?? []).flatMap((section) => [section.startPoint, ...(section.bendPoints ?? []), section.endPoint]);
    if (points.length > 0) routes.set(edge.id, points);
  });
  node.children?.forEach((child) => {
    const x = child.x ?? 0;
    const y = child.y ?? 0;
    // Component wrappers are not drawn; their top-level nodes move by the wrapper's offset.
    if (child.id.startsWith(COMPONENT_PREFIX)) collect(child, shapes, routes, { x, y });
    else {
      shapes.set(child.id, { ...child, x: x + offset.x, y: y + offset.y });
      collect(child, shapes, routes);
    }
  });
}
