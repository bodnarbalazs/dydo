export interface UrlState {
  team: string | null;
  project: string | null;
  focus: string | null;
}

const KEYS = ['team', 'project', 'focus'] as const;

export function readUrlState(search: string): UrlState {
  const params = new URLSearchParams(search);
  return { team: params.get('team'), project: params.get('project'), focus: params.get('focus') };
}

/** The query string for a state; empty values are left out. */
export function toSearch(state: UrlState): string {
  const params = new URLSearchParams();
  KEYS.forEach((key) => {
    const value = state[key];
    if (value) params.set(key, value);
  });
  const search = params.toString();
  return search === '' ? '' : `?${search}`;
}
