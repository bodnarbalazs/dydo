import type { ThemePreference } from './theme';

const stroke = { fill: 'none', stroke: 'currentColor', strokeWidth: 1.5, strokeLinecap: 'round', strokeLinejoin: 'round' } as const;

const OPTIONS: [ThemePreference, string, React.JSX.Element][] = [
  [
    'system',
    'System',
    <>
      <rect x="1.75" y="2.5" width="12.5" height="8.5" rx="1.5" {...stroke} />
      <path d="M5.5 13.75h5M8 11v2.75" {...stroke} />
    </>,
  ],
  [
    'light',
    'Light',
    <>
      <circle cx="8" cy="8" r="3" {...stroke} />
      <path d="M8 1.5v1.5M8 13v1.5M1.5 8H3M13 8h1.5M3.4 3.4l1.06 1.06M11.54 11.54l1.06 1.06M3.4 12.6l1.06-1.06M11.54 4.46l1.06-1.06" {...stroke} />
    </>,
  ],
  ['dark', 'Dark', <path d="M13.5 9.6A5.75 5.75 0 0 1 6.4 2.5a5.75 5.75 0 1 0 7.1 7.1z" {...stroke} />],
];

/** The toolbar's System / Light / Dark switch. */
export function ThemeControl({ preference, onChoose }: { preference: ThemePreference; onChoose: (next: ThemePreference) => void }) {
  return (
    <div className="theme-control" role="group" aria-label="Theme">
      {OPTIONS.map(([value, label, icon]) => (
        <button key={value} type="button" aria-label={`${label} theme`} title={label} aria-pressed={preference === value} onClick={() => onChoose(value)}>
          <svg width="16" height="16" viewBox="0 0 16 16" aria-hidden="true">
            {icon}
          </svg>
        </button>
      ))}
    </div>
  );
}
