import { Background, Controls, MiniMap, Panel, ReactFlow, useNodesInitialized, useReactFlow, type NodeMouseHandler } from '@xyflow/react';
import { useEffect, useMemo, useRef } from 'react';
import type { Issue } from '../api/types';
import type { MapFlow, MapFlowNode } from '../layout/toFlow';
import { readableColor } from './colors';
import { Legend } from './Legend';
import { MapActionsContext } from './MapActions';
import { nodeTypes } from './nodes';
import { edgeTypes } from './RoutedEdge';

interface MapCanvasProps {
  flow: MapFlow;
  focus: string | null;
  /** Changes whenever the whole map should be fitted again, e.g. a new graph or Collapse all. */
  fitKey: number;
  onFocus: (id: string) => void;
  onOpenExternal: (issue: Issue) => void;
  onTogglePlate: (id: string) => void;
}

export function MapCanvas({ flow, focus, fitKey, onFocus, onOpenExternal, onTogglePlate }: MapCanvasProps) {
  const nodes = useMemo(() => flow.nodes.map((node) => ({ ...node, selected: node.id === focus })), [flow.nodes, focus]);
  const actions = useMemo(() => ({ togglePlate: onTogglePlate }), [onTogglePlate]);

  const onNodeClick: NodeMouseHandler<MapFlowNode> = (event, node) => {
    if (event.target instanceof Element && event.target.closest('a, button')) return;
    const { issue, kind } = node.data.node;
    if (kind === 'external' && issue.project !== null) onOpenExternal(issue);
    else onFocus(issue.id);
  };

  return (
    <MapActionsContext.Provider value={actions}>
      <ReactFlow
        nodes={nodes}
        edges={flow.edges}
        nodeTypes={nodeTypes}
        edgeTypes={edgeTypes}
        onNodeClick={onNodeClick}
        nodesDraggable={false}
        nodesConnectable={false}
        elementsSelectable={false}
        minZoom={0.05}
        maxZoom={2}
      >
        <Viewport nodes={nodes} focus={focus} fitKey={fitKey} />
        <Background gap={24} color="#dfe3ea" />
        <Controls showInteractive={false} />
        <MiniMap pannable zoomable nodeColor={(node: MapFlowNode) => readableColor(node.data.node.issue.state.color)} nodeStrokeWidth={0} />
        <Panel position="bottom-left">
          <Legend />
        </Panel>
      </ReactFlow>
    </MapActionsContext.Provider>
  );
}

/** Fits the map on a new layout request and centres the focused node whenever focus moves. */
function Viewport({ nodes, focus, fitKey }: { nodes: MapFlowNode[]; focus: string | null; fitKey: number }) {
  const { fitView } = useReactFlow();
  const initialized = useNodesInitialized();
  const fitted = useRef<number | null>(null);
  const centred = useRef<string | null>(null);

  useEffect(() => {
    if (!initialized) return;
    const target = focus !== null && nodes.some((node) => node.id === focus) ? focus : null;
    if (fitted.current !== fitKey) {
      fitted.current = fitKey;
      centred.current = target;
      void (target === null ? fitView({ padding: 0.04 }) : fitView({ nodes: [{ id: target }], maxZoom: 1, duration: 0 }));
    } else if (target !== null && centred.current !== target) {
      centred.current = target;
      void fitView({ nodes: [{ id: target }], maxZoom: 1, minZoom: 0.6, duration: 300 });
    }
  }, [initialized, nodes, focus, fitKey, fitView]);
  return null;
}
