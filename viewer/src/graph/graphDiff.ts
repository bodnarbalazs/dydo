import type { Graph, Issue, Relation } from '../api/types';
import { pickableIds } from './rules';

export type ChangedField = 'state' | 'assignee' | 'title' | 'parent' | 'labels' | 'pickable';

/** A blocking relation: `from` blocks `to`. */
export interface Block {
  from: string;
  to: string;
}

export interface GraphDiff {
  /** Issue id to the fields that differ, for issues on both graphs. */
  changed: ReadonlyMap<string, ChangedField[]>;
  added: ReadonlySet<string>;
  removed: ReadonlySet<string>;
  addedBlocks: Block[];
  removedBlocks: Block[];
}

/** What a refresh changed, by issue id across the Project's and the external issues. */
export function diffGraphs(before: Graph, after: Graph): GraphDiff {
  const old = issuesById(before);
  const now = issuesById(after);
  const pickable = { before: pickableIds(before), after: pickableIds(after) };
  const changed = new Map<string, ChangedField[]>();
  for (const [id, issue] of now) {
    const was = old.get(id);
    if (was === undefined) continue;
    const fields = changedFields(was, issue, pickable.before.has(id) !== pickable.after.has(id));
    if (fields.length > 0) changed.set(id, fields);
  }
  const oldBlocks = blocksOf(before.relations);
  const newBlocks = blocksOf(after.relations);
  return {
    changed,
    added: new Set([...now.keys()].filter((id) => !old.has(id))),
    removed: new Set([...old.keys()].filter((id) => !now.has(id))),
    addedBlocks: [...newBlocks].filter(([key]) => !oldBlocks.has(key)).map(([, block]) => block),
    removedBlocks: [...oldBlocks].filter(([key]) => !newBlocks.has(key)).map(([, block]) => block),
  };
}

function changedFields(was: Issue, now: Issue, pickable: boolean): ChangedField[] {
  const differs: [ChangedField, boolean][] = [
    ['state', was.state.name !== now.state.name || was.state.type !== now.state.type || was.state.color !== now.state.color],
    ['assignee', was.assignee !== now.assignee],
    ['title', was.title !== now.title],
    ['parent', was.parentId !== now.parentId],
    ['labels', labelSet(was) !== labelSet(now)],
    ['pickable', pickable],
  ];
  return differs.filter(([, differ]) => differ).map(([field]) => field);
}

/** The issue's labels by name and colour, in a fixed order, so reordering is no change. */
function labelSet(issue: Issue): string {
  return JSON.stringify(issue.labels.map(({ name, color }) => `${color} ${name}`).sort((a, b) => a.localeCompare(b)));
}

function issuesById(graph: Graph): Map<string, Issue> {
  return new Map([...graph.issues, ...graph.external].map((issue) => [issue.id, issue]));
}

function blocksOf(relations: Relation[]): Map<string, Block> {
  return new Map(
    relations.filter((relation) => relation.type === 'blocks').map(({ from, to }) => [`${from}->${to}`, { from, to }]),
  );
}

/** The toolbar notice after a refresh, e.g. `3 changed · 1 new · 1 removed`. */
export function describeDiff(diff: GraphDiff): string {
  const counts: [number, string, string][] = [
    [diff.changed.size, 'changed', 'changed'],
    [diff.added.size, 'new', 'new'],
    [diff.removed.size, 'removed', 'removed'],
    [diff.addedBlocks.length, 'link added', 'links added'],
    [diff.removedBlocks.length, 'link removed', 'links removed'],
  ];
  const parts = counts.filter(([count]) => count > 0).map(([count, one, many]) => `${count} ${count === 1 ? one : many}`);
  return parts.length === 0 ? 'No changes' : parts.join(' · ');
}
