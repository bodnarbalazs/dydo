import { describe, expect, it } from 'vitest';
import type { Project } from '../api/types';
import { pickerSections, rowKey, targetLabel, type ListState, type Row } from './projectList';

function project(name: string, type: string, dates: Partial<Pick<Project, 'targetDate' | 'completedAt' | 'canceledAt'>> = {}, status = type): Project {
  return { id: name, name, url: `u/${name}`, status: { name: status, type }, targetDate: null, completedAt: null, canceledAt: null, ...dates };
}

const closedState: ListState = { query: '', closedExpanded: false, showAllClosed: false };
const names = (rows: Row[]) => rows.map((row) => (row.kind === 'project' ? row.project.name : rowKey(row)));
const view = (projects: Project[], state: Partial<ListState> = {}) =>
  pickerSections(projects, { ...closedState, ...state }).map((section) => [section.label, names(section.rows)]);

const completed = (name: string, day: number) => project(name, 'completed', { completedAt: `2026-09-${String(day).padStart(2, '0')}T12:00:00.000Z` });
const sevenDone = [3, 9, 1, 7, 5, 2, 8].map((day) => completed(`Done ${String(day)}`, day));

describe('pickerSections', () => {
  it('heads open sections In Progress, Planned, Paused, Backlog, then unknown types, and hides empty ones and Canceled', () => {
    const projects = [
      project('Idea', 'backlog'),
      project('Custom', 'triage', {}, 'Triage'),
      project('Halt', 'paused'),
      project('Gone', 'canceled'),
      project('Run', 'started'),
    ];
    expect(view(projects)).toEqual([
      ['In Progress', ['Run']],
      ['Paused', ['Halt']],
      ['Backlog', ['Idea']],
      ['Triage', ['Custom']],
    ]);
  });

  it('orders a section by earliest target date, then undated alphabetically', () => {
    const projects = [
      project('zeta', 'planned'),
      project('Later', 'planned', { targetDate: '2026-12-01' }),
      project('Alpha', 'planned'),
      project('Overdue', 'planned', { targetDate: '2026-01-15' }),
      project('Same b', 'planned', { targetDate: '2026-12-01' }),
    ];
    expect(view(projects)).toEqual([['Planned', ['Overdue', 'Later', 'Same b', 'Alpha', 'zeta']]]);
  });

  it('collapses Closed to its count, holding completed Projects only', () => {
    expect(view([...sevenDone, project('Gone', 'canceled')])).toEqual([['Closed', ['#closed']]]);
    expect(pickerSections(sevenDone, closedState)[0]?.rows[0]).toEqual({ kind: 'closed', count: 7, expanded: false });
  });

  it('expands Closed to the 5 newest completions and a Show all row', () => {
    expect(view(sevenDone, { closedExpanded: true })).toEqual([['Closed', ['#closed', 'Done 9', 'Done 8', 'Done 7', 'Done 5', 'Done 3', '#showAll']]]);
    expect(pickerSections(sevenDone, { ...closedState, closedExpanded: true })[0]?.rows.at(-1)).toEqual({ kind: 'showAll', count: 7 });
  });

  it('shows every completion after Show all, and no Show all row for 5 or fewer', () => {
    expect(view(sevenDone, { closedExpanded: true, showAllClosed: true })[0]?.[1]).toHaveLength(8);
    expect(view(sevenDone.slice(0, 5), { closedExpanded: true })).toEqual([['Closed', ['#closed', 'Done 9', 'Done 7', 'Done 5', 'Done 3', 'Done 1']]]);
  });

  it('omits Closed without completed Projects', () => {
    expect(view([project('Run', 'started')])).toEqual([['In Progress', ['Run']]]);
  });

  it('searches every status by name, case-insensitive, in list order with a status cue', () => {
    const projects = [
      project('Map canceled', 'canceled'),
      project('map done old', 'completed', { completedAt: '2026-01-01T00:00:00.000Z' }),
      project('MAP done new', 'completed', { completedAt: '2026-02-01T00:00:00.000Z' }),
      project('Map idea', 'backlog'),
      project('Map run', 'started'),
      project('Other', 'started'),
    ];
    const [results] = pickerSections(projects, { ...closedState, query: '  mAp ' });
    expect(results?.label).toBe('Matching Projects');
    expect(names(results?.rows ?? [])).toEqual(['Map run', 'Map idea', 'MAP done new', 'map done old', 'Map canceled']);
    expect(results?.rows.every((row) => row.kind === 'project' && row.cue)).toBe(true);
  });

  it('answers an empty result for a search that matches nothing', () => {
    expect(view([project('Run', 'started')], { query: 'zzz' })).toEqual([['Matching Projects', []]]);
  });
});

describe('rowKey', () => {
  it('keys a Project by its id and a control by its kind', () => {
    expect(rowKey({ kind: 'project', project: project('Run', 'started'), cue: false })).toBe('Run');
    expect(rowKey({ kind: 'showAll', count: 7 })).toBe('#showAll');
  });
});

describe('targetLabel', () => {
  const today = new Date(2026, 8, 27, 23, 30);

  it.each([
    ['2026-10-03', 'Oct 3', false],
    ['2026-09-27', 'Sep 27', false],
    ['2026-09-26', 'Sep 26', true],
    ['2027-01-15', 'Jan 15, 2027', false],
    ['2025-12-31', 'Dec 31, 2025', true],
  ])('%s reads %s, overdue %s', (targetDate, text, overdue) => {
    expect(targetLabel(targetDate, today)).toEqual({ text, overdue });
  });

  it('compares against the local date, padding month and day', () => {
    expect(targetLabel('2026-02-08', new Date(2026, 1, 9)).overdue).toBe(true);
    expect(targetLabel('2026-02-09', new Date(2026, 1, 9)).overdue).toBe(false);
  });
});
