import type { Theme } from '../theme/theme';

/** The dark theme's card and plate surfaces; styles.css uses the same values. */
export const DARK_CARD = '#1c1d22';
const DARK_PLATE = '#141518';

/** A status colour dark enough to read on a white card: very light colours are mixed toward slate. */
export function readableColor(hex: string): string {
  const rgb = parseHex(hex);
  if (rgb === null) return '#64748b';
  if (luminance(rgb) < 0.72) return hex;
  const mix = (channel: number, slate: number): number => Math.round(channel * 0.45 + slate * 0.55);
  return toHex([mix(rgb[0], 100), mix(rgb[1], 116), mix(rgb[2], 139)]);
}

/** A status colour light enough to read on a dark card: dark colours are mixed toward pale slate. */
export function readableOnDark(hex: string): string {
  const rgb = parseHex(hex);
  if (rgb === null) return '#94a3b8';
  if (luminance(rgb) >= 0.42) return hex;
  const mix = (channel: number, pale: number): number => Math.round(channel * 0.3 + pale * 0.7);
  return toHex([mix(rgb[0], 203), mix(rgb[1], 213), mix(rgb[2], 225)]);
}

/** The colour mixed into white at the given strength: an opaque wash for card backgrounds. */
export function wash(hex: string, strength: number): string {
  return mixInto([255, 255, 255], hex, strength);
}

/** The colour mixed into a dark surface, the dark card by default: a faint tint rather than a pastel. */
export function darkWash(hex: string, strength: number, base: string = DARK_CARD): string {
  return mixInto(rgbOf(base), hex, strength);
}

export interface StatusPalette {
  /** Status text, icons and minimap marks. */
  readable: string;
  /** A plate's outline: the readable colour on light, a muted one on dark so pale statuses do not glare. */
  frame: string;
  /** A card's or a plate header's background. */
  card: string;
  /** An expanded plate's body, fainter than the cards on it. */
  plate: string;
}

/** How a status colour is drawn on each theme's surfaces. */
export function statusPalette(hex: string, theme: Theme): StatusPalette {
  if (theme === 'light') {
    const readable = readableColor(hex);
    return { readable, frame: readable, card: wash(hex, 0.2), plate: wash(hex, 0.1) };
  }
  return { readable: readableOnDark(hex), frame: darkWash(hex, 0.45, DARK_PLATE), card: darkWash(hex, 0.1), plate: darkWash(hex, 0.06, DARK_PLATE) };
}

type Rgb = [number, number, number];

/** A malformed colour reads as slate. */
function rgbOf(hex: string): Rgb {
  return parseHex(hex) ?? [100, 116, 139];
}

function mixInto(base: Rgb, hex: string, strength: number): string {
  const rgb = rgbOf(hex);
  const mix = (index: 0 | 1 | 2): number => Math.round(base[index] + (rgb[index] - base[index]) * strength);
  return toHex([mix(0), mix(1), mix(2)]);
}

function luminance([r, g, b]: Rgb): number {
  return (0.2126 * r + 0.7152 * g + 0.0722 * b) / 255;
}

function parseHex(hex: string): Rgb | null {
  const match = /^#([0-9a-f]{6})$/i.exec(hex);
  if (match?.[1] === undefined) return null;
  const value = Number.parseInt(match[1], 16);
  return [(value >> 16) & 255, (value >> 8) & 255, value & 255];
}

function toHex(rgb: Rgb): string {
  return `#${rgb.map((channel) => channel.toString(16).padStart(2, '0')).join('')}`;
}
