import { ReactFlowProvider } from '@xyflow/react';
import type { ELK } from 'elkjs/lib/elk-api';
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { ApiError, fetchGraph, fetchProjects, fetchTeams } from '../api/client';
import type { Graph, Issue, Project, Team } from '../api/types';
import { describeDiff, diffGraphs, type GraphDiff } from '../graph/graphDiff';
import { buildMapModel } from '../graph/mapModel';
import { layoutMap } from '../layout/layout';
import type { MapFlow } from '../layout/toFlow';
import { exitCascade, exitHold, planCascade, REDUCED_MOTION, type Cascade } from '../map/cascade';
import { MapCanvas } from '../map/MapCanvas';
import { ThemeContext, useThemePreference } from '../theme/useTheme';
import { Toolbar } from './Toolbar';
import { readUrlState, toSearch, type UrlState } from './urlState';

interface Failure {
  code: string;
  message: string;
}

/** A request's outcome: its answer, or the failure shown to the user. */
type Loaded<T> = { value: T } | { failure: Failure };

/** An in-place refresh that replaced `from` with the current graph, and what it changed. */
interface Refresh {
  from: Graph;
  diff: GraphDiff;
}

/** The URL and what has loaded for it; moving to another team or Project starts that part empty. */
interface View {
  url: UrlState;
  projects: Loaded<Project[]> | null;
  graph: Loaded<Graph> | null;
  refresh: Refresh | null;
  /** A refresh that failed; the map it would have replaced stays. */
  refreshFailure: Failure | null;
}

/** A laid-out graph, and the marks of the refresh it landed, if it did. */
interface Layout {
  graph: Graph;
  loaded: Loaded<{ flow: MapFlow; fitKey: number }>;
  cascade: Cascade | null;
}

const NO_GRAPH = { graph: null, refresh: null, refreshFailure: null };

function failure(error: unknown): Failure {
  if (error instanceof ApiError) return { code: error.code, message: error.message };
  return { code: 'viewer_error', message: error instanceof Error ? error.message : String(error) };
}

function valueOf<T>(loaded: Loaded<T> | null): T | null {
  return loaded !== null && 'value' in loaded ? loaded.value : null;
}

function failureOf(loaded: Loaded<unknown> | null): Failure | null {
  return loaded !== null && 'failure' in loaded ? loaded.failure : null;
}

/** Hands the outcome of `request` to `done` unless the returned cleanup ran first, so a view left behind ignores late answers. */
function settle<T>(request: Promise<T>, done: (loaded: Loaded<T>) => void): () => void {
  let live = true;
  request.then(
    (value) => {
      if (live) done({ value });
    },
    (reason: unknown) => {
      if (live) done({ failure: failure(reason) });
    },
  );
  return () => {
    live = false;
  };
}

function moveTo(view: View, url: UrlState): View {
  return {
    ...view,
    url,
    projects: url.team === view.url.team ? view.projects : null,
    ...(url.project === view.url.project ? {} : NO_GRAPH),
  };
}

/** A refresh's answer: a new graph, diffed against the one it replaces, or a failure that keeps the map. */
function refreshed(view: View, loaded: Loaded<Graph>): View {
  if ('failure' in loaded) return { ...view, refreshFailure: loaded.failure };
  const from = valueOf(view.graph);
  return { ...view, graph: loaded, refresh: from === null ? null : { from, diff: diffGraphs(from, loaded.value) }, refreshFailure: null };
}

/** The marks of the refresh a new layout lands: only its first layout, replacing the map it refreshed. */
function landed(refresh: Refresh | null, shown: Layout | null, loaded: Layout['loaded'], reduced: boolean): Cascade | null {
  const before = valueOf(shown?.loaded ?? null);
  const after = valueOf(loaded);
  if (refresh === null || shown?.graph !== refresh.from || before === null || after === null) return null;
  return planCascade(refresh.diff, before.flow, after.flow, reduced);
}

const prefersReducedMotion = () => window.matchMedia(REDUCED_MOTION).matches;
const pause = (ms: number) => new Promise<void>((done) => setTimeout(done, ms));

export function App({ elk }: { elk: ELK }) {
  const [view, setView] = useState<View>(() => ({ url: readUrlState(window.location.search), projects: null, ...NO_GRAPH }));
  const [teams, setTeams] = useState<Loaded<Team[]> | null>(null);
  const [collapsed, setCollapsed] = useState<ReadonlySet<string>>(new Set());
  const [showRelated, setShowRelated] = useState(false);
  const [layout, setLayout] = useState<Layout | null>(null);
  const [refreshing, setRefreshing] = useState(false);
  const [fitKey, setFitKey] = useState(0);
  const { preference, theme, choose } = useThemePreference();

  const { url, refresh } = view;
  const graph = valueOf(view.graph);
  // While a refresh lays out, the map it replaces stays, its removals fading.
  const replacing = refresh !== null && layout?.graph === refresh.from;
  const exiting = useMemo(() => (refresh === null ? null : exitCascade(refresh.diff)), [refresh]);
  const laidOut = layout !== null && (layout.graph === graph || replacing) ? layout.loaded : null;
  const map = valueOf(laidOut);
  const mapFailure = failureOf(view.graph) ?? failureOf(laidOut);
  // One slot per stage, in a fixed order, so each failure on screen keeps its place.
  const failures = [failureOf(teams), failureOf(view.projects), mapFailure, view.refreshFailure];

  const navigate = useCallback((next: UrlState, mode: 'push' | 'replace') => {
    const target = `${window.location.pathname}${toSearch(next)}`;
    if (mode === 'push') window.history.pushState(null, '', target);
    else window.history.replaceState(null, '', target);
    setView((current) => moveTo(current, next));
  }, []);

  useEffect(() => {
    const onPop = () => {
      const next = readUrlState(window.location.search);
      setView((current) => moveTo(current, next));
    };
    window.addEventListener('popstate', onPop);
    return () => {
      window.removeEventListener('popstate', onPop);
    };
  }, []);

  useEffect(() => settle(fetchTeams(), setTeams), []);

  useEffect(() => {
    const team = url.team;
    if (team === null) return undefined;
    return settle(fetchProjects(team), (projects) => setView((current) => ({ ...current, projects })));
  }, [url.team]);

  useEffect(() => {
    const project = url.project;
    if (project === null) return undefined;
    return settle(fetchGraph(project), (loaded) => {
      setCollapsed(new Set());
      setView((current) => ({ ...current, ...NO_GRAPH, graph: loaded }));
    });
  }, [url.project]);

  const refreshMap = useCallback(() => {
    const project = url.project;
    if (project === null) return;
    setRefreshing(true);
    settle(fetchGraph(project), (loaded) => {
      setRefreshing(false);
      setView((current) => (current.url.project === project ? refreshed(current, loaded) : current));
    });
  }, [url.project]);

  // The layout effect needs to know whether its graph replaces the map on screen, without relaying out when that map changes.
  const shownLayout = useRef(layout);
  useEffect(() => {
    shownLayout.current = layout;
  });

  useEffect(() => {
    if (graph === null) return undefined;
    const reduced = prefersReducedMotion();
    const hold = refresh !== null && shownLayout.current?.graph === refresh.from ? exitHold(refresh.diff, reduced) : 0;
    const laid = layoutMap(elk, buildMapModel(graph, { collapsed, showRelated }));
    const request = (hold === 0 ? laid : Promise.all([laid, pause(hold)]).then(([flow]) => flow)).then((flow) => ({ flow, fitKey }));
    return settle(request, (loaded) => setLayout((shown) => ({ graph, loaded, cascade: landed(refresh, shown, loaded, reduced) })));
  }, [elk, graph, collapsed, showRelated, fitKey, refresh]);

  const plateIds = useMemo(() => new Set(graph?.issues.flatMap((issue) => (issue.parentId === null ? [] : [issue.parentId]))), [graph]);
  const allPlates = useMemo(() => new Set(graph?.issues.filter((issue) => plateIds.has(issue.id)).map((issue) => issue.id)), [graph, plateIds]);

  const focus = useCallback((id: string) => navigate({ ...url, focus: id }, 'replace'), [navigate, url]);
  // The pill's click also reaches its plate, which focuses it.
  const togglePlate = useCallback((id: string) => {
    setCollapsed((current) => {
      const next = new Set(current);
      if (!next.delete(id)) next.add(id);
      return next;
    });
  }, []);
  const openExternal = useCallback(
    (issue: Issue) => navigate({ team: issue.team.id, project: issue.project?.id ?? null, focus: issue.id }, 'push'),
    [navigate],
  );
  const setAll = (next: ReadonlySet<string>) => {
    setCollapsed(next);
    setFitKey((key) => key + 1);
  };

  return (
    <ThemeContext.Provider value={theme}>
      <div className="app">
        <Toolbar
          teams={valueOf(teams) ?? []}
          projects={valueOf(view.projects) ?? []}
          team={url.team}
          project={url.project}
          hasPlates={allPlates.size > 0}
          showRelated={showRelated}
          summary={graph === null ? null : summarize(graph, allPlates.size)}
          canRefresh={url.project !== null}
          refreshing={refreshing}
          notice={refreshing || view.refreshFailure !== null || refresh === null ? null : describeDiff(refresh.diff)}
          onRefresh={refreshMap}
          onTeam={(team) => navigate({ team, project: null, focus: null }, 'push')}
          onProject={(project) => navigate({ ...url, project, focus: null }, 'push')}
          onCollapseAll={() => setAll(allPlates)}
          onExpandAll={() => setAll(new Set())}
          onShowRelated={setShowRelated}
          theme={preference}
          onTheme={choose}
        />
        {failures.map(
          (shown, stage) =>
            shown !== null && (
              <div key={stage} className="error" role="alert">
                <strong>{shown.code}</strong> {shown.message}
              </div>
            ),
        )}
        <main className="canvas">
          {map === null ? (
            <Placeholder url={url} loading={mapFailure === null} />
          ) : (
            <ReactFlowProvider>
              <MapCanvas
                flow={map.flow}
                focus={url.focus}
                fitKey={map.fitKey}
                cascade={replacing ? exiting : (layout?.cascade ?? null)}
                onFocus={focus}
                onOpenExternal={openExternal}
                onTogglePlate={togglePlate}
              />
            </ReactFlowProvider>
          )}
        </main>
      </div>
    </ThemeContext.Provider>
  );
}

function summarize(graph: Graph, plates: number): string {
  return `${graph.project.name}: ${graph.issues.length} issues, ${plates} plates`;
}

function Placeholder({ url, loading }: { url: UrlState; loading: boolean }) {
  if (url.team === null) return <p className="placeholder">Choose a team to list its Projects.</p>;
  if (url.project === null) return <p className="placeholder">Choose a Project to draw its map.</p>;
  return <p className="placeholder">{loading ? 'Loading the Project map…' : 'The map could not be drawn.'}</p>;
}
