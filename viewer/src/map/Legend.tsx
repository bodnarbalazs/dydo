import { EDGE_COLORS } from '../layout/toFlow';

export function Legend() {
  return (
    <div className="legend" aria-label="Legend">
      <LegendLine color={EDGE_COLORS.blocks} label="blocks (arrow points at the blocked issue)" arrow />
      <LegendLine color={EDGE_COLORS.muted} label="resolved blocker" arrow />
      <LegendLine color={EDGE_COLORS.related} label="related" dashed />
      <div className="legend-row">
        <span className="legend-swatch pickable-swatch" /> pickable
      </div>
      <div className="legend-row">
        <span className="legend-swatch external-swatch" /> issue outside this Project
      </div>
    </div>
  );
}

function LegendLine({ color, label, arrow = false, dashed = false }: { color: string; label: string; arrow?: boolean; dashed?: boolean }) {
  return (
    <div className="legend-row">
      <svg width="34" height="10" aria-hidden="true">
        <line x1="1" y1="5" x2={arrow ? 26 : 33} y2="5" stroke={color} strokeWidth="2" {...(dashed ? { strokeDasharray: '5 3' } : {})} />
        {arrow && <path d="M26 1 L33 5 L26 9 z" fill={color} />}
      </svg>
      {label}
    </div>
  );
}
