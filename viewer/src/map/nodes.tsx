import { Handle, Position, type NodeProps } from '@xyflow/react';
import type { ReactNode } from 'react';
import type { Issue } from '../api/types';
import type { MapNode } from '../graph/mapModel';
import type { MapFlowNode } from '../layout/toFlow';
import { useTheme } from '../theme/useTheme';
import { statusPalette } from './colors';
import { IssueSummary, LinearLink } from './IssueSummary';
import { LabelChips } from './LabelChips';
import { useMapActions } from './MapActions';
import { RefreshLayers } from './RefreshLayers';
import { StatusIcon } from './StatusIcon';

function Handles() {
  return (
    <>
      <Handle type="target" position={Position.Left} isConnectable={false} className="hidden-handle" />
      <Handle type="source" position={Position.Right} isConnectable={false} className="hidden-handle" />
    </>
  );
}

function classes(...names: (string | false)[]): string {
  return names.filter(Boolean).join(' ');
}

/** A started issue carries its status colour for the stylesheet's glow. */
function started(issue: Issue): React.CSSProperties {
  return issue.state.type === 'started' ? ({ '--status': issue.state.color } as React.CSSProperties) : {};
}

function IssueCard({ node, selected, children }: { node: MapNode; selected: boolean; children?: ReactNode }) {
  const { issue, pickable, closed } = node;
  const palette = statusPalette(issue.state.color, useTheme());
  return (
    <div
      className={classes('issue-card', issue.state.type === 'started' && 'started', closed && 'closed', pickable && 'pickable', selected && 'selected')}
      style={{ borderLeftColor: issue.state.color, background: palette.card, ...started(issue) }}
      data-identifier={issue.identifier}
    >
      {children}
      <IssueSummary issue={issue} pickable={pickable} color={palette.readable} />
    </div>
  );
}

function IssueNode({ id, data, selected }: NodeProps<MapFlowNode>) {
  const ring = statusPalette(data.node.issue.state.color, useTheme()).readable;
  return (
    <>
      <IssueCard node={data.node} selected={selected}>
        <Handles />
      </IssueCard>
      <RefreshLayers id={id} ring={ring} region="card" drawOld={(previous) => <IssueCard node={previous} selected={selected} />} />
    </>
  );
}

function PlateHeader({ node }: { node: MapNode }) {
  const { issue, pickable } = node;
  const palette = statusPalette(issue.state.color, useTheme());
  return (
    <div className="plate-header" style={{ borderLeftColor: issue.state.color, background: palette.card }}>
      <IssueSummary issue={issue} pickable={pickable} color={palette.readable} />
    </div>
  );
}

function Plate({ node, selected, children }: { node: MapNode; selected: boolean; children: ReactNode }) {
  const { issue, pickable, closed, collapsed } = node;
  const palette = statusPalette(issue.state.color, useTheme());
  return (
    <div
      className={classes(
        'plate',
        issue.state.type === 'started' && 'started',
        collapsed && 'collapsed',
        closed && 'closed',
        pickable && 'pickable',
        selected && 'selected',
      )}
      style={{ borderColor: palette.frame, background: collapsed ? undefined : palette.plate, ...started(issue) }}
      data-identifier={issue.identifier}
    >
      {children}
    </div>
  );
}

function pill({ collapsed, descendants }: MapNode): string {
  return `${collapsed ? '▸' : '▾'} ${String(descendants)} sub-issue${descendants === 1 ? '' : 's'}`;
}

function PlateNode({ id, data, selected }: NodeProps<MapFlowNode>) {
  const { issue, collapsed } = data.node;
  const { togglePlate } = useMapActions();
  const ring = statusPalette(issue.state.color, useTheme()).readable;
  return (
    <>
      <Plate node={data.node} selected={selected}>
        <Handles />
        <PlateHeader node={data.node} />
        <button
          type="button"
          className="plate-toggle"
          aria-expanded={!collapsed}
          aria-label={`${collapsed ? 'Expand' : 'Collapse'} ${issue.identifier}`}
          onClick={() => togglePlate(issue.id)}
        >
          {pill(data.node)}
        </button>
      </Plate>
      <RefreshLayers
        id={id}
        ring={ring}
        region="header"
        drawOld={(previous) => (
          <Plate node={previous} selected={selected}>
            <PlateHeader node={previous} />
            <span className="plate-toggle">{pill(previous)}</span>
          </Plate>
        )}
      />
    </>
  );
}

function ExternalCard({ node, selected, children }: { node: MapNode; selected: boolean; children?: ReactNode }) {
  const { issue, closed } = node;
  const color = statusPalette(issue.state.color, useTheme()).readable;
  return (
    <div className={classes('external-card', closed && 'closed', selected && 'selected')} data-identifier={issue.identifier}>
      {children}
      <div className="card-top">
        <StatusIcon type={issue.state.type} color={color} />
        <LabelChips labels={issue.labels} />
        <LinearLink issue={issue} />
      </div>
      <div className="title" title={issue.title}>
        {issue.title}
      </div>
      <div className="external-project">{issue.project?.name ?? 'No project'}</div>
    </div>
  );
}

function ExternalNode({ id, data, selected }: NodeProps<MapFlowNode>) {
  const ring = statusPalette(data.node.issue.state.color, useTheme()).readable;
  return (
    <>
      <ExternalCard node={data.node} selected={selected}>
        <Handles />
      </ExternalCard>
      <RefreshLayers id={id} ring={ring} region="card" drawOld={(previous) => <ExternalCard node={previous} selected={selected} />} />
    </>
  );
}

export const nodeTypes = { issue: IssueNode, plate: PlateNode, external: ExternalNode };
