import { ApiError, fetchGraph, fetchSaved } from '../api/client';
import type { Graph } from '../api/types';
import { diffGraphs, type GraphDiff } from '../graph/graphDiff';

export interface Failure { code: string; message: string }
export type Loaded<T> = { value: T } | { failure: Failure };
export interface Refresh { from: Graph; diff: GraphDiff }
export interface VisitState {
  graph: Loaded<Graph> | null;
  refresh: Refresh | null;
  refreshing: boolean;
  refreshFailure: Failure | null;
  savedAt: string | null;
}

export const NO_GRAPH: VisitState = { graph: null, refresh: null, refreshing: false, refreshFailure: null, savedAt: null };

export function failure(error: unknown): Failure {
  if (error instanceof ApiError) return { code: error.code, message: error.message };
  return { code: 'viewer_error', message: error instanceof Error ? error.message : String(error) };
}

export function valueOf<T>(loaded: Loaded<T> | null): T | null {
  return loaded !== null && 'value' in loaded ? loaded.value : null;
}

export function failureOf(loaded: Loaded<unknown> | null): Failure | null {
  return loaded !== null && 'failure' in loaded ? loaded.failure : null;
}

/** One visit owns its requests and the saved canvas's viewing window, including subsequent Refresh. */
export class ProjectVisit {
  private readonly abort = new AbortController();
  private state: VisitState = { ...NO_GRAPH, refreshing: true };
  private pending: Graph | null = null;
  private readyAt: number | null = null;
  private timer: ReturnType<typeof setTimeout> | undefined;
  private acceptedFresh = false;

  constructor(private readonly project: string, private readonly changed: (state: VisitState) => void) {
    this.emit();
    void fetchSaved(project, this.abort.signal).then((snapshot) => {
      if (this.abort.signal.aborted || this.acceptedFresh || snapshot === null) return;
      const previousFailure = failureOf(this.state.graph);
      this.state = { ...this.state, graph: { value: snapshot.graph }, savedAt: snapshot.fetchedAt, refreshFailure: previousFailure };
      this.emit();
    }).catch(() => undefined);
    this.refresh();
  }

  refresh(): void {
    if (this.abort.signal.aborted) return;
    this.state = { ...this.state, refreshing: true, refreshFailure: null };
    this.emit();
    void fetchGraph(this.project, this.abort.signal).then(
      (graph) => { if (!this.abort.signal.aborted) { this.pending = graph; this.apply(); } },
      (error: unknown) => this.failed(error),
    );
  }

  /** The acknowledgement belongs to the exact saved graph whose fit completed. */
  ready(graph: Graph): void {
    if (this.state.savedAt === null || valueOf(this.state.graph) !== graph || this.readyAt !== null) return;
    this.readyAt = performance.now();
    this.apply();
  }

  layoutFailed(graph: Graph): void {
    if (this.state.savedAt === null || valueOf(this.state.graph) !== graph) return;
    this.state = { ...this.state, graph: this.state.refreshFailure === null ? null : { failure: this.state.refreshFailure }, savedAt: null, refreshFailure: null };
    this.emit();
    this.apply();
  }

  dispose(): void {
    this.abort.abort();
    clearTimeout(this.timer);
  }

  private apply(): void {
    if (this.abort.signal.aborted || this.pending === null) return;
    if (this.state.savedAt !== null) {
      if (this.readyAt === null) return;
      const remaining = 2000 - (performance.now() - this.readyAt);
      if (remaining > 0) {
        clearTimeout(this.timer);
        this.timer = setTimeout(() => this.apply(), remaining);
        return;
      }
    }
    const from = valueOf(this.state.graph);
    this.state = { graph: { value: this.pending }, refresh: from === null ? null : { from, diff: diffGraphs(from, this.pending) }, refreshing: false, refreshFailure: null, savedAt: null };
    this.pending = null;
    this.acceptedFresh = true;
    this.emit();
  }

  private failed(error: unknown): void {
    if (this.abort.signal.aborted) return;
    const problem = failure(error);
    this.state = { ...this.state, refreshing: false, refreshFailure: valueOf(this.state.graph) === null ? null : problem,
      graph: valueOf(this.state.graph) === null ? { failure: problem } : this.state.graph };
    this.emit();
  }

  private emit(): void { if (!this.abort.signal.aborted) this.changed(this.state); }
}
