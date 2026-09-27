import type { GraphDiff } from '../graph/graphDiff';
import { edgeId, type MapNode } from '../graph/mapModel';
import type { MapFlow } from '../layout/toFlow';

/** The refresh's timing, in milliseconds; MapCanvas hands each to styles.css as a custom property. */
export const TIMING = {
  /** Removed issues and links fade out on the old map before the new layout lands. */
  exit: 300,
  /** Nodes that moved glide to their new place, and the cascade waits for them. */
  glide: 400,
  /** Between one changed or added issue and the next. */
  stagger: 150,
  /** A changed card's old look fades into its new one; an added card fades and grows in. */
  crossFade: 400,
  /** The ring in the new status colour fades out. */
  pulse: 3000,
} as const;

export const REDUCED_MOTION = '(prefers-reduced-motion: reduce)';

/** A node's part in a refresh; `previous` is its old look, left out when motion is reduced. */
export type NodeMark = { kind: 'changed'; delay: number; previous: MapNode | null } | { kind: 'added'; delay: number } | { kind: 'removed' };
export type EdgeMark = { kind: 'added'; delay: number } | { kind: 'removed' };

export interface Cascade {
  nodes: ReadonlyMap<string, NodeMark>;
  edges: ReadonlyMap<string, EdgeMark>;
  /** Some node moved or resized: nodes glide and the links wait for them. */
  glide: boolean;
}

/** How long the old map shows its fading removals before the new layout replaces it. */
export function exitHold(diff: GraphDiff, reduced: boolean): number {
  return !reduced && (diff.removed.size > 0 || diff.removedBlocks.length > 0) ? TIMING.exit : 0;
}

/** The old map's marks while a refresh lays out: its removed issues and blocking links fade. */
export function exitCascade(diff: GraphDiff): Cascade {
  const removed = { kind: 'removed' } as const;
  return {
    nodes: new Map([...diff.removed].map((id) => [id, removed])),
    edges: new Map(diff.removedBlocks.map(({ from, to }) => [edgeId('blocks', from, to), removed])),
    glide: false,
  };
}

/** The new map's marks: changed and added issues one after another in reading order, and the links they gained. */
export function planCascade(diff: GraphDiff, before: MapFlow, after: MapFlow, reduced: boolean): Cascade {
  const oldPlaces = placesOf(before);
  const newPlaces = placesOf(after);
  const glide = !reduced && [...newPlaces].some(([id, place]) => {
    const was = oldPlaces.get(id);
    return was !== undefined && (was.x !== place.x || was.y !== place.y || was.width !== place.width || was.height !== place.height);
  });
  const start = glide ? TIMING.glide : 0;
  const oldLooks = new Map(before.nodes.map((node) => [node.id, node.data.node]));
  const marked = [...newPlaces.values()].filter(({ id }) => diff.changed.has(id) || diff.added.has(id));

  const nodes = new Map<string, Exclude<NodeMark, { kind: 'removed' }>>();
  readingOrder(marked).forEach((id, index) => {
    const delay = reduced ? 0 : start + index * TIMING.stagger;
    const previous = oldLooks.get(id);
    nodes.set(id, previous === undefined ? { kind: 'added', delay } : { kind: 'changed', delay, previous: reduced ? null : previous });
  });
  const edges = new Map<string, EdgeMark>();
  for (const { from, to } of diff.addedBlocks) {
    edges.set(edgeId('blocks', from, to), { kind: 'added', delay: nodes.get(to)?.delay ?? start });
  }
  return { nodes, edges, glide };
}

interface Place {
  id: string;
  x: number;
  y: number;
  width: number;
  height: number;
}

/** Each node's box on the canvas; a sub-issue's position is relative to its plate. */
function placesOf(flow: MapFlow): Map<string, Place> {
  const places = new Map<string, Place>();
  // Parents precede their children in a flow, so each parent is placed first.
  for (const node of flow.nodes) {
    const parent = node.parentId === undefined ? undefined : places.get(node.parentId);
    places.set(node.id, {
      id: node.id,
      x: node.position.x + (parent?.x ?? 0),
      y: node.position.y + (parent?.y ?? 0),
      width: node.width ?? 0,
      height: node.height ?? 0,
    });
  }
  return places;
}

/** Nodes whose tops lie within this of a row's first node read as that row: half a card. */
const ROW_BAND = 48;

/** Top row first, each row left to right: how a reader scans the map. */
export function readingOrder(places: { id: string; x: number; y: number }[]): string[] {
  const rows: { top: number; items: { id: string; x: number }[] }[] = [];
  for (const place of [...places].sort((a, b) => a.y - b.y)) {
    const row = rows.at(-1);
    if (row !== undefined && place.y - row.top <= ROW_BAND) row.items.push(place);
    else rows.push({ top: place.y, items: [place] });
  }
  return rows.flatMap((row) => row.items.sort((a, b) => a.x - b.x).map((item) => item.id));
}
