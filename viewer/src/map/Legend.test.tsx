import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { EDGE_COLORS } from '../layout/toFlow';
import { Legend } from './Legend';

/** Each line sample as its colour, where the line ends, its dash and its arrowhead fill. */
function samples() {
  return [...document.querySelectorAll('.legend-row')].flatMap((row) => {
    const line = row.querySelector('line');
    if (line === null) return [];
    return [
      {
        label: row.textContent,
        stroke: line.getAttribute('stroke'),
        end: line.getAttribute('x2'),
        dash: line.getAttribute('stroke-dasharray'),
        arrow: row.querySelector('path')?.getAttribute('fill') ?? null,
      },
    ];
  });
}

describe('Legend', () => {
  it('explains the three edge styles', () => {
    render(<Legend />);
    expect(samples()).toEqual([
      { label: 'blocks (arrow points at the blocked issue)', stroke: EDGE_COLORS.blocks, end: '26', dash: null, arrow: EDGE_COLORS.blocks },
      { label: 'resolved blocker', stroke: EDGE_COLORS.muted, end: '26', dash: null, arrow: EDGE_COLORS.muted },
      { label: 'related', stroke: EDGE_COLORS.related, end: '33', dash: '5 3', arrow: null },
    ]);
  });

  it('shows the pickable and outside-Project swatches', () => {
    render(<Legend />);
    expect(screen.getByLabelText('Legend')).toBeTruthy();
    const swatches = [...document.querySelectorAll('.legend-swatch')].map((swatch) => [swatch.className, swatch.parentElement?.textContent?.trim()]);
    expect(swatches).toEqual([
      ['legend-swatch pickable-swatch', 'pickable'],
      ['legend-swatch external-swatch', 'issue outside this Project'],
    ]);
  });
});
