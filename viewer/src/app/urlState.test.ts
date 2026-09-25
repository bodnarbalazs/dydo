import { describe, expect, it } from 'vitest';
import { readUrlState, toSearch } from './urlState';

describe('url state', () => {
  it('reads team, project and focus', () => {
    expect(readUrlState('?team=t&project=p&focus=f')).toEqual({ team: 't', project: 'p', focus: 'f' });
  });

  it('reads missing keys as null', () => {
    expect(readUrlState('')).toEqual({ team: null, project: null, focus: null });
  });

  it('writes only the keys that have values, in a stable order', () => {
    expect(toSearch({ focus: 'f', team: 't', project: null })).toBe('?team=t&focus=f');
    expect(toSearch({ team: null, project: null, focus: null })).toBe('');
    expect(toSearch({ team: '', project: 'p', focus: null })).toBe('?project=p');
  });

  it('round-trips', () => {
    const state = { team: 'a b', project: 'p&q', focus: 'x' };
    expect(readUrlState(toSearch(state))).toEqual(state);
  });
});
