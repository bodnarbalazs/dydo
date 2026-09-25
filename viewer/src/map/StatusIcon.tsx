import type { StateType } from '../api/types';

interface StatusIconProps {
  type: StateType;
  color: string;
}

/** One glyph per workflow state type, drawn in the status colour. */
export function StatusIcon({ type, color }: StatusIconProps) {
  return (
    <svg className="status-icon" width="14" height="14" viewBox="0 0 14 14" aria-hidden="true" data-state-type={type}>
      {GLYPHS[type](color)}
    </svg>
  );
}

const ring = (color: string, dash?: string) => (
  <circle cx="7" cy="7" r="5.5" fill="none" stroke={color} strokeWidth="1.8" {...(dash === undefined ? {} : { strokeDasharray: dash })} />
);

const GLYPHS: Record<StateType, (color: string) => React.JSX.Element> = {
  triage: (color) => (
    <>
      {ring(color)}
      <path d="M4.5 7h5M7.5 5l2 2-2 2" fill="none" stroke={color} strokeWidth="1.4" />
    </>
  ),
  backlog: (color) => ring(color, '2 1.6'),
  unstarted: (color) => ring(color),
  started: (color) => (
    <>
      {ring(color)}
      <path d="M7 3.5a3.5 3.5 0 0 1 0 7z" fill={color} />
    </>
  ),
  completed: (color) => (
    <>
      <circle cx="7" cy="7" r="6.5" fill={color} />
      <path d="M4.2 7.2l1.9 1.9 3.7-3.8" fill="none" stroke="#fff" strokeWidth="1.6" />
    </>
  ),
  canceled: (color) => (
    <>
      <circle cx="7" cy="7" r="6.5" fill={color} />
      <path d="M4.8 4.8l4.4 4.4M9.2 4.8l-4.4 4.4" stroke="#fff" strokeWidth="1.6" />
    </>
  ),
  duplicate: (color) => (
    <>
      <circle cx="7" cy="7" r="6.5" fill={color} />
      <path d="M4.3 5.7h5.4M4.3 8.3h5.4" stroke="#fff" strokeWidth="1.5" />
    </>
  ),
};
