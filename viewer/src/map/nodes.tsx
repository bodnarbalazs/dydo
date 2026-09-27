import { Handle, Position, type NodeProps } from '@xyflow/react';
import type { MapFlowNode } from '../layout/toFlow';
import type { Issue } from '../api/types';
import { useTheme } from '../theme/useTheme';
import { statusPalette } from './colors';
import { IssueSummary, LinearButton } from './IssueSummary';
import { useMapActions } from './MapActions';
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

function IssueNode({ data, selected }: NodeProps<MapFlowNode>) {
  const { issue, pickable, closed } = data.node;
  const palette = statusPalette(issue.state.color, useTheme());
  return (
    <div
      className={classes('issue-card', issue.state.type === 'started' && 'started', closed && 'closed', pickable && 'pickable', selected && 'selected')}
      style={{ borderLeftColor: issue.state.color, background: palette.card, ...started(issue) }}
      data-identifier={issue.identifier}
    >
      <Handles />
      <IssueSummary issue={issue} pickable={pickable} color={palette.readable} />
    </div>
  );
}

function PlateNode({ data, selected }: NodeProps<MapFlowNode>) {
  const { issue, pickable, closed, collapsed, descendants } = data.node;
  const { togglePlate } = useMapActions();
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
      <Handles />
      <div className="plate-header" style={{ borderLeftColor: issue.state.color, background: palette.card }}>
        <IssueSummary issue={issue} pickable={pickable} color={palette.readable} />
      </div>
      <button
        type="button"
        className="plate-toggle"
        aria-expanded={!collapsed}
        aria-label={`${collapsed ? 'Expand' : 'Collapse'} ${issue.identifier}`}
        onClick={() => togglePlate(issue.id)}
      >
        {collapsed ? '▸' : '▾'} {descendants} sub-issue{descendants === 1 ? '' : 's'}
      </button>
    </div>
  );
}

function ExternalNode({ data, selected }: NodeProps<MapFlowNode>) {
  const { issue, closed } = data.node;
  const color = statusPalette(issue.state.color, useTheme()).readable;
  return (
    <div className={classes('external-card', closed && 'closed', selected && 'selected')} data-identifier={issue.identifier}>
      <Handles />
      <div className="card-top">
        <StatusIcon type={issue.state.type} color={color} />
        <span className="identifier">{issue.identifier}</span>
        <LinearButton issue={issue} />
      </div>
      <div className="title" title={issue.title}>
        {issue.title}
      </div>
      <div className="external-project">{issue.project?.name ?? 'No project'}</div>
    </div>
  );
}

export const nodeTypes = { issue: IssueNode, plate: PlateNode, external: ExternalNode };
