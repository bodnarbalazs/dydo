import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { test } from "node:test";
import { renderer } from "../render.ts";

const guide = path.resolve(import.meta.dirname, "../../references/render.md");

test("Native renderer rejects cwd-dependent module resolution", async () => {
  await assert.rejects(renderer("pele", guide), /absolute verified managed module/);
});

const module = process.env.STATECHART_PELE;
test("Native renderer imports the actual locked module and preserves marks and both themes", {
  skip: !module && "STATECHART_PELE must name the actual locked Pele 1.9.0 module; no fake renderer",
}, async () => {
  assert.ok(fs.existsSync(module!), "STATECHART_PELE must name an existing module.");
  const render = await renderer(module!, guide);
  const svg = render("stateDiagram-v2\n[*] --> s0\ns0 --> s1: increment\nclassDef breaks fill:#ffecee\nclass s1 breaks\nclass s1 bugPath\n");
  assert.match(svg, /^<svg/);
  assert.match(svg, /class="pele-node[^\"]* breaks/);
  assert.match(svg, /bugPath/);
  assert.match(svg, /<style>\s*\.pele/);
  assert.match(svg, /prefers-color-scheme: dark/);
  assert.match(svg, /--pele-bg: #ffffff/);
  assert.match(svg, /--pele-bg: #1f1f1f/);
});
