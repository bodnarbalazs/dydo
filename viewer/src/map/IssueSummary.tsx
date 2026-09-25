import type { Issue } from '../api/types';
import { readableColor } from './colors';
import { StatusIcon } from './StatusIcon';

interface IssueSummaryProps {
  issue: Issue;
  pickable: boolean;
}

/** The identifier, title, status and assignee every map card shows. */
export function IssueSummary({ issue, pickable }: IssueSummaryProps) {
  const color = readableColor(issue.state.color);
  return (
    <>
      <div className="card-top">
        <span className="identifier">{issue.identifier}</span>
        {pickable && <span className="pickable-badge">Pickable</span>}
        <LinearButton issue={issue} />
      </div>
      <div className="title" title={issue.title}>
        {issue.title}
      </div>
      <div className="card-bottom">
        <span className="state" style={{ color }}>
          <StatusIcon type={issue.state.type} color={color} />
          <span className="state-name">{issue.state.name}</span>
        </span>
        <span className={issue.assignee === null ? 'assignee unassigned' : 'assignee'}>{issue.assignee ?? 'unassigned'}</span>
      </div>
    </>
  );
}

export function LinearButton({ issue }: { issue: Issue }) {
  return (
    <a
      className="linear-button"
      href={issue.url}
      target="_blank"
      rel="noopener"
      title={`Open ${issue.identifier} in Linear`}
      aria-label={`Open ${issue.identifier} in Linear`}
      onClick={(event) => event.stopPropagation()}
    >
      Linear ↗
    </a>
  );
}
