/** A status colour dark enough to read on a white card: very light colours are mixed toward slate. */
export function readableColor(hex: string): string {
  const rgb = parseHex(hex);
  if (rgb === null) return '#64748b';
  const [r, g, b] = rgb;
  const luminance = (0.2126 * r + 0.7152 * g + 0.0722 * b) / 255;
  if (luminance < 0.72) return hex;
  const mix = (channel: number, slate: number): number => Math.round(channel * 0.45 + slate * 0.55);
  return toHex([mix(r, 100), mix(g, 116), mix(b, 139)]);
}

/** The colour mixed into white at the given strength: an opaque wash for card backgrounds. */
export function wash(hex: string, strength: number): string {
  const rgb = parseHex(hex) ?? [100, 116, 139];
  const mix = (channel: number): number => Math.round(255 + (channel - 255) * strength);
  return toHex([mix(rgb[0]), mix(rgb[1]), mix(rgb[2])]);
}

function parseHex(hex: string): [number, number, number] | null {
  const match = /^#([0-9a-f]{6})$/i.exec(hex);
  if (match?.[1] === undefined) return null;
  const value = Number.parseInt(match[1], 16);
  return [(value >> 16) & 255, (value >> 8) & 255, value & 255];
}

function toHex(rgb: [number, number, number]): string {
  return `#${rgb.map((channel) => channel.toString(16).padStart(2, '0')).join('')}`;
}
