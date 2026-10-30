import fs from "node:fs";
import path from "node:path";
import { pathToFileURL } from "node:url";
import { inspect } from "node:util";
import { gate, worker } from "./lifetime.ts";

try {
  const [nonce, configuration, role] = process.argv.slice(2);
  await gate(nonce);
  const input = JSON.parse(fs.readFileSync(configuration, "utf8"));
  if (input.mode === "check" && role !== "worker")
    process.exit(await worker(import.meta.filename, configuration));
  const { renderer } = await import("./render.ts");
  const render = await renderer(input.renderer, input.renderGuide);
  if (input.mode === "render-ready") {
    render("stateDiagram-v2\n[*] --> Ready\nReady --> [*]\n");
    process.exit(0);
  }
  if (input.mode !== "check") throw new Error("Unsupported native statechart operation.");
  const { checkView } = await import("./frontends/view.ts");
  const { compile } = await import("./pipeline.ts");
  const { check } = await import("./run.ts");
  const request = input.request;
  const view = checkView(request.View ? await import(pathToFileURL(request.View.Path).href) : {}, inspect);
  const { files, exit } = compile(await check(request.Spec.Path, request.Config.Path), view);
  if (exit !== 0 && exit !== 1) process.exit(2);
  const draft = path.join(input.stage, "draft");
  fs.mkdirSync(draft);
  for (const [name, parts] of files) {
    const fd = fs.openSync(path.join(draft, name), "wx");
    try {
      for (const part of parts) fs.writeSync(fd, typeof part === "string" ? Buffer.from(part) : part);
      fs.writeSync(fd, "\n");
    } finally { fs.closeSync(fd); }
  }
  fs.writeFileSync(path.join(input.stage, "verdict.json"), JSON.stringify({ verdict: exit }), { flag: "wx" });
  fs.renameSync(draft, path.join(input.stage, "done"));
  const done = path.join(input.stage, "done");
  try {
    for (const name of fs.readdirSync(done).filter(name => name.endsWith(".mmd")))
      fs.writeFileSync(path.join(done, name.replace(/\.mmd$/, ".svg")), render(fs.readFileSync(path.join(done, name), "utf8")), { flag: "wx" });
  } catch (error) {
    fs.writeFileSync(path.join(done, "render-error.json"), JSON.stringify({ state: "render-error", message: String(error) }), { flag: "wx" });
    console.error(`Statechart render error; valid report and Mermaid retained: ${String(error)}`);
    process.exit(2);
  }
  process.exit(exit);
} catch (error) {
  console.error(String(error));
  process.exit(2);
}
