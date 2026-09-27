import { Background, BackgroundVariant, Controls, MiniMap, Panel, ReactFlow, useReactFlow, type NodeMouseHandler } from '@xyflow/react';
import { useEffect, useMemo, useRef } from 'react';
import type { Issue } from '../api/types';
import type { MapFlow, MapFlowNode } from '../layout/toFlow';
import { useTheme } from '../theme/useTheme';
import { statusPalette } from './colors';
import { TIMING, type Cascade, type EdgeMark, type NodeMark } from './cascade';
import { Legend } from './Legend';
import { MapActionsContext } from './MapActions';
import { nodeTypes } from './nodes';
import { CascadeContext } from './RefreshLayers';
import { edgeTypes } from './RoutedEdge';

interface MapCanvasProps {
  flow: MapFlow;
  focus: string | null;
  /** Changes whenever the whole map should be fitted again, e.g. a new graph or Collapse all. */
  fitKey: number;
  /** The latest refresh's marks, or null when nothing is animating. */
  cascade: Cascade | null;
  onFocus: (id: string) => void;
  onOpenExternal: (issue: Issue) => void;
  onTogglePlate: (id: string) => void;
}

export function MapCanvas({ flow, focus, fitKey, cascade, onFocus, onOpenExternal, onTogglePlate }: MapCanvasProps) {
  const nodes = useMemo(
    () => flow.nodes.map((node) => ({ ...node, selected: node.id === focus, ...marked(cascade?.nodes.get(node.id)) })),
    [flow.nodes, focus, cascade],
  );
  const edges = useMemo(() => flow.edges.map((edge) => ({ ...edge, ...marked(cascade?.edges.get(edge.id)) })), [flow.edges, cascade]);
  const actions = useMemo(() => ({ togglePlate: onTogglePlate }), [onTogglePlate]);
  const theme = useTheme();

  const onNodeClick: NodeMouseHandler<MapFlowNode> = (_event, node) => {
    const { issue, kind } = node.data.node;
    if (kind === 'external' && issue.project !== null) onOpenExternal(issue);
    else onFocus(issue.id);
  };

  return (
    <MapActionsContext.Provider value={actions}>
      <CascadeContext.Provider value={cascade}>
        <ReactFlow
          nodes={nodes}
          edges={edges}
          nodeTypes={nodeTypes}
          edgeTypes={edgeTypes}
          onNodeClick={onNodeClick}
          nodesDraggable={false}
          nodesConnectable={false}
          elementsSelectable={false}
          minZoom={0.05}
          maxZoom={2}
          colorMode={theme}
          className={cascade?.glide === true ? 'refresh-glide' : undefined}
          style={TIMING_PROPERTIES}
        >
          <Viewport nodes={nodes} focus={focus} fitKey={fitKey} />
          {/* Graph paper: a faint line every 24 px under a stronger one every five cells. */}
          <Background id="minor" variant={BackgroundVariant.Lines} gap={24} color="var(--grid-minor)" />
          <Background id="major" variant={BackgroundVariant.Lines} gap={120} color="var(--grid-major)" />
          <Controls showInteractive={false} />
          <MiniMap pannable zoomable nodeColor={(node: MapFlowNode) => statusPalette(node.data.node.issue.state.color, theme).frame} nodeStrokeWidth={0} />
          <Panel position="bottom-left">
            <Legend />
          </Panel>
        </ReactFlow>
      </CascadeContext.Provider>
    </MapActionsContext.Provider>
  );
}

const TIMING_PROPERTIES = Object.fromEntries(
  Object.entries(TIMING).map(([name, ms]) => [`--refresh-${name}`, `${String(ms)}ms`]),
) as React.CSSProperties;

/** A node's or link's part in a refresh, as a class and its turn in the cascade, for styles.css. */
function marked(mark: NodeMark | EdgeMark | undefined): { className?: string; style?: React.CSSProperties } {
  if (mark === undefined) return {};
  const delay = mark.kind === 'removed' ? {} : { style: { '--refresh-delay': `${String(mark.delay)}ms` } as React.CSSProperties };
  return { className: `refresh-${mark.kind}`, ...delay };
}

/** Fits the map on a new layout request and centres the focused node whenever focus moves. */
function Viewport({ nodes, focus, fitKey }: { nodes: MapFlowNode[]; focus: string | null; fitKey: number }) {
  const { fitView } = useReactFlow();
  const fitted = useRef<number | null>(null);
  const centred = useRef<string | null>(null);

  useEffect(() => {
    const target = nodes.some((node) => node.id === focus) ? focus : null;
    if (fitted.current !== fitKey) {
      fitted.current = fitKey;
      centred.current = target;
      void (target === null ? fitView({ padding: 0.04 }) : fitView({ nodes: [{ id: target }], maxZoom: 1, duration: 0 }));
    } else if (target !== null && centred.current !== target) {
      centred.current = target;
      void fitView({ nodes: [{ id: target }], maxZoom: 1, minZoom: 0.6, duration: 300 });
    }
  }, [nodes, focus, fitKey, fitView]);
  return null;
}
