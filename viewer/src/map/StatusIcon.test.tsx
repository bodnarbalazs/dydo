import { render } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import type { StateType } from '../api/types';
import { StatusIcon } from './StatusIcon';

const TYPES: StateType[] = ['triage', 'backlog', 'unstarted', 'started', 'completed', 'canceled', 'duplicate'];

function glyph(type: StateType): string {
  const { container } = render(<StatusIcon type={type} color="#123456" />);
  return container.innerHTML;
}

describe('StatusIcon', () => {
  it('draws a distinct glyph for every state type in the status colour', () => {
    const glyphs = TYPES.map(glyph);
    expect(new Set(glyphs).size).toBe(TYPES.length);
    glyphs.forEach((markup, index) => {
      expect(markup).toContain('#123456');
      expect(markup).toContain(`data-state-type="${TYPES[index] ?? ''}"`);
    });
  });

  it('draws backlog as a dashed ring', () => {
    expect(glyph('backlog')).toContain('stroke-dasharray');
    expect(glyph('unstarted')).not.toContain('stroke-dasharray');
  });
});
