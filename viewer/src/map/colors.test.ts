import { describe, expect, it } from 'vitest';
import { readableColor, wash } from './colors';

describe('readableColor', () => {
  it('keeps a colour that already reads on white', () => {
    expect(readableColor('#5e6ad2')).toBe('#5e6ad2');
  });

  it('darkens a very light colour toward slate', () => {
    expect(readableColor('#e2e2e2')).toBe('#9da6b2');
  });

  it('falls back to slate for a malformed colour', () => {
    expect(readableColor('teal')).toBe('#64748b');
  });
});

describe('wash', () => {
  it('mixes the colour into white', () => {
    expect(wash('#000000', 0.25)).toBe('#bfbfbf');
    expect(wash('#5e6ad2', 0)).toBe('#ffffff');
  });

  it('washes slate for a malformed colour', () => {
    expect(wash('nope', 1)).toBe('#64748b');
  });
});
