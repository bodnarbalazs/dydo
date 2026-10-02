import type { Issue } from '../api/types';

const SHOWN = 3;

/**
 * Up to three label chips, by name. The row is one line tall and clips what wraps, so a chip that
 * does not fit whole is hidden rather than cut; only a lone first chip shrinks, with an ellipsis.
 */
export function LabelChips({ labels }: { labels: Issue['labels'] }) {
  if (labels.length === 0) return null;
  const sorted = labels.toSorted((a, b) => a.name.localeCompare(b.name, 'en', { sensitivity: 'base' }));
  return (
    <div className="labels" title={sorted.map((label) => label.name).join(', ')}>
      {sorted.slice(0, SHOWN).map((label) => (
        <span key={label.name} className="label-chip">
          <span className="label-dot" style={{ background: label.color }} />
          <span className="label-name">{label.name}</span>
        </span>
      ))}
    </div>
  );
}
