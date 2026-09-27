import { describe, expect, it } from 'vitest';
import { darkWash, DARK_CARD, readableColor, readableOnDark, statusPalette, wash } from './colors';

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

describe('readableOnDark', () => {
  it('keeps a colour that already reads on a dark card', () => {
    expect(readableOnDark('#f2c94c')).toBe('#f2c94c');
    expect(readableOnDark('#5e6ad2')).toBe('#5e6ad2');
  });

  it.each([
    ['#0f783c', '#93b9b0'],
    ['#000000', '#8e959e'],
  ])('lightens a dark colour toward pale slate: %s reads as %s', (hex, readable) => {
    expect(readableOnDark(hex)).toBe(readable);
  });

  it.each([
    ['#6c6c6c', '#6c6c6c'],
    ['#6b6b6b', '#aeb5be'],
  ])('lightens below a luminance of 0.42: %s reads as %s', (hex, readable) => {
    expect(readableOnDark(hex)).toBe(readable);
  });

  it('falls back to pale slate for a malformed colour', () => {
    expect(readableOnDark('teal')).toBe('#94a3b8');
  });
});

describe('darkWash', () => {
  it('mixes the colour into the dark card surface, not white', () => {
    expect(darkWash('#ffffff', 0)).toBe(DARK_CARD);
    expect(darkWash('#ffffff', 1)).toBe('#ffffff');
    expect(darkWash('#ffffff', 0.5)).toBe('#8e8e91');
  });

  it('mixes into another dark base when given one', () => {
    expect(darkWash('#ffffff', 0.5, '#000000')).toBe('#808080');
  });

  it('washes slate for a malformed colour', () => {
    expect(darkWash('nope', 1)).toBe('#64748b');
  });
});

describe('statusPalette', () => {
  it('keeps today\'s light colours', () => {
    const readable = readableColor('#f2c94c');
    expect(statusPalette('#f2c94c', 'light')).toEqual({ readable, frame: readable, card: wash('#f2c94c', 0.2), plate: wash('#f2c94c', 0.1) });
  });

  it('tints the dark surface faintly and reads on it', () => {
    const palette = statusPalette('#0f783c', 'dark');
    expect(palette.readable).toBe(readableOnDark('#0f783c'));
    expect(palette.card).toBe(darkWash('#0f783c', 0.1));
    expect(palette.plate).toBe(darkWash('#0f783c', 0.06, '#141518'));
    expect(palette.frame).toBe(darkWash('#0f783c', 0.45, '#141518'));
  });
});
