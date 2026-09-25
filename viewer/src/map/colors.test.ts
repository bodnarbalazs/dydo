import { describe, expect, it } from 'vitest';
import { readableColor, wash } from './colors';

describe('readableColor', () => {
  it('keeps a colour that already reads on white', () => {
    expect(readableColor('#5e6ad2')).toBe('#5e6ad2');
  });

  it('darkens a very light colour toward slate', () => {
    expect(readableColor('#e2e2e2')).toBe('#9da6b2');
    expect(readableColor('#b4cb96')).toBe('#889b90');
  });

  it.each([
    ['#b7b7b7', '#b7b7b7'],
    ['#b8b8b8', '#8a939f'],
    ['#0aef92', '#3cab8e'],
  ])('darkens from a luminance of 0.72 up: %s reads as %s', (hex, readable) => {
    expect(readableColor(hex)).toBe(readable);
  });

  it('reads upper-case hex', () => {
    expect(readableColor('#5E6AD2')).toBe('#5E6AD2');
  });

  it.each(['teal', 'x#5e6ad2', '#5e6ad2x'])('falls back to slate for the malformed colour %s', (hex) => {
    expect(readableColor(hex)).toBe('#64748b');
  });
});

describe('wash', () => {
  it('mixes the colour into white', () => {
    expect(wash('#000000', 0.25)).toBe('#bfbfbf');
    expect(wash('#5e6ad2', 0)).toBe('#ffffff');
    expect(wash('#000000', 0.3)).toBe('#b3b3b3');
    expect(wash('#000000', 1)).toBe('#000000');
  });

  it('washes slate for a malformed colour', () => {
    expect(wash('nope', 1)).toBe('#64748b');
  });
});
