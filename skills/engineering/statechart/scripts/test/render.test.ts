import assert from "node:assert/strict";
import fs from "node:fs";
import { createRequire } from "node:module";
import path from "node:path";
import { before, test } from "node:test";
import { fileURLToPath, pathToFileURL } from "node:url";
import { fuzz, type Html, type Mermaid, type Render } from "./labels.ts";

const here = path.dirname(fileURLToPath(import.meta.url));
const charts = [];
for (const f of fs.readdirSync(path.join(here, "charts")))
  charts.push(path.join(here, "charts", f));
for (const d of fs.readdirSync(path.join(here, "corpus")))
  charts.push(path.join(here, "corpus", d, "chart.mmd"));
const guide = fs.readFileSync(path.join(here, "../../references/render.md"), "utf8");
const css = guide.match(/```css\n([\s\S]*?)```/);
assert.ok(css, "references/render.md has no css block");
const theme = css[1];
// Pele is the managed module dydo verifies; Mermaid and jsdom are restored from this package's lock.
const pele = process.env.STATECHART_PELE;
const locked = createRequire(path.join(here, "../package.json"));
const missing = (() => {
  if (!pele) return "STATECHART_PELE must name the managed Pele 1.9.0 module";
  const { version } = JSON.parse(fs.readFileSync(path.resolve(pele, "../../package.json"), "utf8"));
  if (version !== "1.9.0") return `STATECHART_PELE names Pele ${version}, not 1.9.0`;
  try {
    locked.resolve("mermaid");
    locked.resolve("jsdom");
  } catch {
    return "Mermaid 11.16.0 and jsdom 26.1.0 come from pnpm-lock.yaml; run pnpm install --frozen-lockfile in scripts/";
  }
  return false;
})();

let render: Render;
let mermaid: Mermaid;
let html: Html;
before(async () => {
  if (missing) return;
  ({ render } = await import(pathToFileURL(pele!).href));

  const { window } = new (locked("jsdom").JSDOM)("");
  Object.assign(globalThis, { window, document: window.document, DOMParser: window.DOMParser });
  mermaid = (await import(pathToFileURL(locked.resolve("mermaid")).href)).default;
  html = (markup) => {
    const t = window.document.createElement("template");
    t.innerHTML = markup;
    return t.content.textContent ?? "";
  };
});

const classes = new Set<string>();
for (const chart of charts) {
  const name = path.relative(here, chart);
  const text = fs.readFileSync(chart, "utf8");
  const marks = [...text.matchAll(/^ {2}class (\w+) (\w+)$/gm)];
  for (const [, , mark] of marks) classes.add(mark);

  test(`${name} renders in Pele 1.9.0 with each class on its node`, { skip: missing }, () => {
    const { svg } = render(text);
    assert.match(svg, /^<svg /);
    for (const [, id, mark] of marks)
      assert.match(svg, new RegExp(`class="pele-node[^"]* ${mark}\\b[^"]*" data-id="${id}"`), id);
  });

  test(`${name} parses in mermaid.js, which chat views use`, { skip: missing }, async () => {
    await mermaid.parse(text);
  });
}

test("random labels and colors keep their text and the chart's shape in Mermaid and Pele", { skip: missing }, async () => {
  const { drawn, refused, failure } = await fuzz({ seed: 1, charts: 500, render, mermaid, html });
  if (failure) assert.fail(failure);
  assert.ok(drawn > 400 && refused > 20, `${drawn} charts drawn, ${refused} refused`);
});

test("the theme in references/render.md colors each class that the charts use", () => {
  assert.ok(classes.size > 0);
  for (const name of classes) assert.match(theme, new RegExp(`\\.pele \\.${name} rect \\{`), name);
});
