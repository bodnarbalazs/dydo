import fs from "node:fs";
import path from "node:path";
import { pathToFileURL } from "node:url";

export async function renderer(module: string, guide: string) {
  if (!path.isAbsolute(module)) throw new Error("Renderer must be an absolute verified managed module.");
  const { render } = await import(pathToFileURL(module).href);
  if (typeof render !== "function") throw new Error("Locked Pele renderer has no render function.");
  const theme = fs.readFileSync(guide, "utf8").match(/```css\r?\n([\s\S]*?)```/)?.[1];
  if (!theme) throw new Error("Installed render guide has no verified theme.");
  return (text: string): string => {
    const { svg } = render(text, { limit: 1_000_000, outputLimit: 8_000_000, maxEdges: 10_000 });
    if (typeof svg !== "string" || !svg.startsWith("<svg")) throw new Error("Pele did not produce an SVG.");
    return svg.replace(/(<svg\b[^>]*>)/, `$1<style>${theme}</style>`);
  };
}
