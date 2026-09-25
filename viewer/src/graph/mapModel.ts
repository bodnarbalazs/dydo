import type { ElkNode } from 'elkjs/lib/elk-api';
import type { Graph, Issue, Relation } from '../api/types';
import { isClosed, pickableIds } from './rules';

const CARD = { width: 280, height: 96 } as const;
const EXTERNAL = { width: 230, height: 70 } as const;
const PLATE_HEADER = 96;
// The header plus room for the collapse pill sits above the sub-issues.
const PLATE_PADDING = `[top=${String(PLATE_HEADER + 34)},left=18,bottom=18,right=18]`;

export type MapNodeKind = 'issue' | 'plate' | 'external';

export interface MapNode {
  id: string;
  kind: MapNodeKind;
  issue: Issue;
  /** The visible plate holding this node, or null at top level. */
  parentId: string | null;
  pickable: boolean;
  closed: boolean;
  collapsed: boolean;
  /** Sub-issues at any depth, for plates. */
  descendants: number;
}

export interface MapEdge {
  id: string;
  type: Relation['type'];
  source: string;
  target: string;
  muted: boolean;
}

export interface MapModel {
  /** Parents precede their children, as React Flow requires. */
  nodes: MapNode[];
  edges: MapEdge[];
  elk: ElkNode;
}

export interface ViewOptions {
  collapsed: ReadonlySet<string>;
  showRelated: boolean;
}

/** Turns a contract Graph into the visible map: plates, re-anchored edges and the ELK input. */
export function buildMapModel(graph: Graph, view: ViewOptions): MapModel {
  const tree = new IssueTree(graph.issues);
  const pickable = pickableIds(graph);
  const edges = visibleEdges(graph, view, tree);
  const linkedExternal = new Set(edges.flatMap((edge) => [edge.source, edge.target]));

  const nodes: MapNode[] = [];
  const visit = (issue: Issue, parentId: string | null): void => {
    const children = tree.children(issue.id);
    const collapsed = children.length > 0 && view.collapsed.has(issue.id);
    nodes.push({
      id: issue.id,
      kind: children.length > 0 ? 'plate' : 'issue',
      issue,
      parentId,
      pickable: pickable.has(issue.id),
      closed: isClosed(issue),
      collapsed,
      descendants: tree.descendantCount(issue.id),
    });
    if (!collapsed) children.forEach((child) => visit(child, issue.id));
  };
  tree.roots().forEach((root) => visit(root, null));
  graph.external
    .filter((issue) => linkedExternal.has(issue.id))
    .forEach((issue) =>
      nodes.push({ id: issue.id, kind: 'external', issue, parentId: null, pickable: false, closed: isClosed(issue), collapsed: false, descendants: 0 }),
    );

  return { nodes, edges, elk: elkGraph(nodes, edges, tree) };
}

function visibleEdges(graph: Graph, view: ViewOptions, tree: IssueTree): MapEdge[] {
  const byId = new Map([...graph.issues, ...graph.external].map((issue) => [issue.id, issue]));
  const edges = new Map<string, MapEdge>();
  for (const relation of graph.relations) {
    if (relation.type === 'related' && !view.showRelated) continue;
    const source = tree.anchor(relation.from, view.collapsed);
    const target = tree.anchor(relation.to, view.collapsed);
    if (source === target) continue;
    const blocker = byId.get(relation.from);
    const muted = relation.type === 'blocks' && blocker !== undefined && isClosed(blocker);
    const id = `${relation.type}:${source}->${target}`;
    const existing = edges.get(id);
    edges.set(id, { id, type: relation.type, source, target, muted: muted && (existing?.muted ?? true) });
  }
  return [...edges.values()];
}

const LAYERED: Record<string, string> = {
  'elk.algorithm': 'layered',
  'elk.direction': 'RIGHT',
  'elk.hierarchyHandling': 'INCLUDE_CHILDREN',
  'elk.edgeRouting': 'ORTHOGONAL',
  'elk.layered.mergeEdges': 'true',
  'elk.layered.nodePlacement.strategy': 'NETWORK_SIMPLEX',
  'elk.layered.compaction.postCompaction.strategy': 'EDGE_LENGTH',
  'elk.layered.spacing.nodeNodeBetweenLayers': '56',
  'elk.layered.spacing.edgeNodeBetweenLayers': '14',
  'elk.layered.spacing.edgeEdgeBetweenLayers': '6',
  'elk.spacing.nodeNode': '18',
  'elk.spacing.edgeNode': '16',
  'elk.spacing.edgeEdge': '8',
};

/**
 * Each connected group of top-level nodes is one layered layout (plates included); the groups are
 * then packed into rows. A single layered run would stack every unconnected issue in one column.
 */
function elkGraph(nodes: MapNode[], edges: MapEdge[], tree: IssueTree): ElkNode {
  const elkNodes = new Map<string, ElkNode>();
  for (const node of nodes) {
    const elkNode: ElkNode =
      node.kind === 'plate' && !node.collapsed
        ? { id: node.id, layoutOptions: { 'elk.padding': PLATE_PADDING }, children: [] }
        : { id: node.id, ...(node.kind === 'external' ? EXTERNAL : CARD) };
    elkNodes.set(node.id, elkNode);
    if (node.parentId !== null) elkNodes.get(node.parentId)?.children?.push(elkNode);
  }
  // ELK cannot route an edge between a plate and its own descendant; such edges are drawn unrouted.
  const routable = edges.filter((edge) => !tree.isAncestor(edge.source, edge.target) && !tree.isAncestor(edge.target, edge.source));
  const groups = connectedGroups(
    nodes.filter((node) => node.parentId === null).map((node) => node.id),
    routable.map((edge) => [tree.top(edge.source), tree.top(edge.target)]),
  );
  const groupOf = new Map(groups.flatMap((group, index) => group.map((id) => [id, index] as const)));
  const components = groups.map(
    (group, index): ElkNode => ({
      id: `${COMPONENT_PREFIX}${String(index)}`,
      layoutOptions: { ...LAYERED, 'elk.padding': '[top=0,left=0,bottom=0,right=0]' },
      children: group.flatMap((id) => elkNodes.get(id) ?? []),
      edges: [],
    }),
  );
  for (const edge of routable) {
    const component = components[groupOf.get(tree.top(edge.source)) ?? 0];
    component?.edges?.push({ id: edge.id, sources: [edge.source], targets: [edge.target] });
  }
  return {
    id: 'root',
    layoutOptions: {
      'elk.algorithm': 'rectpacking',
      'elk.aspectRatio': '1.7',
      'elk.spacing.nodeNode': '60',
      'elk.json.edgeCoords': 'ROOT',
      'elk.json.shapeCoords': 'PARENT',
    },
    children: components,
  };
}

export const COMPONENT_PREFIX = 'component:';

/** Groups of ids joined by links, biggest first; ties keep input order. */
export function connectedGroups(ids: string[], links: [string, string][]): string[][] {
  const root = new Map(ids.map((id) => [id, id]));
  const find = (id: string): string => {
    const up = root.get(id) ?? id;
    if (up === id) return id;
    const top = find(up);
    root.set(id, top);
    return top;
  };
  links.forEach(([a, b]) => root.set(find(a), find(b)));
  const groups = new Map<string, string[]>();
  ids.forEach((id) => {
    const key = find(id);
    groups.set(key, [...(groups.get(key) ?? []), id]);
  });
  return [...groups.values()].sort((a, b) => b.length - a.length);
}

class IssueTree {
  private readonly parent = new Map<string, string>();
  private readonly childMap = new Map<string, Issue[]>();
  private readonly issues: Issue[];

  constructor(issues: Issue[]) {
    this.issues = issues;
    const ids = new Set(issues.map((issue) => issue.id));
    for (const issue of issues) {
      if (issue.parentId === null || !ids.has(issue.parentId)) continue;
      this.parent.set(issue.id, issue.parentId);
      this.childMap.set(issue.parentId, [...(this.childMap.get(issue.parentId) ?? []), issue]);
    }
  }

  roots(): Issue[] {
    return this.issues.filter((issue) => !this.parent.has(issue.id));
  }

  children(id: string): Issue[] {
    return this.childMap.get(id) ?? [];
  }

  descendantCount(id: string): number {
    return this.children(id).reduce((sum, child) => sum + 1 + this.descendantCount(child.id), 0);
  }

  /** The node an issue is drawn as: itself, or its outermost collapsed ancestor. */
  anchor(id: string, collapsed: ReadonlySet<string>): string {
    let anchor = id;
    for (let up = this.parent.get(id); up !== undefined; up = this.parent.get(up)) {
      if (collapsed.has(up)) anchor = up;
    }
    return anchor;
  }

  /** The top-level issue an issue sits under, or the issue itself. */
  top(id: string): string {
    let top = id;
    for (let up = this.parent.get(id); up !== undefined; up = this.parent.get(up)) top = up;
    return top;
  }

  isAncestor(ancestor: string, id: string): boolean {
    for (let up = this.parent.get(id); up !== undefined; up = this.parent.get(up)) {
      if (up === ancestor) return true;
    }
    return false;
  }
}
