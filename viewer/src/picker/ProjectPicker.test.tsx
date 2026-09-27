import { fireEvent, render, screen, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { Project } from '../api/types';
import { ProjectPicker } from './ProjectPicker';

function project(name: string, type: string, statusName: string, dates: Partial<Pick<Project, 'targetDate' | 'completedAt'>> = {}): Project {
  return { id: `id-${name}`, name, url: 'u', status: { name: statusName, type }, targetDate: null, completedAt: null, canceledAt: null, ...dates };
}

const done = (name: string, day: number) => project(name, 'completed', 'Completed', { completedAt: `2026-09-0${String(day)}T00:00:00.000Z` });

const projects = [
  project('Map', 'started', 'In Progress', { targetDate: '2026-10-03' }),
  project('Late', 'started', 'In Progress', { targetDate: '2026-09-01' }),
  project('Next year', 'planned', 'Planned', { targetDate: '2027-01-15' }),
  project('Idea', 'backlog', 'Backlog'),
  project('Dropped map', 'canceled', 'Canceled'),
  ...[1, 2, 3, 4, 5, 6].map((day) => done(`Done ${String(day)}`, day)),
];

function renderPicker(overrides: Partial<Parameters<typeof ProjectPicker>[0]> = {}) {
  const props = { projects, value: null, disabled: false, onSelect: vi.fn(), ...overrides };
  render(<ProjectPicker {...props} />);
  return props;
}

const trigger = () => screen.getByRole('button', { name: /^Project / });
const search = () => screen.getByRole('combobox', { name: 'Search Projects' });
const optionNames = () => screen.getAllByRole('option').map((option) => option.textContent);
const key = (name: string) => fireEvent.keyDown(search(), { key: name });
const activeText = () => document.getElementById(search().getAttribute('aria-activedescendant') ?? '')?.textContent;

function openPicker() {
  fireEvent.click(trigger());
}

beforeEach(() => {
  vi.useFakeTimers({ toFake: ['Date'] });
  vi.setSystemTime(new Date(2026, 8, 27, 12));
});

afterEach(() => {
  vi.useRealTimers();
});

describe('ProjectPicker', () => {
  it('names the selection in a closed listbox trigger', () => {
    renderPicker({ value: 'id-Map' });
    expect(trigger().textContent).toBe('Map▾');
    expect(trigger().getAttribute('aria-haspopup')).toBe('listbox');
    expect(trigger().getAttribute('aria-expanded')).toBe('false');
    expect(screen.queryByRole('listbox')).toBeNull();
  });

  it('prompts for a Project until one is chosen, and is disabled without a team', () => {
    renderPicker({ disabled: true });
    expect(trigger().textContent).toBe('Choose a Project▾');
    expect(trigger()).toHaveProperty('disabled', true);
  });

  it('opens status sections in order, focused on the search, with dates and overdue tint', () => {
    renderPicker();
    openPicker();
    expect(trigger().getAttribute('aria-expanded')).toBe('true');
    expect(document.activeElement).toBe(search());
    expect(search().getAttribute('aria-controls')).toBe(screen.getByRole('listbox').id);
    expect(screen.getAllByRole('group').map((group) => group.getAttribute('aria-label'))).toEqual(['In Progress', 'Planned', 'Backlog', 'Closed']);
    expect(optionNames()).toEqual(['LateSep 1', 'MapOct 3', 'Next yearJan 15, 2027', 'Idea', '▸Closed (6)']);
    expect(screen.getByText('Sep 1').className).toBe('picker-date overdue');
    expect(screen.getByText('Oct 3').className).toBe('picker-date');
  });

  it('expands Closed to 5 and a Show all row, then all, and collapses it again on the next open', () => {
    renderPicker();
    openPicker();
    fireEvent.click(screen.getByRole('option', { name: 'Closed (6)' }));
    const closed = within(screen.getByRole('group', { name: 'Closed' }));
    expect(closed.getAllByRole('option').map((option) => option.textContent)).toEqual(['▾Closed (6)', 'Done 6', 'Done 5', 'Done 4', 'Done 3', 'Done 2', 'Show all 6']);
    fireEvent.click(screen.getByRole('option', { name: 'Show all 6' }));
    expect(closed.getAllByRole('option')).toHaveLength(7);
    expect(screen.getByRole('option', { name: 'Done 1' })).toBeTruthy();
    fireEvent.click(trigger());
    expect(screen.queryByRole('listbox')).toBeNull();
    openPicker();
    expect(within(screen.getByRole('group', { name: 'Closed' })).getAllByRole('option')).toHaveLength(1);
  });

  it('searches every status by name with a status cue, Canceled included', () => {
    renderPicker();
    openPicker();
    fireEvent.change(search(), { target: { value: 'MAP' } });
    expect(screen.getAllByRole('group').map((group) => group.getAttribute('aria-label'))).toEqual(['Matching Projects']);
    expect(optionNames()).toEqual(['MapIn ProgressOct 3', 'Dropped mapCanceled']);
    expect(screen.getByText('Canceled').className).toBe('picker-cue cue-canceled');
    fireEvent.change(search(), { target: { value: 'nothing' } });
    expect(screen.getByText('No Projects match.')).toBeTruthy();
  });

  it('says when the team has no Projects', () => {
    renderPicker({ projects: [] });
    openPicker();
    expect(screen.getByText('This team has no Projects.')).toBeTruthy();
    key('Enter');
    key('ArrowDown');
    expect(search().getAttribute('aria-activedescendant')).toBeNull();
  });

  it('picks a Project by click, closes and returns focus to the trigger', () => {
    const props = renderPicker();
    openPicker();
    fireEvent.mouseDown(screen.getByRole('option', { name: 'Idea' }));
    fireEvent.click(screen.getByRole('option', { name: 'Idea' }));
    expect(props.onSelect).toHaveBeenCalledWith('id-Idea');
    expect(screen.queryByRole('listbox')).toBeNull();
    expect(document.activeElement).toBe(trigger());
  });

  it('does not report re-picking the selected Project, which it marks selected and active', () => {
    const props = renderPicker({ value: 'id-Idea' });
    openPicker();
    expect(screen.getByRole('option', { name: 'Idea' }).getAttribute('aria-selected')).toBe('true');
    expect(screen.getByRole('option', { name: /^Map/ }).getAttribute('aria-selected')).toBe('false');
    expect(activeText()).toBe('Idea');
    key('Enter');
    expect(props.onSelect).not.toHaveBeenCalled();
    expect(screen.queryByRole('listbox')).toBeNull();
  });

  it('moves with ArrowDown, ArrowUp, Home and End, clamped at the ends', () => {
    renderPicker();
    fireEvent.keyDown(trigger(), { key: 'ArrowDown' });
    expect(activeText()).toBe('LateSep 1');
    key('ArrowUp');
    expect(activeText()).toBe('LateSep 1');
    key('ArrowDown');
    expect(activeText()).toBe('MapOct 3');
    expect(screen.getByRole('option', { name: /^Map/ }).className).toContain('active');
    key('End');
    expect(activeText()).toBe('▸Closed (6)');
    key('ArrowDown');
    expect(activeText()).toBe('▸Closed (6)');
    key('Home');
    expect(activeText()).toBe('LateSep 1');
  });

  it('opens Closed and Show all with Enter, keeping the active row in place', () => {
    const props = renderPicker();
    openPicker();
    key('End');
    key('Enter');
    expect(activeText()).toBe('▾Closed (6)');
    key('End');
    key('Enter');
    expect(activeText()).toBe('Done 1');
    key('Enter');
    expect(props.onSelect).toHaveBeenCalledWith('id-Done 1');
  });

  it('starts a search on its first match', () => {
    const props = renderPicker({ value: 'id-Idea' });
    openPicker();
    fireEvent.change(search(), { target: { value: 'done 3' } });
    key('Enter');
    expect(props.onSelect).toHaveBeenCalledWith('id-Done 3');
  });

  it('closes on Escape, returning focus, and on Tab or an outside click without it', () => {
    renderPicker();
    openPicker();
    key('Escape');
    expect(screen.queryByRole('listbox')).toBeNull();
    expect(document.activeElement).toBe(trigger());
    openPicker();
    key('Tab');
    expect(screen.queryByRole('listbox')).toBeNull();
    openPicker();
    fireEvent.mouseDown(search());
    key('a');
    expect(screen.getByRole('listbox')).toBeTruthy();
    fireEvent.mouseDown(document.body);
    expect(screen.queryByRole('listbox')).toBeNull();
  });

  it('opens from the trigger with ArrowUp and ignores other keys there', () => {
    renderPicker();
    fireEvent.keyDown(trigger(), { key: 'x' });
    expect(screen.queryByRole('listbox')).toBeNull();
    fireEvent.keyDown(trigger(), { key: 'ArrowUp' });
    expect(screen.getByRole('listbox')).toBeTruthy();
  });
});
