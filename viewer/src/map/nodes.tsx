import { Handle, Position, type NodeProps } from '@xyflow/react';
import type { MapFlowNode } from '../layout/toFlow';
import { readableColor, wash } from './colors';
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

function IssueNode({ data, selected }: NodeProps<MapFlowNode>) {
  const { issue, pickable, closed } = data.node;
  return (
    <div
      className={classes('issue-card', closed && 'closed', pickable && 'pickable', selected && 'selected')}
      style={{ borderLeftColor: issue.state.color, background: wash(issue.state.color, 0.2) }}
      data-identifier={issue.identifier}
    >
      <Handles />
      <IssueSummary issue={issue} pickable={pickable} />
    </div>
  );
}

function PlateNode({ data, selected }: NodeProps<MapFlowNode>) {
  const { issue, pickable, closed, collapsed, descendants } = data.node;
  const { togglePlate } = useMapActions();
  return (
    <div
      className={classes('plate', collapsed && 'collapsed', closed && 'closed', pickable && 'pickable', selected && 'selected')}
      style={{ borderColor: readableColor(issue.state.color), background: collapsed ? undefined : wash(issue.state.color, 0.1) }}
      data-identifier={issue.identifier}
    >
      <Handles />
      <div className="plate-header" style={{ borderLeftColor: issue.state.color, background: wash(issue.state.color, 0.2) }}>
        <IssueSummary issue={issue} pickable={pickable} />
      </div>
      <button
        type="button"
        className="plate-toggle nodrag"
        aria-expanded={!collapsed}
        aria-label={`${collapsed ? 'Expand' : 'Collapse'} ${issue.identifier}`}
        onClick={(event) => {
          event.stopPropagation();
          togglePlate(issue.id);
        }}
      >
        {collapsed ? '▸' : '▾'} {descendants} sub-issue{descendants === 1 ? '' : 's'}
      </button>
    </div>
  );
}

function ExternalNode({ data, selected }: NodeProps<MapFlowNode>) {
  const { issue, closed } = data.node;
  const color = readableColor(issue.state.color);
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
