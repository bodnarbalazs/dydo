import type { Project, Team } from '../api/types';

interface ToolbarProps {
  teams: Team[];
  projects: Project[];
  team: string | null;
  project: string | null;
  hasPlates: boolean;
  showRelated: boolean;
  summary: string | null;
  onTeam: (id: string) => void;
  onProject: (id: string) => void;
  onCollapseAll: () => void;
  onExpandAll: () => void;
  onShowRelated: (show: boolean) => void;
}

export function Toolbar(props: ToolbarProps) {
  const { teams, projects, team, project, hasPlates, showRelated, summary } = props;
  return (
    <header className="toolbar">
      <span className="brand">dydo map</span>
      <label>
        Team
        <select value={team ?? ''} onChange={(event) => props.onTeam(event.target.value)}>
          <option value="" disabled>
            Choose a team
          </option>
          {teams.map((option) => (
            <option key={option.id} value={option.id}>
              {option.name} ({option.key})
            </option>
          ))}
        </select>
      </label>
      <label>
        Project
        <select value={project ?? ''} disabled={team === null} onChange={(event) => props.onProject(event.target.value)}>
          <option value="" disabled>
            Choose a Project
          </option>
          {projects.map((option) => (
            <option key={option.id} value={option.id}>
              {option.name} · {option.status.name}
            </option>
          ))}
        </select>
      </label>
      <button type="button" disabled={!hasPlates} onClick={props.onCollapseAll}>
        Collapse all
      </button>
      <button type="button" disabled={!hasPlates} onClick={props.onExpandAll}>
        Expand all
      </button>
      <label className="toggle">
        <input type="checkbox" checked={showRelated} onChange={(event) => props.onShowRelated(event.target.checked)} />
        Show related
      </label>
      {summary !== null && <span className="summary">{summary}</span>}
    </header>
  );
}
