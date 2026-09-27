import { useEffect, useId, useMemo, useRef, useState, type KeyboardEvent } from 'react';
import type { Project } from '../api/types';
import { pickerSections, rowKey, targetLabel, type ListState, type Row } from './projectList';

interface ProjectPickerProps {
  projects: Project[];
  value: string | null;
  disabled: boolean;
  onSelect: (id: string) => void;
}

// Every open starts here: no search, Closed collapsed. Nothing carries over between opens.
const FRESH: ListState = { query: '', closedExpanded: false, showAllClosed: false };

/** The Project choice: a button naming the selection that opens a searchable, status-grouped listbox. */
export function ProjectPicker({ projects, value, disabled, onSelect }: ProjectPickerProps) {
  const id = useId();
  const [open, setOpen] = useState(false);
  const [list, setList] = useState(FRESH);
  const [active, setActive] = useState<string | null>(null);
  const [today, setToday] = useState(() => new Date());
  const root = useRef<HTMLDivElement>(null);
  const trigger = useRef<HTMLButtonElement>(null);

  const sections = useMemo(() => pickerSections(projects, list), [projects, list]);
  const rows = sections.flatMap((section) => section.rows);
  const activeRow = rows.find((row) => rowKey(row) === active) ?? rows[0];
  const optionId = (row: Row) => `${id}-option-${rowKey(row)}`;
  const listId = `${id}-list`;
  const activeId = activeRow === undefined ? undefined : optionId(activeRow);

  useEffect(() => {
    if (!open) return undefined;
    const onOutside = (event: MouseEvent) => {
      if (!root.current?.contains(event.target as Node)) setOpen(false);
    };
    // Capture: the map's pan handler stops mousedown from bubbling to the document.
    document.addEventListener('mousedown', onOutside, true);
    return () => document.removeEventListener('mousedown', onOutside, true);
  }, [open]);

  useEffect(() => {
    if (activeId !== undefined) document.getElementById(activeId)?.scrollIntoView?.({ block: 'nearest' });
  }, [activeId]);

  const show = () => {
    setList(FRESH);
    setActive(value);
    setToday(new Date());
    setOpen(true);
  };

  const close = () => {
    setOpen(false);
    trigger.current?.focus();
  };

  // A control row that disappears hands the active mark to the row now in its place.
  const relist = (next: ListState, at: number) => {
    setList(next);
    const nextRows = pickerSections(projects, next).flatMap((section) => section.rows);
    const kept = nextRows.find((row) => rowKey(row) === active);
    const row = kept ?? nextRows[Math.min(at, nextRows.length - 1)];
    setActive(row === undefined ? null : rowKey(row));
  };

  const activate = (row: Row) => {
    const at = rows.indexOf(row);
    setActive(rowKey(row));
    if (row.kind === 'closed') relist({ ...list, closedExpanded: !row.expanded, showAllClosed: false }, at);
    else if (row.kind === 'showAll') relist({ ...list, showAllClosed: true }, at);
    else {
      if (row.project.id !== value) onSelect(row.project.id);
      close();
    }
  };

  const onKeyDown = (event: KeyboardEvent) => {
    const at = activeRow === undefined ? -1 : rows.indexOf(activeRow);
    const moves: Record<string, number> = { ArrowDown: at + 1, ArrowUp: at - 1, Home: 0, End: rows.length - 1 };
    const target = moves[event.key];
    if (target !== undefined) {
      const row = rows[Math.max(0, Math.min(target, rows.length - 1))];
      if (row !== undefined) setActive(rowKey(row));
    } else if (event.key === 'Enter') {
      if (activeRow !== undefined) activate(activeRow);
    } else if (event.key === 'Escape') {
      close();
    } else {
      if (event.key === 'Tab') setOpen(false);
      return;
    }
    event.preventDefault();
  };

  const selected = projects.find((project) => project.id === value);
  return (
    <div className="picker" ref={root}>
      <span id={`${id}-label`}>Project</span>
      <button
        ref={trigger}
        type="button"
        className="picker-trigger"
        disabled={disabled}
        aria-haspopup="listbox"
        aria-expanded={open}
        aria-labelledby={`${id}-label ${id}-value`}
        onClick={() => (open ? setOpen(false) : show())}
        onKeyDown={(event) => {
          if (event.key !== 'ArrowDown' && event.key !== 'ArrowUp') return;
          event.preventDefault();
          show();
        }}
      >
        <span id={`${id}-value`} className="picker-value">
          {selected?.name ?? 'Choose a Project'}
        </span>
        <span className="picker-caret" aria-hidden="true">
          ▾
        </span>
      </button>
      {open && (
        <div className="picker-popup">
          <input
            // The popup exists only while open, so mounting it is the moment to take focus.
            autoFocus
            className="picker-search"
            role="combobox"
            aria-expanded="true"
            aria-controls={listId}
            aria-autocomplete="list"
            aria-activedescendant={activeId}
            aria-label="Search Projects"
            placeholder="Search Projects"
            value={list.query}
            onChange={(event) => {
              setList({ ...list, query: event.target.value });
              setActive(null);
            }}
            onKeyDown={onKeyDown}
          />
          <div id={listId} role="listbox" aria-label="Projects" className="picker-list">
            {sections.map((section) => (
              <div key={section.key} role="group" aria-label={section.label} className="picker-group">
                {section.key !== 'closed' && section.key !== 'search' && (
                  <div className="picker-heading" aria-hidden="true">
                    {section.label}
                  </div>
                )}
                {section.rows.map((row) => (
                  <div
                    key={rowKey(row)}
                    id={optionId(row)}
                    role="option"
                    aria-selected={row.kind === 'project' && row.project.id === value}
                    className={`picker-row picker-${row.kind}${row === activeRow ? ' active' : ''}`}
                    // Keeps focus in the search field, so the list stays open and keyboard-driven.
                    onMouseDown={(event) => event.preventDefault()}
                    onClick={() => activate(row)}
                  >
                    <RowContent row={row} today={today} />
                  </div>
                ))}
              </div>
            ))}
          </div>
          {rows.length === 0 && <p className="picker-empty">{list.query.trim() === '' ? 'This team has no Projects.' : 'No Projects match.'}</p>}
        </div>
      )}
    </div>
  );
}

function RowContent({ row, today }: { row: Row; today: Date }) {
  if (row.kind === 'closed') {
    return (
      <>
        <span className="picker-caret" aria-hidden="true">
          {row.expanded ? '▾' : '▸'}
        </span>
        <span className="picker-name">Closed ({row.count})</span>
      </>
    );
  }
  if (row.kind === 'showAll') return <span className="picker-name">Show all {row.count}</span>;
  const { project } = row;
  const date = project.targetDate === null ? null : targetLabel(project.targetDate, today);
  return (
    <>
      <span className="picker-name">{project.name}</span>
      {row.cue && <span className={`picker-cue cue-${project.status.type}`}>{project.status.name}</span>}
      {date !== null && <span className={`picker-date${date.overdue ? ' overdue' : ''}`}>{date.text}</span>}
    </>
  );
}
