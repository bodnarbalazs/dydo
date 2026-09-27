import type { Project } from '../api/types';

/** A row of the picker's list: a Project, or one of the Closed section's two controls. */
export type Row =
  | { kind: 'project'; project: Project; cue: boolean }
  | { kind: 'closed'; count: number; expanded: boolean }
  | { kind: 'showAll'; count: number };

export interface Section {
  key: string;
  label: string;
  rows: Row[];
}

export interface ListState {
  query: string;
  closedExpanded: boolean;
  showAllClosed: boolean;
}

const OPEN_TYPES: Record<string, string> = { started: 'In Progress', planned: 'Planned', paused: 'Paused', backlog: 'Backlog' };
const CLOSED_SHOWN = 5;

const byName = (a: Project, b: Project) => a.name.localeCompare(b.name, undefined, { sensitivity: 'base' });

// Earliest target first, so overdue Projects lead; undated last.
function byTarget(a: Project, b: Project): number {
  if (a.targetDate === b.targetDate) return byName(a, b);
  if (a.targetDate === null) return 1;
  if (b.targetDate === null) return -1;
  return a.targetDate < b.targetDate ? -1 : 1;
}

// Newest completion first; ISO timestamps sort as text.
function byCompleted(a: Project, b: Project): number {
  return (b.completedAt ?? '').localeCompare(a.completedAt ?? '') || byName(a, b);
}

function openTypes(projects: Project[]): string[] {
  const unknown = projects
    .map((project) => project.status.type)
    .filter((type) => !(type in OPEN_TYPES) && type !== 'completed' && type !== 'canceled');
  return [...Object.keys(OPEN_TYPES), ...new Set(unknown)];
}

const projectRows = (projects: Project[], cue: boolean): Row[] => projects.map((project) => ({ kind: 'project', project, cue }));

function openSections(projects: Project[]): Section[] {
  return openTypes(projects).flatMap((type) => {
    const members = projects.filter((project) => project.status.type === type).sort(byTarget);
    if (members.length === 0) return [];
    return [{ key: type, label: OPEN_TYPES[type] ?? members[0]!.status.name, rows: projectRows(members, false) }];
  });
}

function closedSection(projects: Project[], state: ListState): Section[] {
  const closed = projects.filter((project) => project.status.type === 'completed').sort(byCompleted);
  if (closed.length === 0) return [];
  const rows: Row[] = [{ kind: 'closed', count: closed.length, expanded: state.closedExpanded }];
  if (state.closedExpanded) {
    const shown = state.showAllClosed ? closed : closed.slice(0, CLOSED_SHOWN);
    rows.push(...projectRows(shown, false));
    if (shown.length < closed.length) rows.push({ kind: 'showAll', count: closed.length });
  }
  return [{ key: 'closed', label: 'Closed', rows }];
}

// Matches keep the order of the open list, then Completed, then Canceled.
function searchSection(projects: Project[], query: string): Section[] {
  const needle = query.toLocaleLowerCase();
  const matches = projects.filter((project) => project.name.toLocaleLowerCase().includes(needle));
  const types = [...openTypes(matches), 'completed', 'canceled'];
  const rank = (project: Project) => types.indexOf(project.status.type);
  const inType = (a: Project, b: Project) => (a.status.type === 'completed' ? byCompleted(a, b) : byTarget(a, b));
  matches.sort((a, b) => rank(a) - rank(b) || inType(a, b));
  return [{ key: 'search', label: 'Matching Projects', rows: projectRows(matches, true) }];
}

/** The picker's list for `state`: status sections and Closed, or every name match while searching. */
export function pickerSections(projects: Project[], state: ListState): Section[] {
  const query = state.query.trim();
  if (query !== '') return searchSection(projects, query);
  return [...openSections(projects), ...closedSection(projects, state)];
}

export function rowKey(row: Row): string {
  return row.kind === 'project' ? row.project.id : `#${row.kind}`;
}

const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

function localDate(today: Date): string {
  const pad = (value: number) => String(value).padStart(2, '0');
  return `${String(today.getFullYear())}-${pad(today.getMonth() + 1)}-${pad(today.getDate())}`;
}

/** A target date as the row shows it: `Oct 3`, plus `, 2027` outside today's year; overdue before today's local date. */
export function targetLabel(targetDate: string, today: Date): { text: string; overdue: boolean } {
  const [year, month, day] = targetDate.split('-').map(Number) as [number, number, number];
  const short = `${MONTHS[month - 1]!} ${String(day)}`;
  return {
    text: year === today.getFullYear() ? short : `${short}, ${String(year)}`,
    overdue: targetDate < localDate(today),
  };
}
