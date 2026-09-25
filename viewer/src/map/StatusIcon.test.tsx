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

  it.each([
    ['triage', ['circle', 'path d="M4.5 7h5M7.5 5l2 2-2 2"']],
    ['backlog', ['circle']],
    ['unstarted', ['circle']],
    ['started', ['circle', 'path d="M7 3.5a3.5 3.5 0 0 1 0 7z"']],
    ['completed', ['circle', 'path d="M4.2 7.2l1.9 1.9 3.7-3.8"']],
    ['canceled', ['circle', 'path d="M4.8 4.8l4.4 4.4M9.2 4.8l-4.4 4.4"']],
    ['duplicate', ['circle', 'path d="M4.3 5.7h5.4M4.3 8.3h5.4"']],
  ] as [StateType, string[]][])('draws %s with its own marks', (type, marks) => {
    const { container } = render(<StatusIcon type={type} color="#123456" />);
    const drawn = [...container.querySelectorAll('svg > *')].map((mark) => (mark.tagName === 'path' ? `path d="${mark.getAttribute('d') ?? ''}"` : mark.tagName));
    expect(drawn).toEqual(marks);
  });

  it('draws backlog as a dashed ring', () => {
    expect(glyph('backlog')).toContain('stroke-dasharray');
    expect(glyph('unstarted')).not.toContain('stroke-dasharray');
  });
});
