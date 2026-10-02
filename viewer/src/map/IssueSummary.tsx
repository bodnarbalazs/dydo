import type { Issue } from '../api/types';
import { LabelChips } from './LabelChips';
import { StatusIcon } from './StatusIcon';

interface IssueSummaryProps {
  issue: Issue;
  pickable: boolean;
  /** The status colour as it reads on this card. */
  color: string;
}

/** The labels, Linear link, title, status and assignee every map card shows. */
export function IssueSummary({ issue, pickable, color }: IssueSummaryProps) {
  return (
    <>
      <div className="card-top">
        <LabelChips labels={issue.labels} />
        <LinearLink issue={issue} />
      </div>
      <div className="title" title={issue.title}>
        {issue.title}
      </div>
      <div className="card-bottom">
        <span className="state" style={{ color }}>
          <StatusIcon type={issue.state.type} color={color} />
          <span className="state-name">{issue.state.name}</span>
        </span>
        {pickable ? (
          <span className="pickable-badge">Pickable</span>
        ) : (
          <span className={issue.assignee === null ? 'assignee unassigned' : 'assignee'}>{issue.assignee ?? 'unassigned'}</span>
        )}
      </div>
    </>
  );
}

/** The identifier, opening the issue in Linear in a new tab without selecting the card. */
export function LinearLink({ issue }: { issue: Issue }) {
  return (
    <a
      className="linear-link"
      href={issue.url}
      target="_blank"
      rel="noopener"
      title={`Open ${issue.identifier} in Linear`}
      aria-label={`Open ${issue.identifier} in Linear`}
      onClick={(event) => event.stopPropagation()}
    >
      {issue.identifier} ↗
    </a>
  );
}
