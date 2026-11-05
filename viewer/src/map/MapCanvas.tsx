import { Background, BackgroundVariant, Controls, MiniMap, Panel, ReactFlow, useReactFlow, useStore, type NodeMouseHandler } from '@xyflow/react';
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
  onReady?: () => void;
  /** Called once the canvas draws this flow's nodes, an effect after they are handed down. */
  onDrawn: () => void;
  onFocus: (id: string) => void;
  onOpenExternal: (issue: Issue) => void;
  onTogglePlate: (id: string) => void;
}

export function MapCanvas({ flow, focus, fitKey, cascade, onReady, onDrawn, onFocus, onOpenExternal, onTogglePlate }: MapCanvasProps) {
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
          <Viewport nodes={nodes} focus={focus} fitKey={fitKey} onReady={onReady} />
          <Drawn nodes={nodes} onDrawn={onDrawn} />
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

/** React Flow takes the nodes it is handed into its store, and draws them, only in an effect after that render. */
function Drawn({ nodes, onDrawn }: { nodes: MapFlowNode[]; onDrawn: () => void }) {
  const drawn = useStore((state) => state.nodes);
  useEffect(() => {
    if (drawn === nodes) onDrawn();
  }, [drawn, nodes, onDrawn]);
  return null;
}

/** Fits the map on a new layout request and centres the focused node whenever focus moves. */
function Viewport({ nodes, focus, fitKey, onReady }: { nodes: MapFlowNode[]; focus: string | null; fitKey: number; onReady: (() => void) | undefined }) {
  const { fitView } = useReactFlow();
  const fitted = useRef<{ key: number; ready: Promise<boolean> } | null>(null);
  const centred = useRef<string | null>(null);

  useEffect(() => {
    let live = true;
    let frame = 0;
    const target = nodes.some((node) => node.id === focus) ? focus : null;
    if (nodes.length === 0) {
      // React Flow defers an empty fit indefinitely; only populated layouts need fitting.
      fitted.current = null;
    } else if (fitted.current?.key !== fitKey) {
      centred.current = target;
      const fit = target === null ? fitView({ padding: 0.04 }) : fitView({ nodes: [{ id: target }], maxZoom: 1, duration: 0 });
      fitted.current = { key: fitKey, ready: fit };
    } else if (target !== null && centred.current !== target) {
      centred.current = target;
      void fitView({ nodes: [{ id: target }], maxZoom: 1, minZoom: 0.6, duration: 300 });
    }
    void (fitted.current?.ready ?? Promise.resolve(false)).then(() => {
      if (live) frame = requestAnimationFrame(() => {
        frame = requestAnimationFrame(() => { if (live) onReady?.(); });
      });
    });
    return () => { live = false; cancelAnimationFrame(frame); };
  }, [nodes, focus, fitKey, fitView, onReady]);
  return null;
}
