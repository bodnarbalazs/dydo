import { createContext, useContext, useState, type ReactNode } from 'react';
import type { MapNode } from '../graph/mapModel';
import type { Cascade } from './cascade';

/** The latest refresh's marks, for the nodes to draw their part of it. */
export const CascadeContext = createContext<Cascade | null>(null);

interface RefreshLayersProps {
  id: string;
  /** The new status colour as it reads on this theme. */
  ring: string;
  /** A plate rings its header only. */
  region: 'card' | 'header';
  /** Draws the node's old look. */
  drawOld: (previous: MapNode) => ReactNode;
}

/** A changed node's old look fading off it and a ring in its new status colour, each at the node's turn in the cascade. */
export function RefreshLayers({ id, ring, region, drawOld }: RefreshLayersProps) {
  const mark = useContext(CascadeContext)?.nodes.get(id);
  const [faded, setFaded] = useState(false);
  if (mark === undefined || mark.kind === 'removed') return null;
  return (
    <>
      {mark.kind === 'changed' && mark.previous !== null && !faded && (
        <div className="refresh-ghost" aria-hidden="true" onAnimationEnd={(event) => setFaded(event.target === event.currentTarget)}>
          {drawOld(mark.previous)}
        </div>
      )}
      <div className={`refresh-ring refresh-${region}`} style={{ '--ring': ring } as React.CSSProperties} aria-hidden="true" />
    </>
  );
}
