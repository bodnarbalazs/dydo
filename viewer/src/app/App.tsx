import { ReactFlowProvider } from '@xyflow/react';
import type { ELK } from 'elkjs/lib/elk-api';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { ApiError, fetchGraph, fetchProjects, fetchTeams } from '../api/client';
import type { Graph, Issue, Project, Team } from '../api/types';
import { buildMapModel } from '../graph/mapModel';
import { layoutMap } from '../layout/layout';
import type { MapFlow } from '../layout/toFlow';
import { MapCanvas } from '../map/MapCanvas';
import { Toolbar } from './Toolbar';
import { readUrlState, toSearch, type UrlState } from './urlState';

interface Failure {
  code: string;
  message: string;
}

function failure(error: unknown): Failure {
  if (error instanceof ApiError) return { code: error.code, message: error.message };
  return { code: 'viewer_error', message: error instanceof Error ? error.message : String(error) };
}

export function App({ elk }: { elk: ELK }) {
  const [url, setUrl] = useState<UrlState>(() => readUrlState(window.location.search));
  const [teams, setTeams] = useState<Team[]>([]);
  const [loadedProjects, setLoadedProjects] = useState<{ team: string; projects: Project[] } | null>(null);
  const [loadedGraph, setLoadedGraph] = useState<{ project: string; graph: Graph } | null>(null);
  const [error, setError] = useState<Failure | null>(null);
  const [collapsed, setCollapsed] = useState<ReadonlySet<string>>(new Set());
  const [showRelated, setShowRelated] = useState(false);
  const [laidOut, setLaidOut] = useState<{ graph: Graph; flow: MapFlow; fitKey: number } | null>(null);
  const [fitKey, setFitKey] = useState(0);

  const projects = loadedProjects !== null && loadedProjects.team === url.team ? loadedProjects.projects : [];
  const graph = loadedGraph !== null && loadedGraph.project === url.project ? loadedGraph.graph : null;
  const shown = laidOut !== null && laidOut.graph === graph ? laidOut : null;

  const navigate = useCallback((next: UrlState, mode: 'push' | 'replace') => {
    const target = `${window.location.pathname}${toSearch(next)}`;
    if (mode === 'push') window.history.pushState(null, '', target);
    else window.history.replaceState(null, '', target);
    setUrl(next);
  }, []);

  useEffect(() => {
    const onPop = () => {
      setUrl(readUrlState(window.location.search));
    };
    window.addEventListener('popstate', onPop);
    return () => {
      window.removeEventListener('popstate', onPop);
    };
  }, []);

  useEffect(() => {
    fetchTeams().then(setTeams, (reason: unknown) => {
      setError(failure(reason));
    });
  }, []);

  useEffect(() => {
    const team = url.team;
    if (team === null) return undefined;
    let live = true;
    fetchProjects(team).then(
      (loaded) => {
        if (live) setLoadedProjects({ team, projects: loaded });
      },
      (reason: unknown) => {
        if (live) setError(failure(reason));
      },
    );
    return () => {
      live = false;
    };
  }, [url.team]);

  useEffect(() => {
    const project = url.project;
    if (project === null) return undefined;
    let live = true;
    fetchGraph(project).then(
      (loaded) => {
        if (!live) return;
        setError(null);
        setCollapsed(new Set());
        setLoadedGraph({ project, graph: loaded });
        setFitKey((key) => key + 1);
      },
      (reason: unknown) => {
        if (live) setError(failure(reason));
      },
    );
    return () => {
      live = false;
    };
  }, [url.project]);

  useEffect(() => {
    if (graph === null) return undefined;
    let live = true;
    layoutMap(elk, buildMapModel(graph, { collapsed, showRelated })).then(
      (next) => {
        if (live) setLaidOut({ graph, flow: next, fitKey });
      },
      (reason: unknown) => {
        if (live) setError(failure(reason));
      },
    );
    return () => {
      live = false;
    };
  }, [elk, graph, collapsed, showRelated, fitKey]);

  const plateIds = useMemo(() => new Set(graph?.issues.flatMap((issue) => (issue.parentId === null ? [] : [issue.parentId]))), [graph]);
  const allPlates = useMemo(() => new Set(graph?.issues.filter((issue) => plateIds.has(issue.id)).map((issue) => issue.id)), [graph, plateIds]);

  const focus = useCallback((id: string) => navigate({ ...url, focus: id }, 'replace'), [navigate, url]);
  const togglePlate = useCallback(
    (id: string) => {
      setCollapsed((current) => {
        const next = new Set(current);
        if (!next.delete(id)) next.add(id);
        return next;
      });
      focus(id);
    },
    [focus],
  );
  const openExternal = useCallback(
    (issue: Issue) => navigate({ team: issue.team.id, project: issue.project?.id ?? null, focus: issue.id }, 'push'),
    [navigate],
  );
  const setAll = (next: ReadonlySet<string>) => {
    setCollapsed(next);
    setFitKey((key) => key + 1);
  };

  return (
    <div className="app">
      <Toolbar
        teams={teams}
        projects={projects}
        team={url.team}
        project={url.project}
        hasPlates={allPlates.size > 0}
        showRelated={showRelated}
        summary={graph === null ? null : summarize(graph, allPlates.size)}
        onTeam={(team) => navigate({ team, project: null, focus: null }, 'push')}
        onProject={(project) => navigate({ ...url, project, focus: null }, 'push')}
        onCollapseAll={() => setAll(allPlates)}
        onExpandAll={() => setAll(new Set())}
        onShowRelated={setShowRelated}
      />
      {error !== null && (
        <div className="error" role="alert">
          <strong>{error.code}</strong> {error.message}
        </div>
      )}
      <main className="canvas">
        {shown === null ? (
          <Placeholder url={url} loading={error === null} />
        ) : (
          <ReactFlowProvider>
            <MapCanvas flow={shown.flow} focus={url.focus} fitKey={shown.fitKey} onFocus={focus} onOpenExternal={openExternal} onTogglePlate={togglePlate} />
          </ReactFlowProvider>
        )}
      </main>
    </div>
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
